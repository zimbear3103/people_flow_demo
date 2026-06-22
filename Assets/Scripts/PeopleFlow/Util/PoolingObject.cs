using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    public class PoolingObject : Singleton<PoolingObject>
    {
        class Pooled : MonoBehaviour
        {
            public GameObject Prefab;
            public Vector3 OriginalScale;
        }

        readonly Dictionary<GameObject, Stack<GameObject>> m_pools =
            new Dictionary<GameObject, Stack<GameObject>>();

        public static PoolingObject EnsureInstance()
        {
            if (Instance != null) return Instance;
            new GameObject(nameof(PoolingObject)).AddComponent<PoolingObject>();
            // Re-read through the getter so the base Singleton caches m_instance (and HasInstance flips
            // true); otherwise Release's HasInstance guard would miss the pool until the next Get.
            return Instance;
        }

        public GameObject Get(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;

            GameObject go = null;
            if (m_pools.TryGetValue(prefab, out var stack))
            {
                // Skip any entries destroyed while pooled (e.g. a scene teardown that nuked them).
                while (go == null && stack.Count > 0) go = stack.Pop();
            }

            if (go == null)
            {
                go = Instantiate(prefab, parent);
                var marker = go.AddComponent<Pooled>();
                marker.Prefab = prefab;
                marker.OriginalScale = go.transform.localScale;
            }
            else
            {
                go.transform.SetParent(parent, false);
                go.SetActive(true);
            }
            return go;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;

            var marker = instance.GetComponent<Pooled>();
            if (marker == null || marker.Prefab == null)
            {
                Destroy(instance);
                return;
            }

            // Restore the authored scale so a recycled instance doesn't come back at the zero scale a
            // hop-into-hole shrank it to, then park it (inactive) under the pool.
            instance.transform.localScale = marker.OriginalScale;
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);

            if (!m_pools.TryGetValue(marker.Prefab, out var stack))
                m_pools[marker.Prefab] = stack = new Stack<GameObject>();
            stack.Push(instance);
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                var go = Get(prefab, transform);
                Release(go);
            }
        }
    }
}
