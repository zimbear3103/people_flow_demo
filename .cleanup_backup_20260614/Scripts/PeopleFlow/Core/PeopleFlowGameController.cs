using System;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// The hub of the People Flow module and the single integration point with the host
    /// game (GamePlayController). It owns level lifecycle, the live crowd count and the
    /// global crowd cap, and reports outcomes back to the host through the two static
    /// events below — the host never reaches into the module's internals.
    ///
    /// Contract used by GamePlayController.cs:
    ///   static event Action     LevelFailed;
    ///   static event Action&lt;int&gt; LevelCompleted;   // survivor count
    ///   static PeopleFlowGameController EnsureInstance();
    ///   BeginLevel(int) / PauseGame() / ResumeGame() / QuitLevel()
    ///   DebugWinLevel() / DebugFailLevel()
    ///
    /// NOTE: we deliberately do NOT declare OnDestroy here — the Singleton base uses a
    /// private OnDestroy to unregister itself, and overriding it would break that (see
    /// CLAUDE.md "Common Gotchas"). The static events are only ever *raised* here, so there
    /// is nothing for this controller to unsubscribe.
    /// </summary>
    public class PeopleFlowGameController : Singleton<PeopleFlowGameController>
    {
        // ---- Host-facing outcome events -------------------------------------------------
        public static event Action LevelFailed;
        public static event Action<int> LevelCompleted;
        // --------------------------------------------------------------------------------

        [Header("Scene references (auto-resolved if left empty)")]
        [SerializeField] private ObjectPooler m_pooler;
        [SerializeField] private FlowSpawner m_spawner;
        [SerializeField] private PlayerInput m_input;
        [SerializeField] private TargetPipeBase m_target;

        [Header("Tuning")]
        [Tooltip("Hard cap on simultaneously alive people, protecting memory/perf on mobile.")]
        [SerializeField] private int m_maxAlive = 800;

        [Tooltip("Demo convenience: auto-BeginLevel with this index on Start (set to 0 in the " +
                 "standalone Level1_demo scene). Leave at -1 in the real game so the host " +
                 "(GamePlayController) drives level flow.")]
        [SerializeField] private int m_autoStartLevel = -1;

        public int AliveCount { get; private set; }
        public int CollectedCount { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>True while a level is running and not paused (input/spawn gate).</summary>
        public bool CanPlay => IsPlaying && !IsPaused;
        public int MaxAlive => m_maxAlive;

        /// <summary>Static check used by PersonMovement to freeze on pause.</summary>
        public static bool Paused
        {
            get
            {
                PeopleFlowGameController c = Instance;
                return c != null && c.IsPaused;
            }
        }

        private int m_currentRequired;

        /// <summary>
        /// Returns the live instance, creating a host GameObject for it if none exists yet.
        /// Called by the host before BeginLevel.
        /// </summary>
        public static PeopleFlowGameController EnsureInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(PeopleFlowGameController));
                return go.AddComponent<PeopleFlowGameController>();
            }
            return Instance;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            // Standalone demo path: with no host present, kick the configured level so the
            // scene is immediately playable. In the real game this stays -1 and the host
            // (GamePlayController) calls BeginLevel itself.
            if (m_autoStartLevel >= 0)
                BeginLevel(m_autoStartLevel);
        }

        private void ResolveReferences()
        {
            // Instance getters do the FindAnyObjectByType lookup and cache the result.
            if (m_pooler == null) m_pooler = ObjectPooler.Instance;
            if (m_spawner == null) m_spawner = FindAnyObjectByType<FlowSpawner>();
            if (m_input == null) m_input = PlayerInput.Instance;
            if (m_target == null) m_target = FindAnyObjectByType<TargetPipeBase>();
        }

        // ---- Host API -------------------------------------------------------------------

        public void BeginLevel(int level)
        {
            ResolveReferences();

            PeopleFlowLevelConfig cfg = PeopleFlowSampleLevels.Get(level);
            m_currentRequired = cfg.RequiredCount;

            AliveCount = 0;
            CollectedCount = 0;
            IsPaused = false;
            IsPlaying = true;

            if (m_pooler != null)
                m_pooler.Initialize();
            if (m_target != null)
                m_target.Configure(cfg.RequiredCount);
            if (m_spawner != null)
            {
                m_spawner.SetSpawnRate(cfg.SpawnRate);
                m_spawner.ResetSpawner();
            }

            GameLog.Log(LogType.Log, $"[PeopleFlow] BeginLevel {level} (required={cfg.RequiredCount}, rate={cfg.SpawnRate}).");
        }

        public void PauseGame()
        {
            IsPaused = true;
        }

        public void ResumeGame()
        {
            IsPaused = false;
        }

        public void QuitLevel()
        {
            IsPlaying = false;
            IsPaused = false;
            if (m_pooler != null)
                m_pooler.ReturnAll("Person");
            AliveCount = 0;
            GameLog.Log(LogType.Log, "[PeopleFlow] QuitLevel.");
        }

        public void DebugWinLevel()
        {
            if (!IsPlaying)
                IsPlaying = true;
            int survivors = Mathf.Max(1, Mathf.Max(CollectedCount, m_currentRequired));
            ReportLevelComplete(survivors);
        }

        public void DebugFailLevel()
        {
            if (!IsPlaying)
                IsPlaying = true;
            ReportLevelFailed();
        }

        // ---- Crowd bookkeeping (called by spawner / gates / target) ---------------------

        public bool CanSpawnMore(int count = 1)
        {
            return IsPlaying && (AliveCount + count) <= m_maxAlive;
        }

        public void NotifyPersonSpawned()
        {
            AliveCount++;
        }

        public void NotifyPersonReachedTarget()
        {
            AliveCount = Mathf.Max(0, AliveCount - 1);
            CollectedCount++;
        }

        public void NotifyPersonDespawned()
        {
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }

        // ---- Outcome reporting ----------------------------------------------------------

        public void ReportLevelComplete(int survivors)
        {
            if (!IsPlaying)
                return;
            IsPlaying = false;
            GameLog.Log(LogType.Log, $"[PeopleFlow] LevelCompleted (survivors={survivors}).");
            LevelCompleted?.Invoke(survivors);
        }

        public void ReportLevelFailed()
        {
            if (!IsPlaying)
                return;
            IsPlaying = false;
            GameLog.Log(LogType.Log, "[PeopleFlow] LevelFailed.");
            LevelFailed?.Invoke();
        }
    }
}
