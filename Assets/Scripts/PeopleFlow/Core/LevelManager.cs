using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Builds one level's gameplay objects from <see cref="LevelData"/>: the runway track + its
    /// visual, the holes placed around the loop, and the lanes with their populated queues. Then
    /// wires input/timer/UI and tells <see cref="GameManager"/> to start.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Level prefabs (required — drag in your Hole, Lane and Character prefabs)")]
        [Tooltip("Instantiated to build the level. Holes, lanes and runners come from these prefabs " +
                 "instead of being spawned from primitives in code. An entry component " +
                 "(GameBootstrap / PeopleFlowGameController) may also supply or override them.")]
        [SerializeField] LevelPrefabs m_prefabs = new LevelPrefabs();
        [SerializeField] Transform m_factoriesRoot;
        [SerializeField] Transform m_lanesRoot;

        [Header("Colour materials")]
        [Tooltip("PeopleColor → Material. A colour with a material here is skinned with it; any colour " +
                 "left empty uses a generated material. May also be supplied by the entry component.")]
        [SerializeField] ColorMaterialSet m_colorMaterials = new ColorMaterialSet();

        [Header("Road tiling (optional)")]
        [Tooltip("How many copies of the road prefab to tile around the loop. 0 = auto, from the road " +
                 "tile's own length. Raise it for a smoother curve, lower it for chunkier segments.")]
        [SerializeField] int m_roadSegments = 0;

        [Header("Factory placement")]
        [Tooltip("How far outside the loop (away from its centre) hole factories are pushed, so the " +
                 "factory structure sits beside the road instead of on the running surface. The hole " +
                 "it produces still rides its conveyor and is detected at the nearest point on the track.")]
        [SerializeField] float m_factoryOffset = 3f;

        public RunwayTrack Track { get; private set; }
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
                            "PeopleFlowGameController). Aborting level build.");
                return;
            }

            // Designer-assigned materials per colour, with a generated fallback for empty slots.
            var mats = new MaterialLibrary(m_colorMaterials.BuildMap());

            var trackGo = new GameObject("RunwayTrack");
            trackGo.transform.SetParent(transform, false);

            if (level.trackPlacement != null && level.trackPlacement.overrideTransform)
                level.trackPlacement.ApplyWorld(trackGo.transform);
            Track = trackGo.AddComponent<RunwayTrack>();
            Track.Build(level);

            m_factoriesRoot = NewRoot("Factories");
            m_lanesRoot = NewRoot("Lanes");
            m_runnersRoot = NewRoot("CharactersRoot");

            BuildRunwayVisual(level, mats);
            BuildFactories(level, mats);
            BuildLanes(level, mats);

            input.Bind(Lanes, Camera.main);
            timer.Begin(level.timeLimit);
            UIManager.Instance?.Bind(level, timer, Track);
            GameManager.Instance.BeginLevel(level.TotalHoles);

            int singleHoleFactories = level.holes != null ? level.holes.Count : 0;
            int bundleFactories = level.holeFactories != null ? level.holeFactories.Count : 0;
            PFLog.Info($"Level {level.levelNumber} built: {level.TotalHoles} holes from " +
                       $"{singleHoleFactories + bundleFactories} factories " +
                       $"({singleHoleFactories} single-hole + {bundleFactories} bundle), " +
                       $"{Lanes.Count} lanes, capacity {level.runwayCapacity}.");
        }

        Transform NewRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        // ---- factories (every hole spawns at one) ---------------------------

        /// <summary>
        /// Build every hole as a factory under the Factories root — holes only ever spawn at a
        /// factory. Each standalone <see cref="LevelData.holes"/> entry becomes its own single-hole
        /// factory (see <see cref="SingleHoleFactory"/>); each authored <see cref="LevelData.holeFactories"/>
        /// keeps its full bundle. A factory produces its bundle one hole at a time (see
        /// <see cref="HoleFactory"/>); the produced hole rides the conveyor and is detected at the
        /// nearest point on the track.
        /// </summary>
        void BuildFactories(LevelData level, MaterialLibrary mats)
        {
            // Collect the work first so single-hole and bundle factories share one indexed spawn loop.
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

        /// <summary>Wrap a standalone hole as its own single-hole factory, so it spawns at a factory
        /// like every other hole. The factory sits at the hole's track position (or its pinned
        /// placement); the produced hole rides the factory conveyor and is detected at the nearest
        /// track point. The hole's own <c>trackPosition</c> inside the bundle is ignored (the factory
        /// position is used) — exactly as for an authored bundle.</summary>
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

        /// <summary>Instantiate one factory from <paramref name="setup"/> (pinned to its placement, or
        /// pushed just outside the loop at its track position), add/keep its <see cref="HoleFactory"/>,
        /// and hand it the Hole prefab + bundle to produce. The component is added at runtime if the
        /// factory prefab doesn't already carry one.</summary>
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
                // Push the factory off the running surface, outward from the loop centre, so its
                // structure sits beside the road rather than on it. The hole it produces rides the
                // conveyor (holder) back toward the track and is detected at the nearest track point.
                Vector3 outward = pos - Track.transform.position; outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
                factoryPos = pos + outward * m_factoryOffset;

                // Fixed orientation: the prefab's authored rotation yawed +90° so the conveyor
                // ("Bangtruyen") arc faces the right way. The produced hole's local offset is measured
                // against this orientation, so it must stay fixed, not track-relative.
                rot = prefab.transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            }

            var go = Instantiate(prefab, factoryPos, rot, m_factoriesRoot);
            go.name = $"Factory_{index}";
            if (pinned) go.transform.localScale = setup.placement.SafeScale;

            var factory = go.GetComponent<HoleFactory>();
            if (factory == null) factory = go.AddComponent<HoleFactory>();

            // Factories produce holes from the level's single Hole prefab.
            factory.Setup(setup, m_prefabs.HoleFor(index), mats, Track);
        }

        // ---- lanes ----------------------------------------------------------

        void BuildLanes(LevelData level, MaterialLibrary mats)
        {
            int n = level.lanes.Count;
            Vector3 entry = Track.Evaluate(RunwayTrack.EntryT);
            const float spacing = 4f;       // horizontal gap between lanes
            const float laneY = 0.05f;      // all lanes sit just above the ground plane
            float zBack = entry.z - 2.2f;   // lanes sit just outside the loop, toward the camera

            for (int i = 0; i < n; i++)
            {
                var setup = level.lanes[i];
                var prefab = m_prefabs.LaneFor(i);
                if (prefab == null) { PFLog.Error($"No lane prefab for index {i} — skipping."); continue; }

                // Pinned to an explicit transform, or auto-spaced along the bottom edge.
                bool pinned = setup.placement != null && setup.placement.overrideTransform;
                Vector3 padPos = pinned
                    ? setup.placement.position
                    : new Vector3(entry.x + (i - (n - 1) * 0.5f) * spacing, laneY, zBack);
                Quaternion rot = pinned ? setup.placement.Rotation : prefab.transform.rotation;

                // Place at the resolved pad position immediately (4-arg overload) while keeping the
                // chosen rotation, so the lane never flickers at the prefab's origin.
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
                lane.Setup(setup, Track, mats, m_runnersRoot, m_prefabs.character, level.runSpeed, padPos);
                Lanes.Add(lane);
            }
        }

        // ---- runway visual --------------------------------------------------

        void BuildRunwayVisual(LevelData level, MaterialLibrary mats)
        {
            // Ground plane (Plane is 10×10 units at scale 1).
            float sizeX = level.loopWidth + 9f;
            float sizeZ = level.loopHeight + 11f;
            Prim.Create(PrimitiveType.Plane, "Ground", transform,
                new Vector3(0f, 0f, 0f),
                new Vector3(sizeX / 10f, 1f, sizeZ / 10f), mats.Ground);

            // The loop visual: tile the Road prefab around the path, or draw it as a LineRenderer.
            // Both follow Track's path, so either matches the level's shape. Fall back to the line if
            // RoadTiles is chosen but no road prefab is assigned.
            bool useTiles = level.roadVisual == RoadVisual.RoadTiles && m_prefabs.road != null;
            if (useTiles) BuildRoad(m_prefabs.road, NewRoot("Road"));
            else BuildTrackLine();

            // Entry marker so the player can see where pushed characters enter.
            Vector3 entry = Track.Evaluate(RunwayTrack.EntryT);
            Prim.Create(PrimitiveType.Cube, "EntryMarker", transform,
                new Vector3(entry.x, 0.04f, entry.z), new Vector3(1.5f, 0.05f, 0.5f),
                mats.Solid(new Color(1f, 1f, 1f)));
        }

        /// <summary>
        /// Tile the road prefab around the loop so it forms the visible running surface: N copies,
        /// each placed on the track via <see cref="RunwayTrack.Evaluate"/>, turned to face travel,
        /// and stretched along its run axis so neighbours meet. Runners follow the same Evaluate
        /// path, so they run along the road. "Multiple geo" = these N tiled segments.
        /// </summary>
        void BuildRoad(GameObject prefab, Transform parent)
        {
            // The Road prefab is authored lying flat via its own local rotation (an X ~90° tilt that
            // lays the imported model onto the ground). Keep that rotation and only yaw each tile to
            // face travel — instantiating with a fresh LookRotation alone would discard the tilt and
            // stand the tiles up on edge.
            Quaternion flat = prefab.transform.localRotation;

            // After the flat tilt, the tile's length runs along its LOCAL Y, so that's the axis we
            // measure and stretch to make neighbours meet.
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

                // Stretch along the run axis (local Y after the flat tilt) so each tile spans exactly
                // one segment (continuous loop).
                Vector3 s = go.transform.localScale;
                s.y *= segLen / tileLen;
                go.transform.localScale = s;
            }
        }

        void BuildTrackLine()
        {
            // Draw the loop as a thick line through the track's own vertices, so it reproduces the
            // shape exactly — dense for the oval, crisp corners for a rectangle/square/custom path.
            var pts = Track.PathPoints;
            int n = pts != null ? pts.Count : 0;
            if (n < 2)
            {
                PFLog.Warn("BuildTrackLine: track has no path points — skipping the line visual.");
                return;
            }

            var lineGo = new GameObject("TrackLine");
            lineGo.transform.SetParent(transform, false);
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.positionCount = n;
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.95f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < n; i++)
                lr.SetPosition(i, pts[i] + Vector3.up * 0.02f);

            var lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var lineMat = new Material(lineShader);
            Color lineColor = new Color(0.95f, 0.96f, 1f);
            if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", lineColor);
            lineMat.color = lineColor;
            lr.material = lineMat;
        }

        /// <summary>Local-space mesh bounds of a prefab (mesh or skinned), for sizing the road tiles.</summary>
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
