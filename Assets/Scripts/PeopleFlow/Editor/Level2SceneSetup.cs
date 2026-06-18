#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PeopleFlow.EditorTools
{
    /// <summary>
    /// One-click wiring of a representative "Level 2" map (People-Loop-style early level) in the
    /// active scene: finds (or creates) the <see cref="GameBootstrap"/>, assigns the Ingame prefabs
    /// — Road (runway), Hole, Factory (hole bundle), WaitingArea (lanes), Minion (runners) — into
    /// its <c>LevelPrefabs</c>, and forces the built-in Level 2 (one factory producing Red→Green→Blue
    /// ×3 in turn, 3 auto-dealt lanes). No manual dragging required: open the Game scene, run the
    /// menu, press Play.
    /// </summary>
    public static class Level2SceneSetup
    {
        const string PrefabDir = "Assets/Prefabs/Ingame/";
        const int Level2Index = 1; // DefaultLevels is 0-based; index 1 == Level 2.

        [MenuItem("PeopleFlow/Setup Level 2 Map")]
        public static void Setup()
        {
            var hole = Load("Hole");
            var factory = Load("Factory"); // visual housing for the hole factory
            var lane = Load("WaitingArea");
            var character = Load("Minion");
            var road = Load("Road"); // optional — runway falls back to a procedural line if missing
            if (hole == null || lane == null || character == null)
            {
                Debug.LogError($"[PeopleFlow] Could not find Hole/WaitingArea/Minion under {PrefabDir} — aborting.");
                return;
            }

            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
                Debug.Log("[PeopleFlow] No GameBootstrap found — created one in the active scene.");
            }

            var so = new SerializedObject(bootstrap);
            SetObject(so.FindProperty("m_prefabs.hole"), hole);
            SetObject(so.FindProperty("m_prefabs.factorie"), factory);
            SetObject(so.FindProperty("m_prefabs.lane"), lane);
            SetObject(so.FindProperty("m_prefabs.character"), character);
            SetObject(so.FindProperty("m_prefabs.road"), road);

            // Force the built-in Level 2, and clear any override asset so the forced index wins.
            var forceIdx = so.FindProperty("m_forceLevelIndex");
            if (forceIdx != null) forceIdx.intValue = Level2Index;
            SetObject(so.FindProperty("m_overrideLevel"), null);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);

            Selection.activeGameObject = bootstrap.gameObject;
            Debug.Log("[PeopleFlow] Level 2 map wired on GameBootstrap " +
                      "(Road / Hole / Factory / WaitingArea / Minion assigned, forced level 2). Save the scene and press Play.");
        }

        static GameObject Load(string prefabName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + prefabName + ".prefab");

        static void SetObject(SerializedProperty prop, Object value)
        {
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
#endif
