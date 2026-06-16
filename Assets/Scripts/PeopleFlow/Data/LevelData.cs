using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>One waiting lane: an ordered list of colours. Index 0 = front of the queue.</summary>
    [System.Serializable]
    public class LaneSetup
    {
        [Tooltip("Front of the queue is element 0; characters are pushed onto the runway in this order.")]
        public List<PeopleColor> characters = new List<PeopleColor>();

        [Header("Barrier (section 7)")]
        [Tooltip("If true the lane is blocked until 'unlockAfterHolesCompleted' holes are done.")]
        public bool barrier;
        public int unlockAfterHolesCompleted;
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

        [Header("Loop shape (procedural oval, world units)")]
        public float loopWidth = 7f;
        public float loopHeight = 11f;

        [Header("Content")]
        public List<LaneSetup> lanes = new List<LaneSetup>();
        public List<HoleSetup> holes = new List<HoleSetup>();
        public List<ArrowSetup> arrows = new List<ArrowSetup>();

        public int TotalHoles => holes != null ? holes.Count : 0;
    }
}
