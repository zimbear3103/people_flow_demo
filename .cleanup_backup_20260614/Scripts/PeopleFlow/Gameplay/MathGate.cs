using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PeopleFlow
{
    /// <summary>
    /// A multiplier/adder gate placed inside the pipe. Every "Person" that passes through
    /// the trigger contributes extra people to the crowd:
    ///   • Multiply (x N): each person spawns (N - 1) clones  → the crowd multiplies.
    ///   • Add (+ N):      each person spawns N clones.
    ///
    /// PERFORMANCE: a burst of 100 people hitting an "x5" gate in the same frame would try
    /// to spawn 400 objects at once. To avoid that hitch we never spawn inline in the
    /// trigger callback — we accumulate a <see cref="m_pendingSpawns"/> debt and drain it
    /// at a capped rate (<see cref="m_maxSpawnsPerFrame"/>) over subsequent frames.
    /// </summary>
    public class MathGate : MonoBehaviour
    {
        public enum GateOperation
        {
            Add,
            Multiply
        }

        [Header("Math")]
        [SerializeField] private GateOperation m_operation = GateOperation.Multiply;
        [SerializeField] private int m_value = 2;
        [SerializeField] private string m_personTag = "Person";

        [Header("Batching (anti-spike)")]
        [Tooltip("Max extra people this gate may spawn per frame. Spreads big bursts over time.")]
        [SerializeField] private int m_maxSpawnsPerFrame = 12;
        [Tooltip("Random horizontal radius around the gate that clones appear in.")]
        [SerializeField] private float m_spawnSpread = 0.6f;
        [Tooltip("Vertical offset for clones. Keep negative so they spawn BELOW the trigger " +
                 "and never re-enter this gate (which would multiply forever).")]
        [SerializeField] private float m_spawnYOffset = -0.7f;
        [Tooltip("Initial velocity given to cloned people (mostly downward to keep flowing).")]
        [SerializeField] private Vector2 m_cloneLaunch = new Vector2(0f, -2f);

        [Header("Feedback")]
        [Tooltip("Label that shows the operation (e.g. 'x2'). Scales up when passed through.")]
        [SerializeField] private TextMeshPro m_label;
        [SerializeField] private float m_labelPunch = 0.35f;
        [SerializeField] private float m_labelPunchDuration = 0.18f;

        private int m_pendingSpawns;
        private Vector3 m_labelBaseScale = Vector3.one;
        private Coroutine m_labelPunchRoutine;

        private void Awake()
        {
            if (m_label != null)
                m_labelBaseScale = m_label.transform.localScale;
            RefreshLabel();
        }

        private void OnValidate()
        {
            RefreshLabel();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(m_personTag))
                return;

            // Queue the debt; the actual spawns happen (rate-limited) in Update.
            int extras = m_operation == GateOperation.Multiply
                ? Mathf.Max(0, m_value - 1)
                : Mathf.Max(0, m_value);

            m_pendingSpawns += extras;
            PlayLabelPunch();
        }

        private void Update()
        {
            if (m_pendingSpawns <= 0)
                return;

            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null)
            {
                m_pendingSpawns = 0;
                return;
            }

            PeopleFlowGameController ctrl = PeopleFlowGameController.Instance;

            int budget = Mathf.Min(m_maxSpawnsPerFrame, m_pendingSpawns);
            for (int i = 0; i < budget; i++)
            {
                if (ctrl != null && !ctrl.CanSpawnMore(1))
                {
                    // Hit the global crowd cap – drop the rest of the debt.
                    m_pendingSpawns = 0;
                    return;
                }

                Vector3 pos = transform.position;
                pos.x += Random.Range(-m_spawnSpread, m_spawnSpread);
                pos.y += m_spawnYOffset;     // below the trigger so clones don't re-trigger us
                GameObject clone = pooler.SpawnFromPool(m_personTag, pos, Quaternion.identity);
                if (clone == null)
                    break;

                if (clone.TryGetComponent(out PersonMovement movement))
                    movement.Launch(m_cloneLaunch + Random.insideUnitCircle * 0.5f);

                ctrl?.NotifyPersonSpawned();
                m_pendingSpawns--;
            }
        }

        private void RefreshLabel()
        {
            if (m_label == null)
                return;
            string prefix = m_operation == GateOperation.Multiply ? "x" : "+";
            m_label.text = prefix + m_value;
        }

        private void PlayLabelPunch()
        {
            if (m_label == null)
                return;

            if (m_labelPunchRoutine != null)
                StopCoroutine(m_labelPunchRoutine);

            // DOTween equivalent: m_label.transform.DOPunchScale(Vector3.one * m_labelPunch, m_labelPunchDuration);
            m_labelPunchRoutine = StartCoroutine(
                JuiceTween.IE_ScalePunch(m_label.transform, m_labelBaseScale, m_labelPunch, m_labelPunchDuration));
        }
    }
}
