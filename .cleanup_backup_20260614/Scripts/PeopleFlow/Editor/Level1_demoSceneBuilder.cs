using System.IO;
using PeopleFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PeopleFlow.EditorTools
{
    /// <summary>
    /// One-click builder for the "Level1_demo" prototype scene. It is fully reproducible:
    /// it generates the Person / PopParticle prefabs, lays out a vertical pipe with two math
    /// gates and a target container, wires the systems (pooler / input / spawner / controller)
    /// and saves Assets/Scenes/Level1_demo.unity.
    ///
    /// Run it via the menu  Tools ▸ PeopleFlow ▸ Build Level1_demo Scene
    /// or headless:
    ///   Unity.exe -batchmode -quit -projectPath . \
    ///     -executeMethod PeopleFlow.EditorTools.Level1_demoSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class Level1_demoSceneBuilder
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/Level1_demo.unity";
        private const string PrefabFolder = "Assets/Prefabs/Ingame/PeopleFlow";
        private const string SortingLayer = "Gameplay";

        // Pipe geometry (world space, portrait camera).
        private const float InnerLeftX = -1.4f;
        private const float InnerRightX = 1.4f;
        private const float WallHalfWidth = 0.2f;
        private const float PipeTopY = 5.2f;
        private const float PipeBottomY = -5.2f;

        [MenuItem("Tools/PeopleFlow/Build Level1_demo Scene")]
        public static void BuildMenu()
        {
            BuildScene();
            EditorUtility.DisplayDialog("People Flow", "Level1_demo scene built and saved at\n" + ScenePath, "OK");
        }

        /// <summary>Entry point for headless/CI invocation.</summary>
        public static void BuildFromCommandLine()
        {
            BuildScene();
        }

        public static void BuildScene()
        {
            EnsureFolder(SceneFolder);
            EnsureFolder("Assets/Prefabs/Ingame");
            EnsureFolder(PrefabFolder);

            // Generate the pooled prefabs first so the pooler can reference them.
            GameObject personPrefab = BuildPersonPrefab();
            GameObject popPrefab = BuildPopParticlePrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildLighting();

            // ---- Systems --------------------------------------------------------------
            var root = new GameObject("[PeopleFlow]");

            var systems = NewChild("Systems", root.transform);
            ObjectPooler pooler = systems.AddComponent<ObjectPooler>();
            PlayerInput input = systems.AddComponent<PlayerInput>();
            PeopleFlowGameController controller = systems.AddComponent<PeopleFlowGameController>();

            ConfigurePooler(pooler, systems.transform, personPrefab, popPrefab);

            // ---- Pipe -----------------------------------------------------------------
            var pipe = NewChild("Pipe", root.transform);
            PipeChannel channel = pipe.AddComponent<PipeChannel>();
            new SO(channel).Float("m_innerLeftX", InnerLeftX).Float("m_innerRightX", InnerRightX).Apply();

            BuildWall("WallLeft", pipe.transform, InnerLeftX - WallHalfWidth);
            BuildWall("WallRight", pipe.transform, InnerRightX + WallHalfWidth);

            FlowSpawner spawner = BuildSpawner(pipe.transform);
            BuildGate(pipe.transform, "Gate_x2", new Vector3(0f, 2.4f, 0f), MathGate.GateOperation.Multiply, 2, new Color(0.2f, 0.8f, 1f));
            BuildGate(pipe.transform, "Gate_add5", new Vector3(0f, -0.8f, 0f), MathGate.GateOperation.Add, 5, new Color(1f, 0.55f, 0.2f));

            // ---- UI -------------------------------------------------------------------
            TMP_Text counterText = BuildUI();

            // ---- Target ---------------------------------------------------------------
            TargetPipeBase target = BuildTarget(pipe.transform, counterText, popPrefab != null);

            // ---- Wire the controller --------------------------------------------------
            new SO(controller)
                .Ref("m_pooler", pooler)
                .Ref("m_spawner", spawner)
                .Ref("m_input", input)
                .Ref("m_target", target)
                .Int("m_maxAlive", 800)
                .Int("m_autoStartLevel", 0)   // makes the standalone demo immediately playable
                .Apply();

            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Level1_demoSceneBuilder] Built and saved " + ScenePath);
        }

        // ----------------------------------------------------------------------------------
        //  Camera / lighting
        // ----------------------------------------------------------------------------------
        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
        }

        private static void BuildLighting()
        {
            // 2D sprites are unlit; nothing required. Placeholder kept for clarity/extension.
        }

        // ----------------------------------------------------------------------------------
        //  Prefabs
        // ----------------------------------------------------------------------------------
        private static GameObject BuildPersonPrefab()
        {
            var go = new GameObject("Person");
            go.tag = "Person";
            go.transform.localScale = Vector3.one * 0.5f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSprite();
            sr.color = new Color(1f, 0.78f, 0.25f);
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.32f;          // matches the built-in Knob sprite radius

            go.AddComponent<Rigidbody2D>();        // PersonMovement configures bodyType in Awake
            go.AddComponent<PersonMovement>();      // serialized defaults are correct

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/Person.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildPopParticlePrefab()
        {
            var go = new GameObject("PopParticle");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 3f;
            main.startSize = 0.18f;
            main.maxParticles = 24;
            main.playOnAwake = true;
            main.startColor = new Color(1f, 0.9f, 0.35f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            // Persisted material so the prefab keeps a valid reference after reload.
            var mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, PrefabFolder + "/PopParticle.mat");

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = mat;
            psr.sortingLayerName = SortingLayer;
            psr.sortingOrder = 10;

            go.AddComponent<AutoReturnToPool>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/PopParticle.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ----------------------------------------------------------------------------------
        //  Systems
        // ----------------------------------------------------------------------------------
        private static void ConfigurePooler(ObjectPooler pooler, Transform poolRoot, GameObject personPrefab, GameObject popPrefab)
        {
            var so = new SerializedObject(pooler);

            SerializedProperty list = so.FindProperty("m_pools");
            list.ClearArray();

            AddPool(list, 0, "Person", personPrefab, 250, true);
            if (popPrefab != null)
                AddPool(list, 1, "PopParticle", popPrefab, 30, true);

            SerializedProperty poolRootProp = so.FindProperty("m_poolRoot");
            if (poolRootProp != null)
                poolRootProp.objectReferenceValue = poolRoot;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddPool(SerializedProperty list, int index, string tag, GameObject prefab, int size, bool expandable)
        {
            if (list.arraySize <= index)
                list.arraySize = index + 1;

            SerializedProperty el = list.GetArrayElementAtIndex(index);
            el.FindPropertyRelative("Tag").stringValue = tag;
            el.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            el.FindPropertyRelative("Size").intValue = size;
            el.FindPropertyRelative("Expandable").boolValue = expandable;
        }

        // ----------------------------------------------------------------------------------
        //  Pipe pieces
        // ----------------------------------------------------------------------------------
        private static void BuildWall(string name, Transform parent, float centerX)
        {
            float height = PipeTopY - PipeBottomY;
            float centerY = (PipeTopY + PipeBottomY) * 0.5f;

            var go = NewChild(name, parent);
            go.transform.position = new Vector3(centerX, centerY, 0f);
            go.transform.localScale = new Vector3(WallHalfWidth * 2f, height, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite();
            sr.color = new Color(0.22f, 0.25f, 0.35f);
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = 1;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;     // scaled by the transform into a tall thin wall
        }

        private static FlowSpawner BuildSpawner(Transform parent)
        {
            var nozzle = NewChild("Nozzle", parent);
            nozzle.transform.position = new Vector3(0f, PipeTopY - 0.6f, 0f);

            var visual = NewChild("Visual", nozzle.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(InnerRightX - InnerLeftX, 0.5f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite();
            sr.color = new Color(0.35f, 0.7f, 0.95f);
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = 2;

            FlowSpawner spawner = nozzle.AddComponent<FlowSpawner>();
            new SO(spawner)
                .Str("m_personTag", "Person")
                .Float("m_spawnRate", 10f)
                .Ref("m_nozzle", nozzle.transform)
                .Vec2("m_launchVelocity", new Vector2(0f, 3.5f))
                .Float("m_launchJitter", 0.9f)
                .Ref("m_nozzleVisual", visual.transform)
                .Apply();

            return spawner;
        }

        private static MathGate BuildGate(Transform parent, string name, Vector3 pos, MathGate.GateOperation op, int value, Color color)
        {
            var go = NewChild(name, parent);
            go.transform.position = pos;

            // Visual bar across the pipe.
            var bar = NewChild("Bar", go.transform);
            bar.transform.localPosition = Vector3.zero;
            bar.transform.localScale = new Vector3(InnerRightX - InnerLeftX, 0.5f, 1f);
            var barSr = bar.AddComponent<SpriteRenderer>();
            barSr.sprite = SquareSprite();
            barSr.color = new Color(color.r, color.g, color.b, 0.45f);
            barSr.sortingLayerName = SortingLayer;
            barSr.sortingOrder = 3;

            // Trigger collider spanning the channel.
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(InnerRightX - InnerLeftX, 0.55f);

            // World-space label. 3D TextMeshPro needs a RectTransform, which AddComponent
            // does NOT create on its own, so add it explicitly before the text component.
            var labelGo = NewChild("Label", go.transform);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(InnerRightX - InnerLeftX, 1f);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 6f;
            label.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;
            // Draw the label in front of the gate bar (TextMeshPro adds a MeshRenderer).
            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingLayerName = SortingLayer;
                labelRenderer.sortingOrder = 4;
            }

            MathGate gate = go.AddComponent<MathGate>();
            new SO(gate)
                .Enum("m_operation", (int)op)
                .Int("m_value", value)
                .Str("m_personTag", "Person")
                .Int("m_maxSpawnsPerFrame", 12)
                .Ref("m_label", label)
                .Apply();

            return gate;
        }

        private static TargetPipeBase BuildTarget(Transform parent, TMP_Text counterText, bool hasPop)
        {
            var go = NewChild("Target", parent);
            go.transform.position = new Vector3(0f, PipeBottomY + 0.6f, 0f);

            // Trigger that collects people.
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(InnerRightX - InnerLeftX, 1.0f);

            // Container visual that bounces.
            var visual = NewChild("ContainerVisual", go.transform);
            visual.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            visual.transform.localScale = new Vector3(InnerRightX - InnerLeftX + 0.3f, 1.0f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite();
            sr.color = new Color(0.15f, 0.6f, 0.35f);
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = 2;

            TargetPipeBase target = go.AddComponent<TargetPipeBase>();
            var so = new SO(target)
                .Int("m_requiredCount", 40)
                .Str("m_personTag", "Person")
                .Ref("m_counterText", counterText)
                .Str("m_counterFormat", "{0}")
                .Ref("m_containerVisual", visual.transform);
            so.Str("m_popParticleTag", hasPop ? "PopParticle" : string.Empty);
            so.Apply();

            return target;
        }

        // ----------------------------------------------------------------------------------
        //  UI
        // ----------------------------------------------------------------------------------
        private static TMP_Text BuildUI()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Remaining-count text (top center).
            var countGo = NewChild("CountText", canvasGo.transform);
            var countRt = countGo.AddComponent<RectTransform>();   // ensure RectTransform before the Graphic
            var count = countGo.AddComponent<TextMeshProUGUI>();
            count.text = "40";
            count.fontSize = 110f;
            count.alignment = TextAlignmentOptions.Center;
            count.color = Color.white;
            countRt.anchorMin = new Vector2(0.5f, 1f);
            countRt.anchorMax = new Vector2(0.5f, 1f);
            countRt.pivot = new Vector2(0.5f, 1f);
            countRt.anchoredPosition = new Vector2(0f, -180f);
            countRt.sizeDelta = new Vector2(600f, 200f);

            // Hint text (bottom center).
            var hintGo = NewChild("HintText", canvasGo.transform);
            var hintRt = hintGo.AddComponent<RectTransform>();
            var hint = hintGo.AddComponent<TextMeshProUGUI>();
            hint.text = "Tap & Hold to release the crowd";
            hint.fontSize = 48f;
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 160f);
            hintRt.sizeDelta = new Vector2(900f, 120f);

            return count;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ----------------------------------------------------------------------------------
        //  Helpers
        // ----------------------------------------------------------------------------------
        private static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite CircleSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        private static Sprite SquareSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Tiny fluent wrapper around SerializedObject for wiring private [SerializeField]s.</summary>
        private class SO
        {
            private readonly SerializedObject m_so;

            public SO(Object target)
            {
                m_so = new SerializedObject(target);
            }

            private SerializedProperty Find(string prop)
            {
                SerializedProperty p = m_so.FindProperty(prop);
                if (p == null)
                    Debug.LogError($"[Level1_demoSceneBuilder] Property '{prop}' not found on {m_so.targetObject}.");
                return p;
            }

            public SO Ref(string prop, Object value)
            {
                var p = Find(prop);
                if (p != null) p.objectReferenceValue = value;
                return this;
            }

            public SO Float(string prop, float value)
            {
                var p = Find(prop);
                if (p != null) p.floatValue = value;
                return this;
            }

            public SO Int(string prop, int value)
            {
                var p = Find(prop);
                if (p != null) p.intValue = value;
                return this;
            }

            public SO Str(string prop, string value)
            {
                var p = Find(prop);
                if (p != null) p.stringValue = value;
                return this;
            }

            public SO Enum(string prop, int value)
            {
                var p = Find(prop);
                if (p != null) p.enumValueIndex = value;
                return this;
            }

            public SO Vec2(string prop, Vector2 value)
            {
                var p = Find(prop);
                if (p != null) p.vector2Value = value;
                return this;
            }

            public void Apply()
            {
                m_so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
