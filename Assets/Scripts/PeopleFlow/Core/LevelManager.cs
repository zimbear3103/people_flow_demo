using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Builds one level's gameplay objects from <see cref="LevelData"/>: the runway track + its
    /// visual, the holes placed around the loop, and the lanes with their populated queues. Then
    /// wires input/timer/UI and tells <see cref="GamePlayController"/> to start.
    /// </summary>
    public class LevelManager : Singleton<LevelManager>
    {
        [Header("Level prefabs (required — drag in your Hole, Lane and Character prefabs)")]
        [Tooltip("Instantiated to build the level. Holes, lanes and runners come from these prefabs " +
                 "instead of being spawned from primitives in code. An entry component " +
                 "(GameBootstrap / GamePlayController) may also supply or override them.")]
        [SerializeField] LevelPrefabs m_prefabs = new LevelPrefabs();
        [SerializeField] Transform m_factoriesRoot;
        [SerializeField] Transform m_lanesRoot;

        [SerializeField] private RunwayTrack Track;
        [SerializeField] private Transform m_roadRoots;

        /// <summary>The runway track the current level was built on (HUD / queries). Null before Build.</summary>
        public RunwayTrack ActiveTrack => Track;

        [Header("Colour materials")]
        [Tooltip("PeopleColor → Material. A colour with a material here is skinned with it; any colour " +
                 "left empty uses a generated material. May also be supplied by the entry component.")]
        [SerializeField] ColorMaterialSet m_colorMaterials = new ColorMaterialSet();

        [Header("Road tiling (optional)")]
        [Tooltip("How many copies of the road prefab to tile around the loop. 0 = auto, from the road " +
                 "tile's own length. Raise it for a smoother curve, lower it for chunkier segments.")]
        [SerializeField] int m_roadSegments = 0;

        public readonly List<Lane> Lanes = new List<Lane>();
        Transform m_runnersRoot;

        /// <summary>
        /// Supply / override the level prefabs before <see cref="Build"/> runs. Used by the entry
        /// components, which create the LevelManager at runtime (so its own inspector slots are
        /// empty). Only non-null prefabs override what is already assigned.
        /// </summary>
        public void ConfigurePrefabs(LevelPrefabs prefabs) => m_prefabs.OverrideWith(prefabs);

        /// <summary>Supply / override the colour→material map before <see cref="Build"/> runs.</summary>
        public void ConfigureMaterials(ColorMaterialSet materials) => m_colorMaterials.OverrideWith(materials);

        public void Build(LevelData level, InputManager input, Timer timer)
        {
            if (!m_prefabs.IsComplete)
            {
                GameLog.Log(LogType.Error, $"LevelManager: assign the {m_prefabs.MissingList()} prefab(s) before " +
                            "building a level (drag them onto the LevelManager, GameBootstrap, or " +
                            "GamePlayController). Aborting level build.");
                return;
            }

            //Clear();

            var mats = new MaterialLibrary(m_colorMaterials.BuildMap());

            // 1. Build Track Math and Visuals together to handle dependencies cleanly
            BuildRunwayTrackAndVisual(level);

            // 2. Build the rest of the elements
            BuildFactories(level, mats);
            BuildLanes(level, mats);

            input.Bind(Lanes, Camera.main);
            timer.Begin(level.timeLimit);

            if (GamePlayController.Instance != null)
                GamePlayController.Instance.BeginLevel(level.TotalHoles);
            else
                GameLog.Log(LogType.Error, "No GamePlayController (the referee) in the scene — the level was built " +
                            "but won't start. Add a GamePlayController before building.");

            int singleHoleFactories = level.holes != null ? level.holes.Count : 0;
            int bundleFactories = level.holeFactories != null ? level.holeFactories.Count : 0;
            GameLog.Log(LogType.Log, $"Level {level.levelNumber} built: {level.TotalHoles} holes from " +
                       $"{singleHoleFactories + bundleFactories} factories " +
                       $"({singleHoleFactories} single-hole + {bundleFactories} bundle), " +
                       $"{Lanes.Count} lanes, capacity {level.runwayCapacity}.");
        }

        public void Clear()
        {
            DestroyAllChildren(m_roadRoots.transform);
            DestroyAllChildren(m_factoriesRoot);
            DestroyAllChildren(m_lanesRoot);
        }

        private void DestroyAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        // ---- runway visual & math logic -------------------------------------

        /// <summary>
        /// Handles the dependency between math and visuals.
        /// If manual LineRenderer -> Spawn visual first, Track copies it.
        /// If Tile/Math -> Track builds math first, Visual copies or tiles along it.
        /// </summary>
        void BuildRunwayTrackAndVisual(LevelData level)
        {
            LineRenderer visualTrack = null;
            bool useTiles = level.roadVisual == RoadVisual.RoadTiles && m_prefabs.road != null;

            // STEP 1: If using a LineRenderer, spawn it FIRST so RunwayTrack can read from it
            if (!useTiles && level.trackLinePrefab != null)
            {
                var trackObj = Instantiate(level.trackLinePrefab, m_roadRoots);
                trackObj.name = "TrackLine_Visual";
                trackObj.transform.localPosition = Vector3.zero;
                trackObj.transform.localRotation = Quaternion.identity;

                visualTrack = trackObj.GetComponent<LineRenderer>();
                if (visualTrack == null)
                    PFLog.Warn($"LevelManager: Prefab '{level.trackLinePrefab.name}' has no LineRenderer!");
            }

            // STEP 2: Build the math. 
            // It will intelligently use visualTrack if it has hand-drawn points, OR fallback to math.
            if (Track != null)
            {
                Track.Build(level, visualTrack);
            }

            // STEP 3: Complete visuals that require the math track to be fully built
            if (useTiles)
            {
                // Tiles need Track.Evaluate, so they must be built AFTER Track.Build
                BuildRoad(m_prefabs.road, m_roadRoots);
            }
            else if (visualTrack != null)
            {
                // If the visualTrack had no points (designer left it blank), Track built the math automatically.
                // We must now force the empty visual LineRenderer to copy the generated math so it's visible.
                if (visualTrack.positionCount < 2)
                {
                    var pts = Track.PathPoints;
                    int n = pts != null ? pts.Count : 0;

                    if (n >= 2)
                    {
                        visualTrack.useWorldSpace = true;
                        visualTrack.loop = true;
                        visualTrack.positionCount = n;

                        for (int i = 0; i < n; i++)
                        {
                            // Apply a tiny Y offset to prevent Z-fighting with the ground plane
                            visualTrack.SetPosition(i, pts[i] + (Vector3.up * 0.02f));
                        }
                    }
                }
            }
        }

        void BuildRoad(GameObject prefab, Transform parent)
        {
            Quaternion flat = prefab.transform.localRotation;
            Bounds mb = MeshBounds(prefab);
            Vector3 ls = prefab.transform.localScale;
            float tileLen = Mathf.Max(0.05f, mb.size.y * Mathf.Abs(ls.y));

            int segments = m_roadSegments > 0
                ? m_roadSegments
                : Mathf.Max(12, Mathf.CeilToInt(Track.TotalLength / tileLen));
            float segLen = Track.TotalLength / segments;

            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;            // centre of each segment
                Vector3 pos = Track.Evaluate(t, out var fwd);
                Quaternion look = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;

                var go = Instantiate(prefab, pos, look * flat, parent);
                go.name = "Road_" + i;

                Vector3 s = go.transform.localScale;
                s.y *= segLen / tileLen;
                go.transform.localScale = s;
            }
        }

        // ---- factories ------------------------------------------------------

        void BuildFactories(LevelData level, MaterialLibrary mats)
        {
            var factories = new List<HoleFactorySetup>();
            if (level.holes != null)
                foreach (var hole in level.holes)
                    if (hole != null) factories.Add(SingleHoleFactory(hole));
            if (level.holeFactories != null)
                foreach (var f in level.holeFactories)
                    if (f != null && f.Count > 0) factories.Add(f);

            if (factories.Count == 0) return;

            if (!m_prefabs.HasFactory)
            {
                PFLog.Error("LevelManager: holes only spawn at factories, but no factory prefab is " +
                            "assigned — assign one (e.g. Prefabs/Ingame/Factory). Skipping all holes.");
                return;
            }

            for (int i = 0; i < factories.Count; i++)
                SpawnFactory(factories[i], i, mats);
        }

        static HoleFactorySetup SingleHoleFactory(HoleSetup hole)
        {
            var f = new HoleFactorySetup
            {
                trackPosition = hole.trackPosition,
                placement = hole.placement,
            };
            f.bundle.Add(hole);
            return f;
        }

        void SpawnFactory(HoleFactorySetup setup, int index, MaterialLibrary mats)
        {
            var prefab = m_prefabs.FactoryFor(index);
            if (prefab == null) { PFLog.Error($"No factory prefab for index {index} — skipping."); return; }

            bool pinned = setup.placement != null && setup.placement.overrideTransform;
            Vector3 factoryPos;
            Quaternion rot;
            if (pinned)
            {
                factoryPos = setup.placement.position;
                rot = setup.placement.Rotation;
            }
            else
            {
                var pos = Track.Evaluate(setup.trackPosition);
                Vector3 outward = pos - Track.transform.position; outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
                factoryPos = pos;
                rot = prefab.transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            }

            var go = Instantiate(prefab, factoryPos, rot, m_factoriesRoot);
            go.name = $"Factory_{index}";
            if (pinned) go.transform.localScale = setup.placement.SafeScale;

            var factory = go.GetComponent<HoleFactory>();
            if (factory == null) factory = go.AddComponent<HoleFactory>();

            factory.Setup(setup, m_prefabs.HoleFor(index), mats, Track);
        }

        // ---- lanes ----------------------------------------------------------

        void BuildLanes(LevelData level, MaterialLibrary mats)
        {
            // Drop references to the previous level's lanes (their GameObjects were destroyed in
            // Clear) so the list — and the InputManager binding below — holds only this level's lanes
            // instead of accumulating stale entries across every restart / next-level rebuild.
            Lanes.Clear();

            int n = level.lanes.Count;
            var laneSupply = SupplyDealer.ResolveLaneSupply(level, level.levelNumber);

            Vector3 entry = Track.Evaluate(RunwayTrack.EntryT);
            const float spacing = 4f;
            const float laneY = 0.05f;
            float zBack = entry.z - 2.2f;

            for (int i = 0; i < n; i++)
            {
                var setup = level.lanes[i];
                var prefab = m_prefabs.LaneFor(i);
                if (prefab == null) { PFLog.Error($"No lane prefab for index {i} — skipping."); continue; }

                bool pinned = setup.placement != null && setup.placement.overrideTransform;
                Vector3 padPos = pinned
                    ? setup.placement.position
                    : new Vector3(entry.x + (i - (n - 1) * 0.5f) * spacing, laneY, zBack);
                Quaternion rot = pinned ? setup.placement.Rotation : prefab.transform.rotation;

                var go = Instantiate(prefab, padPos, rot, m_lanesRoot);
                go.name = "Lane_" + i;
                if (pinned) go.transform.localScale = setup.placement.SafeScale;

                var lane = go.GetComponent<Lane>();
                if (lane == null)
                {
                    PFLog.Error($"Lane prefab '{prefab.name}' has no Lane component — skipping.");
                    Destroy(go);
                    continue;
                }
                lane.Setup(setup, laneSupply[i], Track, mats, m_runnersRoot, m_prefabs.character, level.runSpeed, padPos);
                Lanes.Add(lane);
            }
        }

        static Bounds MeshBounds(GameObject prefab)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds;
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.bounds;
            return new Bounds(Vector3.zero, Vector3.one);
        }
    }
}