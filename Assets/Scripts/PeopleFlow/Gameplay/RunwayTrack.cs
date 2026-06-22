using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace PeopleFlow
{
    public class RunwayTrack : MonoBehaviour
    {
        [SerializeField] int m_segments = 128;
        [Tooltip("World units one minion occupies along the loop, used to AUTO-calculate capacity " +
                 "(capacity = loopLength / this) when the level's autoRunwayCapacity is on. Smaller = a " +
                 "tighter pack / higher capacity. Minions run in RANKS (several abreast) packed " +
                 "nose-to-tail, so set this to roughly one rank's loop footprint so a 'full' runway " +
                 "lines up with the loop actually looking packed.")]
        [SerializeField] float m_minionArcFootprint = 0.4f;

        [Tooltip("Optional authored runway path. When set (here, or passed to Build by the LevelManager), " +
                 "the loop is SAMPLED from this Unity Spline instead of the generated math shape. Leave " +
                 "empty to use the level's TrackShape (oval / rectangle / custom waypoints).")]
        [SerializeField] SplineContainer m_splineContainer;

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

        public const float EntryT = 0f;

        public event Action<float> OnFillChanged;

        public float TotalLength => m_totalLength;
        public int Capacity => m_capacity;
        public int Count => m_runners.Count;
        public bool IsFull => m_runners.Count >= m_capacity;
        public float Fill => m_capacity > 0 ? (float)m_runners.Count / m_capacity : 0f;
        public IReadOnlyList<Hole> Holes => m_holes;

        public IReadOnlyList<Vector3> PathPoints => m_points;

        // ---- build ----------------------------------------------------------

        public void Build(LevelData level, SplineContainer spline = null)
        {
            // Capacity is computed below, once the loop length is known (see autoRunwayCapacity).
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

            // OPTION 1: sample the authored Unity Spline (path is now spline-driven). Falls back to a
            // SplineContainer serialized on this object if the caller didn't pass one.
            // OPTION 2: no usable spline → generate the path from the math shape (oval / rect / custom).
            if (!TryBuildFromSpline(spline != null ? spline : m_splineContainer))
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

            // Capacity = how many minions physically fit around the loop. When the level opts into
            // auto-capacity we derive it from the loop length (length / per-minion footprint), so a
            // bigger runway holds more before it's "full"; otherwise we use the hand-authored number.
            // Overfilling past this loses the level (see the over-capacity check in Update).
            m_capacity = level.autoRunwayCapacity && m_minionArcFootprint > 0.0001f
                ? Mathf.Max(1, Mathf.FloorToInt(m_totalLength / m_minionArcFootprint))
                : Mathf.Max(1, level.runwayCapacity);

            m_built = true;
            OnFillChanged?.Invoke(Fill); // runner list was just cleared → notify HUD the runway is empty
        }

        bool TryBuildFromSpline(SplineContainer container)
        {
            if (container == null || container.Splines == null || container.Splines.Count == 0) return false;
            var spline = container.Splines[0];
            if (spline == null || spline.Count < 2) return false;

            // A closed spline already wraps (t=1 == t=0), so sample i/n and let the arc-length table
            // close the last sample back to the first. An open spline includes its endpoint (i/(n-1));
            // the loop still closes with a final chord from endpoint back to start.
            bool closed = spline.Closed;
            int n = Mathf.Max(16, m_segments);
            m_points = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = closed ? i / (float)n : i / (float)(n - 1);
                Vector3 world = (Vector3)container.EvaluatePosition(t);
                m_points[i] = new Vector3(world.x, transform.position.y, world.z);
            }
            return true;
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

            // Overfill = instant fail: the player held a lane and pushed the runway past its capacity
            // (auto-derived from the loop length, see Build). Full is the brink; one more body over the
            // top loses immediately. Caught here every frame, so it fires the instant a release tips it
            // over.
            if (m_runners.Count > m_capacity)
            {
                GamePlayController.Instance.ReportRunwayJam();
                return;
            }

            // Backstop against a soft-lock: the runway sits exactly full and nothing on it can complete
            // any open hole (and nothing is in motion / being produced) — it can never drain, so fail.
            if (IsFull && !HasViableMove())
                GamePlayController.Instance.ReportRunwayJam();
        }

        bool HasViableMove()
        {
            for (int i = 0; i < m_factories.Count; i++)
                if (m_factories[i] != null && m_factories[i].IsProducing) return true;

            // A group still ASSEMBLING at the start is about to set off (its members stand idle at the
            // start until the group is full) — that's motion-in-waiting, not a deadlock.
            for (int i = 0; i < m_groups.Count; i++)
                if (m_groups[i] != null && m_groups[i].IsAssembling) return true;

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

        public float ForwardDistance(float fromT, float toT)
        {
            fromT = Mathf.Repeat(fromT, 1f);
            toT = Mathf.Repeat(toT, 1f);

            if (toT >= fromT)
                return toT - fromT;

            // Đã vòng qua vạch 1.0 -> 0.0
            return (1f - fromT) + toT;
        }

        public bool IsAnyGroupAssembling()
        {
            for (int i = 0; i < m_groups.Count; i++)
            {
                if (m_groups[i] != null && m_groups[i].IsAssembling) return true;
            }
            return false;
        }

        public bool IsEntryBlocked(float entryT, float safeDistT)
        {
            // 1) Kiểm tra group
            for (int i = 0; i < m_groups.Count; i++)
            {
                var g = m_groups[i];
                if (g == null || g.IsEmpty) continue;

                // a) Entry nằm bên trong body group (đuôi → đầu)?
                //    Nếu tailToEntry <= groupLen thì entry đang bị group đè lên.
                float groupLen = ForwardDistance(g.TailT, g.HeadT);
                float tailToEntry = ForwardDistance(g.TailT, entryT);
                if (groupLen > 0.001f && tailToEntry <= groupLen) return true;

                // b) Đuôi group vừa đi qua Entry? (chờ đuôi rời xa đủ trước khi launch)
                float entryToTail = ForwardDistance(entryT, g.TailT);
                if (entryToTail <= safeDistT) return true;

                // KHÔNG kiểm tra group đang tiến tới Entry (tailToEntry > groupLen).
                // CanGroupMoveForward đã tự dừng group khi gặp group assembling phía trước.
            }

            // 2) Kiểm tra solo runner (không thuộc group nào)
            for (int i = 0; i < m_runners.Count; i++)
            {
                var r = m_runners[i];
                if (r == null || r.IsJumping || r.IsPendingPeel || r.IsEnteringRoad || r.IsInGroup) continue;

                float distToEntry = ForwardDistance(r.TrackT, entryT);
                float distFromEntry = ForwardDistance(entryT, r.TrackT);

                if (distToEntry <= safeDistT || distFromEntry <= safeDistT)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanGroupMoveForward(RunnerGroup myGroup, float myHeadT, float safeDistT)
        {
            if (m_groups == null || m_groups.Count <= 1) return true;

            for (int i = 0; i < m_groups.Count; i++)
            {
                var other = m_groups[i];
                // Bỏ qua chính mình hoặc nhóm rỗng
                if (other == myGroup || other.IsEmpty) continue;

                float dist = ForwardDistance(myHeadT, other.TailT);

                if (dist > 0.001f && dist <= safeDistT)
                {
                    return false;
                }
            }
            return true;
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
