using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Implement this on any pooled prefab that needs a "reset" hook the moment it is
    /// taken from the pool (re-activated). Cheaper and more explicit than relying on
    /// OnEnable, and lets us reset velocity / timers deterministically.
    /// </summary>
    public interface IPooledObject
    {
        void OnObjectSpawn();
    }

    /// <summary>
    /// Lightweight tag stamped on every pooled instance so <see cref="ObjectPooler"/>
    /// can route a GameObject back to the correct pool in O(1) without dictionary lookups
    /// by instance id. Also guards against double-return.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        public string OriginTag;
        public bool IsLive;            // true while checked-out of the pool
    }

    /// <summary>
    /// Optimization core for the "people flow". A classic tag-based pool that pre-warms
    /// a fixed budget of instances and (optionally) grows on demand so the crowd can
    /// scale to hundreds of objects without per-frame Instantiate/Destroy churn (which
    /// is the #1 source of GC spikes / hitches on mobile).
    ///
    /// Usage:
    ///   ObjectPooler.Instance.SpawnFromPool("Person", pos, rot);
    ///   ObjectPooler.Instance.ReturnToPool(go);
    /// </summary>
    public class ObjectPooler : Singleton<ObjectPooler>
    {
        [System.Serializable]
        public class Pool
        {
            public string Tag;                 // string key, e.g. "Person", "PopParticle"
            public GameObject Prefab;
            public int Size = 50;              // pre-warm count
            public bool Expandable = true;     // instantiate more when the pool runs dry
        }

        [Tooltip("Pools to pre-warm on Awake. Each tag must be unique.")]
        [SerializeField] private List<Pool> m_pools = new List<Pool>();

        [Tooltip("Optional parent that holds inactive instances to keep the hierarchy tidy.")]
        [SerializeField] private Transform m_poolRoot;

        // Inactive instances ready to hand out.
        private readonly Dictionary<string, Queue<GameObject>> m_available = new Dictionary<string, Queue<GameObject>>();
        // Definition lookup by tag.
        private readonly Dictionary<string, Pool> m_lookup = new Dictionary<string, Pool>();
        // Every instance ever created per tag (used by ReturnAll for teardown).
        private readonly Dictionary<string, List<GameObject>> m_all = new Dictionary<string, List<GameObject>>();

        private bool m_initialized;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Builds every pool and pre-warms instances. Safe to call more than once.</summary>
        public void Initialize()
        {
            if (m_initialized)
                return;

            if (m_poolRoot == null)
                m_poolRoot = transform;

            foreach (Pool pool in m_pools)
            {
                if (pool == null || string.IsNullOrEmpty(pool.Tag) || pool.Prefab == null)
                {
                    GameLog.Log(LogType.Warning, "[ObjectPooler] Skipped an invalid pool definition (missing tag or prefab).");
                    continue;
                }

                if (m_lookup.ContainsKey(pool.Tag))
                {
                    GameLog.Log(LogType.Warning, $"[ObjectPooler] Duplicate pool tag '{pool.Tag}' ignored.");
                    continue;
                }

                var queue = new Queue<GameObject>(pool.Size);
                var all = new List<GameObject>(pool.Size);

                for (int i = 0; i < pool.Size; i++)
                {
                    GameObject obj = CreateInstance(pool);
                    queue.Enqueue(obj);
                    all.Add(obj);
                }

                m_available[pool.Tag] = queue;
                m_lookup[pool.Tag] = pool;
                m_all[pool.Tag] = all;
            }

            m_initialized = true;
        }

        private GameObject CreateInstance(Pool pool)
        {
            GameObject obj = Instantiate(pool.Prefab, m_poolRoot);
            obj.name = pool.Prefab.name;        // strip the "(Clone)" noise

            PooledObject tag = obj.GetComponent<PooledObject>();
            if (tag == null)
                tag = obj.AddComponent<PooledObject>();
            tag.OriginTag = pool.Tag;
            tag.IsLive = false;

            obj.SetActive(false);
            return obj;
        }

        /// <summary>
        /// Activates and returns an instance of <paramref name="tag"/> at the given pose.
        /// Grows the pool if empty and the pool is expandable; otherwise returns null.
        /// </summary>
        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!m_initialized)
                Initialize();

            if (!m_lookup.TryGetValue(tag, out Pool pool))
            {
                GameLog.Log(LogType.Error, $"[ObjectPooler] No pool exists for tag '{tag}'.");
                return null;
            }

            Queue<GameObject> queue = m_available[tag];
            GameObject obj;

            if (queue.Count > 0)
            {
                obj = queue.Dequeue();
            }
            else if (pool.Expandable)
            {
                // Demand outran the pre-warm budget: grow rather than starve the flow.
                obj = CreateInstance(pool);
                m_all[tag].Add(obj);
            }
            else
            {
                GameLog.Log(LogType.Warning, $"[ObjectPooler] Pool '{tag}' exhausted and not expandable.");
                return null;
            }

            // Position BEFORE activation so the first physics/render frame is already correct.
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            PooledObject pooled = obj.GetComponent<PooledObject>();
            if (pooled != null)
                pooled.IsLive = true;

            // Reset hook (velocity, timers, ...) without an Update poll.
            if (obj.TryGetComponent(out IPooledObject spawnable))
                spawnable.OnObjectSpawn();

            return obj;
        }

        /// <summary>Deactivates the object and returns it to its pool. Idempotent.</summary>
        public void ReturnToPool(GameObject obj)
        {
            if (obj == null)
                return;

            PooledObject pooled = obj.GetComponent<PooledObject>();
            if (pooled == null || string.IsNullOrEmpty(pooled.OriginTag) || !m_lookup.ContainsKey(pooled.OriginTag))
            {
                // Not ours (or never pooled): just hide it so we never leak a live object.
                obj.SetActive(false);
                return;
            }

            if (!pooled.IsLive)
                return;                          // already returned this frame – ignore double-return

            pooled.IsLive = false;
            obj.SetActive(false);
            obj.transform.SetParent(m_poolRoot, false);
            m_available[pooled.OriginTag].Enqueue(obj);
        }

        /// <summary>Returns every live instance of a tag to the pool. Used on level teardown.</summary>
        public void ReturnAll(string tag)
        {
            if (!m_all.TryGetValue(tag, out List<GameObject> all))
                return;

            for (int i = 0; i < all.Count; i++)
            {
                GameObject obj = all[i];
                if (obj != null && obj.activeSelf)
                    ReturnToPool(obj);
            }
        }
    }
}
