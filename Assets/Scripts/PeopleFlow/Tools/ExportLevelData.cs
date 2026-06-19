using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PeopleFlow
{
    /// <summary>
    /// Editor authoring tool — the inverse of <see cref="LevelManager.Build"/>. It reads the level
    /// laid out in this scene (the <see cref="RunwayTrack"/>, the <see cref="HoleFactory"/> objects
    /// and their hole bundles, any standalone <see cref="Hole"/> objects, and the <see cref="Lane"/>
    /// objects under a <see cref="LevelManager"/>) and writes it back out as a <see cref="LevelData"/>
    /// asset, capturing every object's transform into its <see cref="TransformSpec"/> placement
    /// (<c>overrideTransform = true</c>). A designer arranges the level visually, then bakes a
    /// LevelData with one click — Right-click the component ▸ "Export Level Data".
    ///
    /// The scene only holds what its components actually serialize: placements, the factory / lane /
    /// hole counts, and each hole's colour and required count. Data the scene cannot hold — the level
    /// rules (time limit, speeds, loop size, capacity, shape, arrows), each lane's character queue,
    /// and hole specials (hidden / frozen / gate) — is copied from an optional <see cref="m_template"/>
    /// LevelData by index, or left at sensible defaults.
    ///
    /// Mirrors the CapyPuzzle <c>ExportLevelData</c> pattern: a scene MonoBehaviour with an export
    /// action, guarded by <c>UNITY_EDITOR</c> so it strips cleanly from player builds.
    /// </summary>
    public class ExportLevelData : MonoBehaviour
    {
        [Header("Source — the scene layout to read")]
        [Tooltip("LevelManager whose RunwayTrack / factories / holes / lanes describe the level. " +
                 "Leave empty to use the first LevelManager found in the scene.")]
        [SerializeField] LevelManager m_source;

        [Header("Template — fills what the scene can't hold (optional)")]
        [Tooltip("Level rules (time limit, run speed, loop size, capacity, shape, arrows), each lane's " +
                 "character queue, and each hole's specials are copied from here, matched by index. " +
                 "Leave empty to use LevelData defaults.")]
        [SerializeField] LevelData m_template;

        [Header("Output")]
        [SerializeField] int m_levelNumber = 1;
        [Tooltip("Project-relative folder the .asset is written to; created if missing.")]
        [SerializeField] string m_outputFolder = "Assets/Resources_moved/LevelData";
        [Tooltip("Asset file name without extension. '{n}' is replaced with the level number.")]
        [SerializeField] string m_fileName = "Level_{n}";

        [SerializeField] GameObject m_trackLinePrefab;
        // ---- entry point (inspector context-menu) ---------------------------
        private void Start()
        {
            ExportLevel();
        }
        [ContextMenu("Export Level Data")]
        public void ExportLevel()
        {
#if UNITY_EDITOR
            var source = m_source != null ? m_source : FindAnyObjectByType<LevelManager>();
            if (source == null)
            {
                Debug.LogError("[ExportLevelData] No LevelManager assigned or found in the scene — nothing to export.");
                return;
            }

            LevelData data = BuildLevelData(source);
            SaveAsset(data);
#else
            Debug.LogWarning("[ExportLevelData] Exporting a level is editor-only.");
#endif
        }

#if UNITY_EDITOR
        // ---- build the LevelData from the scene -----------------------------

        LevelData BuildLevelData(LevelManager source)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();

            // Level rules + arrows come from the template (or LevelData's own defaults if none).
            CopyRules(m_template, data);
            data.levelNumber = m_levelNumber;

            // Track: pin the loop wherever the RunwayTrack sits in the scene.
            var track = source.GetComponentInChildren<RunwayTrack>(true);
            data.trackPlacement = track != null ? CaptureWorld(track.transform) : new TransformSpec();
            data.trackLinePrefab = m_trackLinePrefab;
            // Factories (each with its hole bundle) + any standalone holes.
            data.holeFactories = new List<HoleFactorySetup>();
            data.holes = new List<HoleSetup>();

            var factories = source.GetComponentsInChildren<HoleFactory>(true); // hierarchy order: Factory_0, _1, …
            for (int i = 0; i < factories.Length; i++)
                data.holeFactories.Add(ExportFactory(factories[i], i));

            // Lanes: placement from the scene; queue / group size / barrier from the template by index.
            data.lanes = new List<LaneSetup>();
            var lanes = source.GetComponentsInChildren<Lane>(true);
            for (int i = 0; i < lanes.Length; i++)
                data.lanes.Add(ExportLane(lanes[i], i));

            WarnAboutGaps(data);
            return data;
        }

        HoleFactorySetup ExportFactory(HoleFactory factory, int index)
        {
            var setup = new HoleFactorySetup
            {
                placement = CaptureWorld(factory.transform),
                bundle = new List<HoleSetup>(),
            };

            // The bundle is the Hole component(s) parented under the factory (its HoleSpawnPos holder),
            // in hierarchy order. Each contributes its colour + required count; the factory position is
            // used for every hole it produces, so a bundle entry's own placement is irrelevant.
            HoleFactorySetup templateFactory = TemplateFactory(index);
            var bundleHoles = factory.Bundle;
            setup.bundle = bundleHoles != null ? new List<HoleSetup>(bundleHoles) : new List<HoleSetup>();
            if (setup.bundle.Count == 0)
                Debug.LogWarning($"[ExportLevelData] Factory '{factory.name}' has no Hole children — it " +
                                 "will export an empty bundle and won't produce anything. Add a Hole under it.");
            return setup;
        }

        LaneSetup ExportLane(Lane lane, int index)
        {
            var setup = new LaneSetup { placement = CaptureWorld(lane.transform) };

            // The waiting queue (characters), group size and barrier aren't authored on the Lane
            // component — pull them from the matching template lane when one is supplied.
            LaneSetup template = TemplateLane(index);
            if (template != null)
            {
                setup.characters = new List<PeopleColor>(template.characters);
                setup.groupSize = template.groupSize;
                setup.barrier = template.barrier;
                setup.unlockAfterHolesCompleted = template.unlockAfterHolesCompleted;
            }
            return setup;
        }

        // ---- transform capture ----------------------------------------------

        /// <summary>Capture a transform as a pinned <see cref="TransformSpec"/>. The build consumes
        /// <c>placement.position</c> / <c>.Rotation</c> as WORLD and <c>.scale</c> as local scale
        /// (see <see cref="LevelManager"/>.SpawnFactory / BuildLanes and
        /// <see cref="TransformSpec.ApplyWorld"/>), so we store world position + world euler + local
        /// scale, with the override on.</summary>
        static TransformSpec CaptureWorld(Transform t) => new TransformSpec
        {
            overrideTransform = true,
            position = t.position,
            rotationEuler = t.rotation.eulerAngles,
            scale = t.localScale,
        };

        // ---- template lookups (by index) ------------------------------------

        static void CopyRules(LevelData from, LevelData to)
        {
            if (from == null) return;
            to.timeLimit = from.timeLimit;
            to.runwayCapacity = from.runwayCapacity;
            to.runSpeed = from.runSpeed;
            to.trackShape = from.trackShape;
            to.loopWidth = from.loopWidth;
            to.loopHeight = from.loopHeight;
            to.cornerRadius = from.cornerRadius;
            to.customWaypoints = from.customWaypoints != null ? new List<Vector3>(from.customWaypoints) : new List<Vector3>();
            to.roadVisual = from.roadVisual;
            to.arrows = from.arrows != null ? new List<ArrowSetup>(from.arrows) : new List<ArrowSetup>();
        }

        HoleFactorySetup TemplateFactory(int index)
            => m_template != null && m_template.holeFactories != null
               && index >= 0 && index < m_template.holeFactories.Count
                ? m_template.holeFactories[index] : null;

        static HoleSetup TemplateBundleEntry(HoleFactorySetup factory, int index)
            => factory != null && factory.bundle != null && index >= 0 && index < factory.bundle.Count
                ? factory.bundle[index] : null;

        HoleSetup TemplateStandaloneHole(int index)
            => m_template != null && m_template.holes != null
               && index >= 0 && index < m_template.holes.Count
                ? m_template.holes[index] : null;

        LaneSetup TemplateLane(int index)
            => m_template != null && m_template.lanes != null
               && index >= 0 && index < m_template.lanes.Count
                ? m_template.lanes[index] : null;

        // ---- diagnostics ----------------------------------------------------

        void WarnAboutGaps(LevelData data)
        {
            if (m_template == null)
                Debug.LogWarning("[ExportLevelData] No template assigned: level rules (time limit, run " +
                    "speed, loop size, capacity, shape, arrows) use defaults — set them on the exported " +
                    "asset, or assign a template.");

            // Lanes left without an authored queue are fine: LevelManager fills every empty lane from
            // the holes at build time (supply == demand). The genuine blocker is having no holes — then
            // there is nothing to fill, and no supply is dealt to any lane.
            if (data.TotalHoles == 0)
                Debug.LogWarning("[ExportLevelData] Exported level has 0 holes — add Hole objects under the " +
                    "factories (or as standalone holes) before exporting; with no holes the lanes stay empty.");
        }

        // ---- save -----------------------------------------------------------

        void SaveAsset(LevelData data)
        {
            if (!System.IO.Directory.Exists(m_outputFolder))
                System.IO.Directory.CreateDirectory(m_outputFolder);

            string file = m_fileName.Replace("{n}", m_levelNumber.ToString());
            string path = $"{m_outputFolder}/{file}.asset";

            // If an asset already exists at this path, copy the freshly built values into it (keeping
            // its GUID so existing references survive); otherwise create a new asset.
            var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(data, existing);
                DestroyImmediate(data); // the temporary instance is no longer needed
                EditorUtility.SetDirty(existing);
                data = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(data, path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = data;
            Debug.Log($"[ExportLevelData] {(existing != null ? "Updated" : "Wrote")} {path} — " +
                      $"{data.TotalHoles} holes, {data.holeFactories.Count} factories, {data.lanes.Count} lanes.");
        }
#endif
    }
}
