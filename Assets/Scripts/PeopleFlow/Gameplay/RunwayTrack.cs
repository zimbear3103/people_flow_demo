using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// The closed loop runway. Builds an oval polyline from <see cref="LevelData"/>, exposes
    /// constant-speed positions along it (arc-length parameterised), tracks how full it is, and
    /// is the authority that detects a permanent jam (full + nobody has anywhere to go) → lose.
    /// </summary>
    public class RunwayTrack : MonoBehaviour
    {
        [SerializeField] int m_segments = 128;

        Vector3[] m_points;        // world-space loop points (closed)
        float[] m_cumulative;      // cumulative arc length, length = segments + 1
        float m_totalLength;
        int m_segCount;
        bool m_built;

        int m_capacity;
        List<ArrowSetup> m_arrows = new List<ArrowSetup>();
        readonly List<People> m_runners = new List<People>();
        readonly List<Hole> m_holes = new List<Hole>();
        readonly List<HoleFactory> m_factories = new List<HoleFactory>();

        /// <summary>Normalised position where lanes feed runners onto the loop (bottom centre).</summary>
        public const float EntryT = 0f;

        /// <summary>Fired with the current fill ratio (0..1) whenever a runner is added/removed.</summary>
        public event Action<float> OnFillChanged;

        public float TotalLength => m_totalLength;
        public int Capacity => m_capacity;
        public int Count => m_runners.Count;
        public bool IsFull => m_runners.Count >= m_capacity;
        public float Fill => m_capacity > 0 ? (float)m_runners.Count / m_capacity : 0f;
        public IReadOnlyList<Hole> Holes => m_holes;

        /// <summary>The world-space loop vertices, in order (closed: the last connects back to the
        /// first). Dense for the oval, sparse corner points for a rectangle. Visuals (the LineRenderer)
        /// render these directly so they match whatever <see cref="TrackShape"/> built the path.</summary>
        public IReadOnlyList<Vector3> PathPoints => m_points;

        // ---- build ----------------------------------------------------------

        public void Build(LevelData level)
        {
            m_capacity = Mathf.Max(1, level.runwayCapacity);
            m_arrows = level.arrows != null ? new List<ArrowSetup>(level.arrows) : new List<ArrowSetup>();

            // The loop geometry is generated per-shape (oval / rectangle / square / custom). Everything
            // below — arc-length tables, Evaluate, ClosestT — is shape-agnostic and works on any closed
            // polyline, so adding a shape only means adding a point generator in TrackPath.
            var local = TrackPath.Build(level, Mathf.Max(16, m_segments));

            // Project the shape's local XZ points through this object's full transform, so the level's
            // trackPlacement (position / rotation / scale) moves, turns and sizes the whole loop. With
            // an identity transform at the origin this is just the local points unchanged.
            int n = local.Count;
            m_points = new Vector3[n];
            for (int i = 0; i < n; i++)
                m_points[i] = transform.TransformPoint(new Vector3(local[i].x, 0f, local[i].z));

            m_cumulative = new float[n + 1];
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                acc += Vector3.Distance(m_points[i], m_points[(i + 1) % n]);
                m_cumulative[i + 1] = acc;
            }
            m_totalLength = acc;
            m_segCount = n;
            m_built = true;
        }

        public void RegisterHole(Hole h)
        {
            if (h != null && !m_holes.Contains(h)) m_holes.Add(h);
        }

        /// <summary>Drop a hole from the targetable set — called by a <see cref="HoleFactory"/> when a
        /// completed hole is retired, so runners stop aiming for it before it animates away.</summary>
        public void UnregisterHole(Hole h)
        {
            if (h != null) m_holes.Remove(h);
        }

        public void RegisterFactory(HoleFactory f)
        {
            if (f != null && !m_factories.Contains(f)) m_factories.Add(f);
        }

        public void UnregisterFactory(HoleFactory f)
        {
            if (f != null) m_factories.Remove(f);
        }

        // ---- path evaluation ------------------------------------------------

        /// <summary>Normalised track parameter (0..1) of the point on the loop closest to
        /// <paramref name="worldPos"/>. Used to detect runners by where a hole actually sits, even
        /// when the hole is offset from the loop (e.g. on a factory conveyor).</summary>
        public float ClosestT(Vector3 worldPos)
        {
            if (!m_built || m_segCount == 0) return 0f;

            float bestSqr = float.MaxValue;
            float bestDist = 0f;
            for (int i = 0; i < m_segCount; i++)
            {
                Vector3 p0 = m_points[i];
                Vector3 p1 = m_points[(i + 1) % m_segCount];
                Vector3 seg = p1 - p0;
                float segLenSqr = seg.sqrMagnitude;
                float u = segLenSqr > 1e-6f ? Mathf.Clamp01(Vector3.Dot(worldPos - p0, seg) / segLenSqr) : 0f;
                Vector3 proj = p0 + seg * u;
                float d = (proj - worldPos).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    bestDist = m_cumulative[i] + Mathf.Sqrt(segLenSqr) * u;
                }
            }
            return m_totalLength > 1e-5f ? bestDist / m_totalLength : 0f;
        }

        public Vector3 Evaluate(float t01) => Evaluate(t01, out _);

        public Vector3 Evaluate(float t01, out Vector3 forward)
        {
            if (!m_built || m_segCount == 0)
            {
                forward = Vector3.forward;
                return transform.position;
            }

            float d = Mathf.Repeat(t01, 1f) * m_totalLength;

            int seg = m_segCount - 1;
            for (int i = 0; i < m_segCount; i++)
            {
                if (d <= m_cumulative[i + 1]) { seg = i; break; }
            }

            float segStart = m_cumulative[seg];
            float segLen = m_cumulative[seg + 1] - segStart;
            float f = segLen > 1e-5f ? (d - segStart) / segLen : 0f;

            Vector3 p0 = m_points[seg];
            Vector3 p1 = m_points[(seg + 1) % m_segCount];
            Vector3 dir = p1 - p0;
            forward = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            return Vector3.Lerp(p0, p1, f);
        }

        /// <summary>Speed multiplier from any arrow zone covering this normalised position.</summary>
        public float SpeedMultiplierAt(float t01)
        {
            t01 = Mathf.Repeat(t01, 1f);
            float mult = 1f;
            for (int i = 0; i < m_arrows.Count; i++)
            {
                var z = m_arrows[i];
                float start = Mathf.Repeat(z.trackPosition, 1f);
                float end = start + z.length;
                bool inside = (t01 >= start && t01 <= end) || (end > 1f && t01 <= end - 1f);
                if (inside) mult = Mathf.Max(mult, z.speedMultiplier);
            }
            return mult;
        }

        // ---- runner bookkeeping --------------------------------------------

        public void Register(People c)
        {
            if (c == null || m_runners.Contains(c)) return;
            m_runners.Add(c);
            OnFillChanged?.Invoke(Fill);
        }

        public void Unregister(People c)
        {
            if (m_runners.Remove(c))
                OnFillChanged?.Invoke(Fill);
        }

        /// <summary>True if any runner sits within <paramref name="radius"/> of a world point.</summary>
        public bool HasRunnerNear(Vector3 worldPos, float radius)
        {
            float sqr = radius * radius;
            for (int i = 0; i < m_runners.Count; i++)
            {
                var r = m_runners[i];
                if (r != null && (r.SpacingPosition - worldPos).sqrMagnitude < sqr)
                    return true;
            }
            return false;
        }

        // ---- jam detection (lose authority) --------------------------------

        void Update()
        {
            if (!m_built || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            if (IsFull && !HasViableMove())
                GameManager.Instance.ReportRunwayJam();
        }

        /// <summary>
        /// True if the runway can still drain: a factory is mid-swap (a hole is about to appear),
        /// someone is mid-jump (a slot is about to free), or some runner has a matching, unlocked,
        /// not-yet-full hole to aim for.
        /// </summary>
        bool HasViableMove()
        {
            // A factory between holes will pop its next hole in within a fraction of a second — don't
            // call a jam during that gap, or a tight factory level could lose on a false positive.
            for (int i = 0; i < m_factories.Count; i++)
                if (m_factories[i] != null && m_factories[i].IsProducing) return true;

            for (int i = 0; i < m_runners.Count; i++)
            {
                var r = m_runners[i];
                if (r == null) continue;
                if (r.IsJumping || r.IsEnteringRoad) return true; // mid-motion: a slot is about to free / fill
                for (int h = 0; h < m_holes.Count; h++)
                {
                    if (m_holes[h] != null && m_holes[h].CanAccept(r.Color)) return true;
                }
            }
            return false;
        }
    }
}
