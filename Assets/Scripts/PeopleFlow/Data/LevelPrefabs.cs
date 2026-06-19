using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// The drag-and-drop prefab set a level is built from. Assign these in the inspector (on the
    /// <see cref="LevelManager"/> itself, or on the entry component that forwards them —
    /// <see cref="GameBootstrap"/> / <see cref="GamePlayController"/>) and the level is
    /// assembled by instantiating them, instead of building primitives in code.
    ///
    /// One prefab per role: the same Hole / Factory / Lane prefab is reused for every hole / factory /
    /// lane the level defines, and the character is instantiated per released runner. The
    /// <c>…For(index)</c> accessors keep an index parameter (ignored) so the build sites read clearly
    /// and a future per-index variant set can drop in without touching them.
    /// </summary>
    [System.Serializable]
    public class LevelPrefabs
    {
        public GameObject hole;

        public GameObject factorie;

        public GameObject lane;

        public GameObject character;

        public GameObject road;

        /// <summary>True only when the hole prefab, the lane prefab, and the character are all assigned.</summary>
        public bool IsComplete => hole != null && lane != null && character != null;

        /// <summary>Hole prefab for hole <paramref name="index"/> (one prefab reused for every hole).</summary>
        public GameObject HoleFor(int index) => hole;

        /// <summary>Lane prefab for lane <paramref name="index"/> (one prefab reused for every lane).</summary>
        public GameObject LaneFor(int index) => lane;

        /// <summary>True if a factory prefab is assigned (needed only for factory levels).</summary>
        public bool HasFactory => factorie != null;

        /// <summary>Factory prefab for factory <paramref name="index"/> (one prefab reused for every factory).</summary>
        public GameObject FactoryFor(int index) => factorie;

        /// <summary>Names of any still-unassigned required prefabs, for a clear error message.</summary>
        public string MissingList()
        {
            var missing = new List<string>(3);
            if (hole == null) missing.Add("Hole");
            if (lane == null) missing.Add("Lane");
            if (character == null) missing.Add("Character");
            return string.Join(", ", missing);
        }

        /// <summary>Copy any assigned prefabs from <paramref name="other"/> over our own (only
        /// non-null entries override, so a partially-configured set never clears existing slots).</summary>
        public void OverrideWith(LevelPrefabs other)
        {
            if (other == null) return;
            if (other.hole != null) hole = other.hole;
            if (other.factorie != null) factorie = other.factorie;
            if (other.lane != null) lane = other.lane;
            if (other.character != null) character = other.character;
            if (other.road != null) road = other.road;
        }
    }
}
