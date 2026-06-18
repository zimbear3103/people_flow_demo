using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Creates and caches simple URP/Lit materials at runtime so the game needs zero pre-made
    /// material assets. Falls back to Standard / a default shader if URP is somehow not present.
    /// </summary>
    public class MaterialLibrary
    {
        readonly Dictionary<int, Material> m_cache = new Dictionary<int, Material>();
        readonly Dictionary<PeopleColor, Material> m_colorOverrides;
        readonly Shader m_shader;

        /// <param name="colorOverrides">
        /// Optional designer-assigned material per colour (see <see cref="ColorMaterialSet"/>).
        /// A colour present here returns its material from <see cref="Colored"/>; any other colour
        /// falls back to a generated material.
        /// </param>
        public MaterialLibrary(Dictionary<PeopleColor, Material> colorOverrides = null)
        {
            m_colorOverrides = colorOverrides;
            m_shader = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Standard")
                       ?? Shader.Find("Sprites/Default");
        }

        /// <summary>A cached opaque material of the given colour.</summary>
        public Material Solid(Color c, float smoothness = 0.12f)
        {
            int key = Key(c, smoothness);
            if (m_cache.TryGetValue(key, out var m)) return m;

            m = new Material(m_shader) { name = $"PF_{c}" };
            // Cover both URP (_BaseColor) and built-in (color) property names.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m_cache[key] = m;
            return m;
        }

        /// <summary>The designer-assigned material for this colour if one is mapped, else a generated one.</summary>
        public Material Colored(PeopleColor c)
        {
            if (m_colorOverrides != null && m_colorOverrides.TryGetValue(c, out var m) && m != null)
                return m;
            return Solid(c.ToColor());
        }

        /// <summary>True if a designer material is mapped for this colour (vs. a generated one).</summary>
        public bool HasColorOverride(PeopleColor c)
            => m_colorOverrides != null && m_colorOverrides.TryGetValue(c, out var m) && m != null;

        // Named convenience materials.
        public Material Neutral => Solid(ColorPalette.Neutral);
        public Material Ground => Solid(new Color(0.74f, 0.80f, 0.70f));
        public Material Track => Solid(new Color(0.92f, 0.93f, 0.97f), 0.05f);
        public Material Dark => Solid(new Color(0.18f, 0.19f, 0.24f));
        public Material Hidden => Solid(ColorPalette.Hidden);
        public Material Ice => Solid(new Color(0.62f, 0.85f, 0.95f), 0.6f);
        public Material DimPip => Solid(new Color(0.78f, 0.80f, 0.84f));

        static int Key(Color c, float s)
        {
            int r = Mathf.RoundToInt(c.r * 255f);
            int g = Mathf.RoundToInt(c.g * 255f);
            int b = Mathf.RoundToInt(c.b * 255f);
            int sm = Mathf.RoundToInt(Mathf.Clamp01(s) * 15f);
            return (r << 24) ^ (g << 16) ^ (b << 8) ^ sm;
        }
    }
}
