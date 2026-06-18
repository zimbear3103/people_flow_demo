using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Helpers for building primitive GameObjects at runtime (the procedural fallback art) plus
    /// utilities for binding to art that comes from drag-and-drop prefabs: collecting the mesh
    /// renderers that should be tinted to a colour, applying that tint, and finding a named child
    /// overlay (lock / barrier / ice).
    /// </summary>
    public static class Prim
    {
        // Substrings (lower-case) of renderer names that should NOT be recoloured when a prefab is
        // tinted to a PeopleColor: shadows, count labels, outlines, eyes, and state overlays.
        static readonly string[] s_tintExcludes =
            { "shadow", "number", "text", "outlin", "eye", "frozen", "barrier", "gate", "ice", "lock" };

        /// <summary>
        /// The mesh/skinned renderers under <paramref name="root"/> that represent the object's
        /// colourable body — particle systems and named-out helper renderers (see
        /// <c>s_tintExcludes</c>) are skipped so a tint doesn't bleed onto shadows, labels, etc.
        /// </summary>
        public static Renderer[] CollectTintable(GameObject root)
        {
            var all = root.GetComponentsInChildren<Renderer>(true);
            var list = new List<Renderer>(all.Length);
            foreach (var r in all)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                bool excluded = false;
                for (int i = 0; i < s_tintExcludes.Length; i++)
                    if (n.Contains(s_tintExcludes[i])) { excluded = true; break; }
                if (!excluded) list.Add(r);
            }
            return list.ToArray();
        }

        /// <summary>Assign a shared material to every renderer in the set (cheap, batch-friendly).</summary>
        public static void Tint(Renderer[] renderers, Material mat)
        {
            if (renderers == null || mat == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterial = mat;
        }

        /// <summary>First descendant transform with the given name (case-insensitive), or null.</summary>
        public static Transform FindDescendant(Transform root, params string[] names)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                foreach (var name in names)
                    if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }

        /// <summary>Create a primitive, optionally stripping its auto-added collider.</summary>
        public static GameObject Create(PrimitiveType type, string name, Transform parent,
            bool withCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!withCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject Create(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat, bool withCollider = false)
        {
            var go = Create(type, name, parent, withCollider);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            SetMaterial(go, mat);
            return go;
        }

        public static void SetMaterial(GameObject go, Material mat)
        {
            if (go == null || mat == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        /// <summary>
        /// A one-shot particle burst (configured entirely in code) used for hole completion.
        /// Call <see cref="ParticleSystem.Play"/> to fire it.
        /// </summary>
        public static ParticleSystem CreateBurst(Transform parent, Color color)
        {
            var go = new GameObject("Burst");
            if (parent != null) go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 3.5f;
            main.startSize = 0.18f;
            main.startColor = color;
            main.gravityModifier = 0.6f;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            var pmat = new Material(shader);
            if (pmat.HasProperty("_BaseColor")) pmat.SetColor("_BaseColor", color);
            pmat.color = color;
            renderer.material = pmat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            return ps;
        }
    }
}
