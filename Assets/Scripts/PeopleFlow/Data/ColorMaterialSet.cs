using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Inspector-friendly <see cref="PeopleColor"/> → <see cref="Material"/> mapping (Unity can't
    /// serialize a <c>Dictionary</c> directly, so it is authored as a list and built into one at
    /// runtime). Drag a material per colour; <see cref="MaterialLibrary"/> uses it for that colour
    /// and falls back to a generated material for any colour left unassigned.
    /// </summary>
    [System.Serializable]
    public class ColorMaterialSet
    {
        [System.Serializable]
        public class Entry
        {
            public PeopleColor color;
            public Material material;
        }

        [Tooltip("One row per colour you want to skin with a real material asset. Colours with no row use a generated material.")]
        public List<Entry> entries = new List<Entry>();

        /// <summary>True if at least one colour has a material assigned.</summary>
        public bool HasAny => entries != null && entries.Exists(e => e != null && e.material != null);

        /// <summary>Build the colour→material lookup (skips empty rows; later rows win on duplicates).</summary>
        public Dictionary<PeopleColor, Material> BuildMap()
        {
            var map = new Dictionary<PeopleColor, Material>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && e.material != null) map[e.color] = e.material;
            return map;
        }

        /// <summary>Merge another set's assigned rows over this one (used by the entry components).</summary>
        public void OverrideWith(ColorMaterialSet other)
        {
            if (other == null || other.entries == null) return;
            foreach (var e in other.entries)
            {
                if (e == null || e.material == null) continue;
                var existing = entries.Find(x => x != null && x.color == e.color);
                if (existing != null) existing.material = e.material;
                else entries.Add(new Entry { color = e.color, material = e.material });
            }
        }
    }
}
