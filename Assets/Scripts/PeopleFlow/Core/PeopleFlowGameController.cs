using System;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Integration bridge between the host game state machine (global GamePlayController) and the
    /// PeopleFlow loop-puzzle module. The host owns level flow + its own UI/popups and calls into
    /// this controller; the module reports outcomes back through the static <see cref="LevelFailed"/>
    /// and <see cref="LevelCompleted"/> events (the contract documented in CLAUDE.md).
    ///
    /// It builds the gameplay (camera, light, managers, runway, holes, lanes) but deliberately does
    /// NOT spawn the module's own <see cref="UIManager"/>, so the host's HUD/popups stay in charge.
    /// </summary>
    public class PeopleFlowGameController : MonoBehaviour
    {
        /// <summary>Fired when the crowd loses (runway jam or time-out).</summary>
        public static event Action LevelFailed;

        /// <summary>Fired on win, with the number of people that made it into holes.</summary>
        public static event Action<int> LevelCompleted;

        public static PeopleFlowGameController Instance { get; private set; }

        [SerializeField] float m_cameraPitch = 52f;
        [SerializeField] float m_fieldOfView = 55f;
        [SerializeField] Color m_background = new Color(0.83f, 0.90f, 0.98f);

        GameObject m_root;     // container for managers + gameplay (one-call teardown)
        LevelData m_level;
        bool m_subscribed;

        public static PeopleFlowGameController EnsureInstance()
        {
            if (Instance == null)
                Instance = new GameObject("PeopleFlowGameController").AddComponent<PeopleFlowGameController>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        // ---- host-facing API ------------------------------------------------

        public void BeginLevel(int level)
        {
            Teardown();

            // Host levels are 1-based; sample levels are 0-based.
            int index = Mathf.Clamp(level - 1, 0, PeopleFlowSampleLevels.Count - 1);
            GameSession.CurrentLevelIndex = index;
            m_level = PeopleFlowSampleLevels.Get(index);

            m_root = new GameObject("PeopleFlow_Gameplay");

            BuildCameraAndLight(m_level);

            // All managers live on the container so one Destroy cleans everything up.
            m_root.AddComponent<GameManager>();
            m_root.AddComponent<AudioManager>();
            var input = m_root.AddComponent<InputManager>();
            var timer = m_root.AddComponent<Timer>();
            var levelManager = m_root.AddComponent<LevelManager>();

            Subscribe();
            levelManager.Build(m_level, new MaterialLibrary(), input, timer);
        }

        public void PauseGame() => GameManager.Instance?.Pause();
        public void ResumeGame() => GameManager.Instance?.Resume();
        public void QuitLevel() => Teardown();

        public void DebugWinLevel() => GameManager.Instance?.ForceWin();
        public void DebugFailLevel() => GameManager.Instance?.ForceLose();

        // ---- module → host event relay -------------------------------------

        void Subscribe()
        {
            if (m_subscribed || GameManager.Instance == null) return;
            GameManager.Instance.OnLevelWin += HandleWin;
            GameManager.Instance.OnLevelLose += HandleLose;
            m_subscribed = true;
        }

        void Unsubscribe()
        {
            if (!m_subscribed || GameManager.Instance == null) { m_subscribed = false; return; }
            GameManager.Instance.OnLevelWin -= HandleWin;
            GameManager.Instance.OnLevelLose -= HandleLose;
            m_subscribed = false;
        }

        void HandleWin() => LevelCompleted?.Invoke(SurvivorCount());
        void HandleLose(LoseReason reason) => LevelFailed?.Invoke();

        /// <summary>People that reached holes on a win = the total required across all holes.</summary>
        int SurvivorCount()
        {
            int n = 0;
            if (m_level?.holes != null)
                foreach (var h in m_level.holes) n += Mathf.Max(0, h.requiredCount);
            return n;
        }

        void Teardown()
        {
            Unsubscribe();
            Time.timeScale = 1f;
            if (m_root != null)
            {
                // DestroyImmediate so the old singletons are gone before BeginLevel rebuilds them
                // in the same frame (Destroy is deferred and would clash with the new instances).
                DestroyImmediate(m_root);
                m_root = null;
            }
        }

        // ---- scene scaffolding ---------------------------------------------

        void BuildCameraAndLight(LevelData level)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = m_background;
            cam.orthographic = false;
            cam.fieldOfView = m_fieldOfView;

            Vector3 target = new Vector3(0f, 0f, -level.loopHeight * 0.12f);
            float dist = level.loopHeight * 1.35f + 6f;
            Quaternion rot = Quaternion.Euler(m_cameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(target - (rot * Vector3.forward) * dist, rot);

            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                lightGo.transform.SetParent(m_root.transform, true);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }
    }
}
