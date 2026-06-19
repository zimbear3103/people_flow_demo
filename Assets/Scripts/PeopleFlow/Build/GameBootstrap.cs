using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// One-component entry point for the Game scene. Drop this on a single empty GameObject, assign
    /// the level prefabs, and press Play: it creates the camera, light, all managers, the UI, and
    /// builds the level by instantiating the assigned Hole / Lane / Character prefabs — no manual
    /// scene wiring required. You can still place configured managers / a LevelData asset yourself
    /// and this will use them instead.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Level source")]
        [Tooltip("If assigned, this level is used. Otherwise a built-in sample level is loaded " +
                 "by GameSession.CurrentLevelIndex (set by the menu).")]
        [SerializeField] LevelData m_overrideLevel;
        [Tooltip("If >= 0, forces this built-in sample index (handy for testing in the Editor).")]
        [SerializeField] int m_forceLevelIndex = -1;

        [Header("Level prefabs (required)")]
        [Tooltip("Drag in the Hole, Lane (WaitingArea) and Character (Minion) prefabs. Forwarded to " +
                 "the LevelManager, which instantiates them to build the level.")]
        [SerializeField] LevelPrefabs m_prefabs = new LevelPrefabs();

        [Tooltip("Optional PeopleColor → Material overrides, forwarded to the LevelManager.")]
        [SerializeField] ColorMaterialSet m_colorMaterials = new ColorMaterialSet();

        [Header("Camera")]
        [SerializeField] float m_cameraPitch = 52f;
        [SerializeField] float m_fieldOfView = 55f;
        [SerializeField] Color m_background = new Color(0.83f, 0.90f, 0.98f);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        void Start()
        {
            var level = ResolveLevel();

            EnsureLight();
            var cam = EnsureCamera(level);

            // Order matters only in that managers must exist before LevelManager.Build runs;
            // AddComponent triggers each Awake synchronously, so singletons are ready immediately.
            // The referee is now GamePlayController; create one in standalone mode (no host shell) so
            // the gameplay scripts that gate on it can run this demo scene on their own.
            EnsureStandaloneController();
            Ensure<AudioManager>("AudioManager");
            Ensure<UIManager>("UIManager");
            var input = Ensure<InputManager>("InputManager");
            var timer = Ensure<Timer>("Timer");
            var levelManager = Ensure<LevelManager>("LevelManager");
            levelManager.ConfigurePrefabs(m_prefabs);
            levelManager.ConfigureMaterials(m_colorMaterials);

            levelManager.Build(level, input, timer);

            PositionCamera(cam, level);
        }

        LevelData ResolveLevel()
        {
            if (m_overrideLevel != null) return m_overrideLevel;
            int index = m_forceLevelIndex >= 0 ? m_forceLevelIndex : GameSession.CurrentLevelIndex;
            return DefaultLevels.Get(index);
        }

        static T Ensure<T>(string name) where T : Component
        {
            var existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;
            return new GameObject(name).AddComponent<T>();
        }

        static GamePlayController EnsureStandaloneController()
        {
            var gpc = GamePlayController.Instance;
            if (gpc == null)
                gpc = new GameObject("GamePlayController").AddComponent<GamePlayController>();
            gpc.ConfigureAsStandalone();
            return gpc;
        }

        Camera EnsureCamera(LevelData level)
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
            return cam;
        }

        void PositionCamera(Camera cam, LevelData level)
        {
            // Angled top-down: aim slightly below loop centre so the lanes at the bottom are framed.
            Vector3 target = new Vector3(0f, 0f, -level.loopHeight * 0.12f);
            float dist = level.loopHeight * 1.35f + 6f;
            Quaternion rot = Quaternion.Euler(m_cameraPitch, 0f, 0f);
            Vector3 pos = target - (rot * Vector3.forward) * dist;
            cam.transform.SetPositionAndRotation(pos, rot);
        }

        void EnsureLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null) return;
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
