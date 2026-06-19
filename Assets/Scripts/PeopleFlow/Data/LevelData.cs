using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A stored transform (world position, Euler rotation, local scale) for one built object.
    /// When <see cref="overrideTransform"/> is ON the level is built by placing the object exactly
    /// here; when OFF the build computes a default placement (holes / factories from their
    /// <c>trackPosition</c>, lanes from automatic spacing). This is how a level's geometry is fully
    /// data-driven: author the spec to pin an object, or leave it off to keep the procedural layout.
    /// </summary>
    [System.Serializable]
    public class TransformSpec
    {
        [Tooltip("ON = place this object at the exact position/rotation/scale below. OFF = let the " +
                 "build compute a default placement (from trackPosition / automatic lane spacing).")]
        public bool overrideTransform = false;
        public Vector3 position = Vector3.zero;
        [Tooltip("Rotation in Euler degrees.")]
        public Vector3 rotationEuler = Vector3.zero;
        public Vector3 scale = Vector3.one;

        public Quaternion Rotation => Quaternion.Euler(rotationEuler);

        /// <summary>Local scale to apply, falling back to one if left at zero (a freshly-serialized
        /// spec) so an enabled-but-unscaled object is never invisible.</summary>
        public Vector3 SafeScale => scale == Vector3.zero ? Vector3.one : scale;

        /// <summary>Apply this spec to <paramref name="t"/> as a WORLD position/rotation plus local
        /// scale. Used when <see cref="overrideTransform"/> is set.</summary>
        public void ApplyWorld(Transform t)
        {
            t.SetPositionAndRotation(position, Rotation);
            t.localScale = SafeScale;
        }
    }

    /// <summary>One waiting lane: an ordered list of colours. Index 0 = front of the queue.</summary>
    [System.Serializable]
    public class LaneSetup
    {
        [Tooltip("Front of the queue is element 0; characters are pushed onto the runway in this order.")]
        public List<PeopleColor> characters = new List<PeopleColor>();

        [Min(1)]
        [Tooltip("Max minions per released group. The waiting line forms single-colour groups of at " +
                 "most this many; a tap/hold releases one such group.")]
        public int groupSize = 3;
        [SerializeField] float m_releaseInterval = 0.22f;
        [SerializeField] float m_memberSpacing = 0.2f;
        [SerializeField] float m_groupSpacing = 0.9f;
        [SerializeField] float m_memberLaunchStagger = 0.08f;
        [SerializeField] bool m_releaseRightToLeft = false;
        [SerializeField] float m_previewStandOffset = 0f;
        [Header("Barrier")]
        [Tooltip("If true the lane is blocked until 'unlockAfterHolesCompleted' holes are done.")]
        public bool barrier;
        public int unlockAfterHolesCompleted;

        [Header("Placement")]
        [Tooltip("Where this lane's pad sits. Override it to pin the lane, or leave it off to let the " +
                 "build space the lanes automatically along the bottom edge.")]
        public TransformSpec placement = new TransformSpec();

    }

    /// <summary>One hole placed around the loop at a normalised track position.</summary>
    [System.Serializable]
    public class HoleSetup
    {
        public PeopleColor color = PeopleColor.Red;
        [Min(1)] public int requiredCount = 3;

        [Range(0f, 1f)]
        [Tooltip("Normalised position around the loop (0 = entry / bottom-centre, increasing clockwise). " +
                 "Avoid placing a hole at exactly 0 (the spawn point) — runners detect holes by crossing " +
                 "them, so a hole sitting on the entry would only be caught after a full lap.")]
        public float trackPosition = 0.25f;

        [Header("Specials (section 7)")]
        [Tooltip("Colour shown as a grey '?' until a runner gets close.")]
        public bool hidden;
        public HoleMechanic mechanic = HoleMechanic.None;
        [Tooltip("For Frozen / Gate: number of OTHER holes that must complete before this one unlocks.")]
        public int unlockAfterHolesCompleted;

        [Header("Placement")]
        [Tooltip("Where this standalone hole sits. Override it to pin the hole at an exact transform " +
                 "(its detection point is taken from the nearest spot on the track); leave it off to " +
                 "place the hole on the loop at 'trackPosition'.")]
        public TransformSpec placement = new TransformSpec();
    }

    /// <summary>
    /// A hole factory: a single track position that produces a <em>bundle</em> of holes one at a
    /// time. The first hole in the bundle is shown; when it completes it disappears and the next
    /// hole in the bundle is spawned in its place, until the bundle is exhausted. Each bundle entry
    /// reuses <see cref="HoleSetup"/> for its colour / count / specials — its own
    /// <see cref="HoleSetup.trackPosition"/> is ignored; the factory's <see cref="trackPosition"/>
    /// is used for every hole it produces.
    /// </summary>
    [System.Serializable]
    public class HoleFactorySetup
    {
        [Range(0f, 1f)]
        [Tooltip("Normalised position around the loop where this factory's holes appear " +
                 "(0 = entry / bottom-centre). Avoid exactly 0 — see HoleSetup.trackPosition.")]
        public float trackPosition = 0.5f;

        [Tooltip("The ordered bundle of holes this factory produces. Index 0 is shown first; each " +
                 "completes, disappears, and is replaced by the next. (Each entry's own trackPosition " +
                 "is ignored — the factory position above is used.)")]
        public List<HoleSetup> bundle = new List<HoleSetup>();

        [Header("Placement")]
        [Tooltip("Where the factory structure sits. Override it to pin the factory at an exact " +
                 "transform; leave it off to push it just outside the loop at 'trackPosition'. Each " +
                 "hole it produces rides the factory's conveyor and is detected at the nearest track point.")]
        public TransformSpec placement = new TransformSpec();

        /// <summary>Number of holes this factory will produce over its lifetime.</summary>
        public int Count => bundle != null ? bundle.Count : 0;
    }

    /// <summary>An arrow / speed zone on the runway that pushes runners faster through a range.</summary>
    [System.Serializable]
    public class ArrowSetup
    {
        [Range(0f, 1f)] public float trackPosition = 0.5f;
        [Range(0.02f, 0.5f)] public float length = 0.1f;
        [Min(1f)] public float speedMultiplier = 2.5f;
    }

    /// <summary>
    /// Designer-authored level definition. Create via Assets ▸ Create ▸ PeopleFlow ▸ Level,
    /// or generate the bundled samples via the menu PeopleFlow ▸ Generate Sample Levels.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "PeopleFlow/Level")]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        public int levelNumber = 1;

        [Header("Rules")]
        public float timeLimit = 60f;
        [Min(1)] public int runwayCapacity = 12;
        public float runSpeed = 3.5f;

        [Header("Loop shape (world units)")]
        [Tooltip("Geometry of the runway loop: smooth Oval, sharp/rounded Rectangle or Square, or a " +
                 "Custom hand-placed path (customWaypoints).")]
        public TrackShape trackShape = TrackShape.Oval;
        public float loopWidth = 7f;
        public float loopHeight = 11f;
        [Min(0f)]
        [Tooltip("Rectangle / Square only: corner rounding radius in world units. 0 = sharp 'kinky' " +
                 "corners; larger values round them off (clamped to half the shorter side).")]
        public float cornerRadius = 0f;
        [Tooltip("Custom shape only: ordered corner points (local XZ around the loop centre, Y ignored). " +
                 "The loop closes from the last point back to the first. Index 0 should sit at the " +
                 "bottom-centre entry where lanes feed in. Leave empty to fall back to an oval.")]
        public List<Vector3> customWaypoints = new List<Vector3>();

        [Header("Loop visual")]
        [Tooltip("How the runway is drawn — tiled Road prefab, or a LineRenderer through the path. " +
                 "Both follow the same path, so they match the chosen shape.")]
        public RoadVisual roadVisual = RoadVisual.RoadTiles;

        [Tooltip("Where the runway loop sits. Override it to move / rotate / scale the whole loop " +
                 "(its centre, orientation and size); leave it off to centre the loop on the origin. " +
                 "The shape above is generated relative to this transform.")]
        public TransformSpec trackPlacement = new TransformSpec();
        public GameObject trackLinePrefab;
        [Header("Content")]
        public List<LaneSetup> lanes = new List<LaneSetup>();
        [Tooltip("Standalone holes. Holes only ever spawn at factories, so at build each one is wrapped " +
                 "into its own single-hole factory, placed at its trackPosition (or its pinned placement).")]
        public List<HoleSetup> holes = new List<HoleSetup>();
        [Tooltip("Hole factories: each produces a bundle of holes one at a time at one position.")]
        public List<HoleFactorySetup> holeFactories = new List<HoleFactorySetup>();
        public List<ArrowSetup> arrows = new List<ArrowSetup>();

        /// <summary>Every hole that must be completed to win: standalone holes plus every hole across
        /// all factory bundles. Drives the win condition and the HUD progress count.</summary>
        public int TotalHoles
        {
            get
            {
                int n = holes != null ? holes.Count : 0;
                if (holeFactories != null)
                    foreach (var f in holeFactories) n += f != null ? f.Count : 0;
                return n;
            }
        }
    }
}
