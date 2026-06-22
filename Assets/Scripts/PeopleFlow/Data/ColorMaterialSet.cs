using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
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

        public bool HasAny => entries != null && entries.Exists(e => e != null && e.material != null);

        public Dictionary<PeopleColor, Material> BuildMap()
        {
            var map = new Dictionary<PeopleColor, Material>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && e.material != null) map[e.color] = e.material;
            return map;
        }

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
