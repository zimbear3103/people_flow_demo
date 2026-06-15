using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Returns a pooled instance to <see cref="ObjectPooler"/> after a fixed lifetime.
    /// Used for fire-and-forget VFX such as the "pop" particle so the gates/target never
    /// have to track and clean them up manually.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoReturnToPool : MonoBehaviour, IPooledObject
    {
        [SerializeField] private float m_lifetime = 0.6f;

        private float m_timer;
        private ParticleSystem m_particles;

        private void Awake()
        {
            m_particles = GetComponent<ParticleSystem>();
        }

        public void OnObjectSpawn()
        {
            m_timer = m_lifetime;
            Replay();
        }

        private void OnEnable()
        {
            // Covers the case where the object is enabled without going through the pooler.
            m_timer = m_lifetime;
            Replay();
        }

        private void Replay()
        {
            // Pooled particles don't auto-restart on re-activation, so kick them here.
            if (m_particles != null)
            {
                m_particles.Clear(true);
                m_particles.Play(true);
            }
        }

        private void Update()
        {
            m_timer -= Time.deltaTime;
            if (m_timer <= 0f)
            {
                ObjectPooler pooler = ObjectPooler.Instance;
                if (pooler != null)
                    pooler.ReturnToPool(gameObject);
            }
        }
    }
}
