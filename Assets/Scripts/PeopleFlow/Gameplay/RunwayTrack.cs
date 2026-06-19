using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// The closed loop runway. Builds an oval polyline from <see cref="LevelData"/> or a provided LineRenderer, 
    /// exposes constant-speed positions along it (arc-length parameterised), tracks how full it is, and
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
        readonly List<RunnerGroup> m_groups = new List<RunnerGroup>();  // crowd blocks the track drives

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
        /// first). Visuals (the LineRenderer) render these directly so they match whatever built the path.</summary>
        public IReadOnlyList<Vector3> PathPoints => m_points;

        // ---- build ----------------------------------------------------------

        /// <summary>
        /// Builds the track logic. If a visualTrack is provided, it extracts the exact points. 
        /// Otherwise, it falls back to generating the path mathematically from the LevelData.
        /// </summary>
        public void Build(LevelData level, LineRenderer visualTrack = null)
        {
            m_capacity = Mathf.Max(1, level.runwayCapacity);
            m_arrows = level.arrows != null ? new List<ArrowSetup>(level.arrows) : new List<ArrowSetup>();

            // Reset the per-level dynamic registries. The track is a persistent scene object reused
            // across restarts / next-level, so without this a rebuild inherits the previous attempt's
            // (now-destroyed) runners, holes and factories as stale entries — inflating Count/Fill,
            // making CanRelease see a phantom-full runway (lanes won't launch), and misfiring the jam
            // (lose) check. Clearing here, before factories/holes/runners re-register, fixes restart.
            m_runners.Clear();
            m_holes.Clear();
            m_factories.Clear();
            m_groups.Clear();

            // OPTION 1: Build from a provided LineRenderer
            if (visualTrack != null && visualTrack.positionCount >= 2)
            {
                int n = visualTrack.positionCount;
                m_points = new Vector3[n];

                Vector3[] linePoints = new Vector3[n];
                visualTrack.GetPositions(linePoints);

                for (int i = 0; i < n; i++)
                {
                    Vector3 worldPos = visualTrack.useWorldSpace
                        ? linePoints[i]
                        : visualTrack.transform.TransformPoint(linePoints[i]);

                    // Flatten Y so minions don't float
                    m_points[i] = new Vector3(worldPos.x, transform.position.y, worldPos.z);
                }
            }
            // OPTION 2: Fallback to manual math path generation
            else
            {
                var local = TrackPath.Build(level, Mathf.Max(16, m_segments));
                int n = local.Count;
                m_points = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    m_points[i] = transform.TransformPoint(new Vector3(local[i].x, 0f, local[i].z));
                }
            }

            // Shared Logic: Calculate arc lengths so runners move smoothly
            int pointCount = m_points.Length;
            m_cumulative = new float[pointCount + 1];
            float acc = 0f;
            for (int i = 0; i < pointCount; i++)
            {
                acc += Vector3.Distance(m_points[i], m_points[(i + 1) % pointCount]);
                m_cumulative[i + 1] = acc;
            }

            m_totalLength = acc;
            m_segCount = pointCount;
            m_built = true;
            OnFillChanged?.Invoke(Fill); // runner list was just cleared → notify HUD the runway is empty
        }

        public void RegisterHole(Hole h)
        {
            if (h != null && !m_holes.Contains(h)) m_holes.Add(h);
        }

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

        /// <summary>Create a crowd block that travels as one rigid formation. The track owns and ticks
        /// it (see <see cref="Update"/>), so it freezes on pause/win/lose and is dropped on rebuild;
        /// its members register individually via their own entry, so capacity and jam detection still
        /// see every runner.</summary>
        public RunnerGroup CreateGroup(PeopleColor color, float speed, int cols, float colSpacing,
            float rowArc, float peelStagger, float headStartT)
        {
            var g = new RunnerGroup(this, color, speed, cols, colSpacing, rowArc, peelStagger, headStartT);
            m_groups.Add(g);
            return g;
        }

        // ---- jam detection (lose authority) --------------------------------

        void Update()
        {
            if (!m_built || GamePlayController.Instance == null || !GamePlayController.Instance.IsGamePlaying)
                return;

            // Drive every crowd block as one rigid formation, and prune emptied ones. Done here (not in
            // each group's own Update) so groups inherit this gated heartbeat: frozen on pause/win/lose.
            for (int i = m_groups.Count - 1; i >= 0; i--)
            {
                m_groups[i].Tick(Time.deltaTime);
                if (m_groups[i].IsEmpty) m_groups.RemoveAt(i);
            }

            // Over capacity: the player overfed the runway past its limit — fail the level.
            if (m_runners.Count > m_capacity)
            {
                GamePlayController.Instance.ReportRunwayJam();
                return;
            }

            if (IsFull && !HasViableMove())
                GamePlayController.Instance.ReportRunwayJam();
        }

        bool HasViableMove()
        {
            for (int i = 0; i < m_factories.Count; i++)
                if (m_factories[i] != null && m_factories[i].IsProducing) return true;

            for (int i = 0; i < m_runners.Count; i++)
            {
                var r = m_runners[i];
                if (r == null) continue;
                if (r.IsJumping || r.IsEnteringRoad || r.IsPendingPeel) return true; // mid-motion / about to drop in
                for (int h = 0; h < m_holes.Count; h++)
                {
                    if (m_holes[h] != null && m_holes[h].CanAccept(r.Color)) return true;
                }
            }
            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!m_built || m_points == null || m_points.Length == 0) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < m_points.Length; i++)
            {
                Vector3 current = m_points[i];
                Vector3 next = m_points[(i + 1) % m_points.Length];
                Gizmos.DrawLine(current, next);
            }
        }
#endif
    }
}