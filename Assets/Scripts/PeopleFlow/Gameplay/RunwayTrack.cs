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

        // ---- build ----------------------------------------------------------

        public void Build(LevelData level)
        {
            m_capacity = Mathf.Max(1, level.runwayCapacity);
            m_arrows = level.arrows != null ? new List<ArrowSetup>(level.arrows) : new List<ArrowSetup>();

            int n = Mathf.Max(16, m_segments);
            Vector3 center = transform.position;
            float a = level.loopWidth * 0.5f;
            float b = level.loopHeight * 0.5f;

            m_points = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                // Start at the bottom-centre (-90°) so EntryT = 0 sits where lanes feed in.
                float ang = -Mathf.PI * 0.5f + Mathf.PI * 2f * (i / (float)n);
                m_points[i] = center + new Vector3(a * Mathf.Cos(ang), 0f, b * Mathf.Sin(ang));
            }

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

        // ---- path evaluation ------------------------------------------------

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
                if (r != null && (r.transform.position - worldPos).sqrMagnitude < sqr)
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
        /// True if the runway can still drain: someone is mid-jump (a slot is about to free), or
        /// some runner has a matching, unlocked, not-yet-full hole to aim for.
        /// </summary>
        bool HasViableMove()
        {
            for (int i = 0; i < m_runners.Count; i++)
            {
                var r = m_runners[i];
                if (r == null) continue;
                if (r.IsJumping) return true;
                for (int h = 0; h < m_holes.Count; h++)
                {
                    if (m_holes[h] != null && m_holes[h].CanAccept(r.Color)) return true;
                }
            }
            return false;
        }
    }
}
