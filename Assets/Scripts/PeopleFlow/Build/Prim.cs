using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Helpers for building primitive GameObjects at runtime (the whole game is assembled from
    /// capsules/cylinders/cubes + coloured materials, so there are no copyrighted art assets).
    /// </summary>
    public static class Prim
    {
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
