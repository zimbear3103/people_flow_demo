using System;
using TMPro;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// The container at the bottom of the pipe (the win condition). Every "Person" that
    /// enters the trigger is collected: returned to the pool, the remaining count ticks
    /// down, the UI updates and the container bounces. When the count hits zero the level
    /// is complete.
    /// </summary>
    public class TargetPipeBase : MonoBehaviour
    {
        [Header("Win condition")]
        [Tooltip("How many people must be collected to complete the level.")]
        [SerializeField] private int m_requiredCount = 50;
        [SerializeField] private string m_personTag = "Person";

        [Header("UI")]
        [Tooltip("Text element showing the remaining count.")]
        [SerializeField] private TMP_Text m_counterText;
        [SerializeField] private string m_counterFormat = "{0}";

        [Header("Feedback")]
        [Tooltip("Visual that dips down when a person is caught (sense of weight).")]
        [SerializeField] private Transform m_containerVisual;
        [SerializeField] private float m_catchDip = 0.12f;
        [SerializeField] private float m_catchDipDuration = 0.12f;
        [Tooltip("Optional pooled particle tag spawned at the catch point.")]
        [SerializeField] private string m_popParticleTag = "PopParticle";

        /// <summary>Fired once when the required count reaches zero.</summary>
        public static event Action LevelComplete;

        public int RequiredCount => m_requiredCount;
        public int Collected => m_collected;

        private int m_collected;
        private bool m_completed;
        private Vector3 m_visualBaseLocalPos;
        private Coroutine m_bounce;

        private void Awake()
        {
            if (m_containerVisual != null)
                m_visualBaseLocalPos = m_containerVisual.localPosition;
            UpdateUI();
        }

        /// <summary>(Re)configures the target for a level. Called by the controller.</summary>
        public void Configure(int required)
        {
            m_requiredCount = Mathf.Max(0, required);
            m_collected = 0;
            m_completed = false;
            UpdateUI();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_completed)
                return;
            if (!other.CompareTag(m_personTag))
                return;

            // The collider lives on the person root (it carries the Rigidbody2D).
            GameObject person = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

            m_collected++;
            m_requiredCount = Mathf.Max(0, m_requiredCount - 1);

            PeopleFlowGameController ctrl = PeopleFlowGameController.Instance;
            if (ctrl != null)
                ctrl.NotifyPersonReachedTarget();

            SpawnPop(person.transform.position);

            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler != null)
                pooler.ReturnToPool(person);

            PlayCatchBounce();
            UpdateUI();

            if (m_requiredCount <= 0 && !m_completed)
            {
                m_completed = true;
                OnLevelComplete();
            }
        }

        private void OnLevelComplete()
        {
            GameLog.Log(LogType.Log, $"[TargetPipeBase] Level complete – collected {m_collected}.");

            // Local hook (as requested) ...
            LevelComplete?.Invoke();

            // ... and report up to the controller, which raises the host-facing event.
            PeopleFlowGameController ctrl = PeopleFlowGameController.Instance;
            if (ctrl != null)
                ctrl.ReportLevelComplete(m_collected);
        }

        private void SpawnPop(Vector3 position)
        {
            if (string.IsNullOrEmpty(m_popParticleTag))
                return;
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null)
                return;
            // Returns itself to the pool via AutoReturnToPool.
            pooler.SpawnFromPool(m_popParticleTag, position, Quaternion.identity);
        }

        private void PlayCatchBounce()
        {
            if (m_containerVisual == null)
                return;

            if (m_bounce != null)
                StopCoroutine(m_bounce);

            // DOTween equivalent: m_containerVisual.DOPunchPosition(Vector3.down * m_catchDip, m_catchDipDuration);
            m_bounce = StartCoroutine(
                JuiceTween.IE_DipBounce(m_containerVisual, m_visualBaseLocalPos, m_catchDip, m_catchDipDuration));
        }

        private void UpdateUI()
        {
            if (m_counterText != null)
                m_counterText.text = string.Format(m_counterFormat, m_requiredCount);
        }
    }
}
