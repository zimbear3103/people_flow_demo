using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Releases the "flow" of people. While <see cref="PlayerInput.IsHolding"/> is true it
    /// pulls instances from the <see cref="ObjectPooler"/> at a fixed rate and gives each a
    /// small upward "jump" velocity so they arc into the pipe.
    ///
    /// The spawn rate is accumulated frame-rate-independently (so 10/s really is 10/s at
    /// 30fps or 120fps), which is far more stable than a WaitForSeconds coroutine.
    /// </summary>
    public class FlowSpawner : MonoBehaviour
    {
        [Header("Spawning")]
        [SerializeField] private string m_personTag = "Person";
        [Tooltip("People released per second while holding.")]
        [SerializeField] private float m_spawnRate = 10f;
        [Tooltip("Spawn point. Defaults to this transform if unset.")]
        [SerializeField] private Transform m_nozzle;

        [Header("Jump arc")]
        [Tooltip("Initial velocity given to each person (upward/forward pop into the pipe).")]
        [SerializeField] private Vector2 m_launchVelocity = new Vector2(0f, 3.5f);
        [Tooltip("Random horizontal spread added to the launch so the column looks alive.")]
        [SerializeField] private float m_launchJitter = 0.8f;

        [Header("Juice")]
        [Tooltip("Visual that jiggles on each spawn (the nozzle). Optional.")]
        [SerializeField] private Transform m_nozzleVisual;
        [SerializeField] private float m_nozzlePunch = 0.18f;
        [SerializeField] private float m_nozzlePunchDuration = 0.12f;

        private float m_accumulator;
        private Vector3 m_nozzleBaseScale = Vector3.one;
        private Coroutine m_jiggle;

        private void Awake()
        {
            if (m_nozzle == null)
                m_nozzle = transform;
            if (m_nozzleVisual != null)
                m_nozzleBaseScale = m_nozzleVisual.localScale;
        }

        /// <summary>Clears the spawn accumulator. Called by the controller on BeginLevel.</summary>
        public void ResetSpawner()
        {
            m_accumulator = 0f;
        }

        /// <summary>Overrides the release rate (people/second), e.g. from per-level config.</summary>
        public void SetSpawnRate(float ratePerSecond)
        {
            m_spawnRate = Mathf.Max(0f, ratePerSecond);
        }

        private void Update()
        {
            PeopleFlowGameController ctrl = PeopleFlowGameController.Instance;
            PlayerInput input = PlayerInput.Instance;

            bool canPlay = ctrl == null || ctrl.CanPlay;
            bool holding = input != null && input.IsHolding;

            if (!canPlay || !holding)
            {
                m_accumulator = 0f;     // don't bank up a burst between holds
                return;
            }

            // Accumulate fractional spawns, then release whole ones this frame.
            m_accumulator += m_spawnRate * Time.deltaTime;
            int toSpawn = Mathf.FloorToInt(m_accumulator);
            if (toSpawn <= 0)
                return;

            m_accumulator -= toSpawn;

            for (int i = 0; i < toSpawn; i++)
            {
                if (ctrl != null && !ctrl.CanSpawnMore(1))
                    break;                 // respect the global crowd cap
                if (!SpawnOne())
                    break;
            }
        }

        private bool SpawnOne()
        {
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null)
                return false;

            GameObject person = pooler.SpawnFromPool(m_personTag, m_nozzle.position, Quaternion.identity);
            if (person == null)
                return false;

            if (person.TryGetComponent(out PersonMovement movement))
            {
                Vector2 v = m_launchVelocity;
                v.x += Random.Range(-m_launchJitter, m_launchJitter);
                movement.Launch(v);
            }

            PeopleFlowGameController ctrl = PeopleFlowGameController.Instance;
            if (ctrl != null)
                ctrl.NotifyPersonSpawned();

            PlayNozzleJiggle();
            return true;
        }

        private void PlayNozzleJiggle()
        {
            if (m_nozzleVisual == null)
                return;

            // Restart so rapid spawns keep punching instead of fighting an old tween.
            if (m_jiggle != null)
                StopCoroutine(m_jiggle);

            // DOTween equivalent: m_nozzleVisual.DOPunchScale(Vector3.one * m_nozzlePunch, m_nozzlePunchDuration);
            m_jiggle = StartCoroutine(
                JuiceTween.IE_ScalePunch(m_nozzleVisual, m_nozzleBaseScale, m_nozzlePunch, m_nozzlePunchDuration));
        }
    }
}
