using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    public class RunnerGroup
    {
        class Member
        {
            public People person;
            public int row, col;
            public bool entered;       // finished its entry hop; now driven in formation
            public float blend;        // 0..1 ease from the entry landing to the live slot
            public float prevT;        // loop position last tick (for hole-crossing tests)
            public float curT;         // loop position this tick (set while posing, used for crossing/proximity)
            public Hole pendingHole;   // reserved a hole's slot; waiting its turn to peel in one-by-one
        }

        const float RevealWindow = 0.06f;   // matches People: reveal a hidden hole when this close
        const float EntryBlend = 0.15f;     // seconds to ease a freshly-landed member into its slot

        readonly RunwayTrack m_track;
        readonly PeopleColor m_color;
        readonly float m_speed;
        readonly int m_cols;
        readonly float m_colSpacing;   // world units between columns
        readonly float m_rowArc;       // normalised loop distance between consecutive rows
        readonly float m_peelStagger;  // min seconds between consecutive members dropping into a hole

        readonly List<Member> m_members = new List<Member>();
        readonly List<Member> m_crossers = new List<Member>(); // scratch: members crossing a hole this frame
        float m_headT;
        float m_peelCooldown;          // counts down between peels (keeps hole entry one-by-one)
        int m_addedCount;              // how many members were ever handed to this group (see AllEntered)
        int m_enteredCount;            // how many members have finished their entry hop (launch sequencing)

        public PeopleColor Color => m_color;
        public bool IsEmpty => m_members.Count == 0;

        public float HeadT => m_headT;

        public int EnteredCount => m_enteredCount;

        public bool AllEntered => m_enteredCount >= m_addedCount;

        public bool IsAssembling => m_enteredCount < m_addedCount;

        public RunnerGroup(RunwayTrack track, PeopleColor color, float speed, int cols,
            float colSpacing, float rowArc, float peelStagger, float headStartT)
        {
            m_track = track;
            m_color = color;
            m_speed = speed;
            m_cols = Mathf.Max(1, cols);
            m_colSpacing = colSpacing;
            m_rowArc = rowArc;
            m_peelStagger = Mathf.Max(0f, peelStagger);
            m_headT = Mathf.Repeat(headStartT, 1f);
        }

        // ---- membership -----------------------------------------------------

        public void AddMember(People p, int row, int col)
        {
            if (p == null) return;
            m_members.Add(new Member { person = p, row = row, col = col });
            m_addedCount++; // tracked separately from m_members (which shrinks as members peel) for AllEntered
            p.SetGroup(this);
        }

        public Vector3 SlotWorldNow(int row, int col, out Quaternion rot)
        {
            float t = Mathf.Repeat(m_headT - row * m_rowArc, 1f);
            return SlotWorld(t, col, out rot);
        }

        public void NotifyEntered(People p)
        {
            var m = Find(p);
            if (m == null) return;
            m.entered = true;
            m.blend = 0f;
            m.prevT = Mathf.Repeat(m_headT - m.row * m_rowArc, 1f);
            m_enteredCount++; // releases the next member waiting its turn to hop on (see EnteredCount)
        }

        // ---- per-frame, driven by RunwayTrack.Update ------------------------

        public void Tick(float dt)
        {
            // Drop destroyed members defensively. If a destroyed member still held a hole reservation,
            // release it first — otherwise the hole stays falsely over-reserved (CanAccept goes false
            // forever) and can never be completed.
            m_members.RemoveAll(m =>
            {
                if (m.person != null) return false;
                if (m.pendingHole != null) m.pendingHole.CancelReserve();
                // A member destroyed before it ever boarded must not block AllEntered forever (which
                // would freeze the whole group at the start) — forget it was expected.
                if (!m.entered) m_addedCount--;
                return true;
            });
            if (m_members.Count == 0 || m_track == null || m_track.TotalLength <= 0.01f) return;

            // 1) Advance the single shared head anchor — but ONLY once the whole group has assembled at
            //    the start. While members are still hopping in, the block HOLDS at the start area so it
            //    sets off together as a complete group (see AllEntered) instead of dragging half-formed.
            //    Speed is sampled at the head ONLY, so the whole block accelerates as one through an
            //    arrow zone instead of shearing when only its front row has entered the zone.
            if (AllEntered)
            {
                // Khoảng cách an toàn tối thiểu giữa đầu nhóm này và đuôi nhóm kia 
                // (Gấp đôi khoảng cách hàng để giữ cự ly đẹp)
                float safeDistT = m_rowArc * 2f;

                // Groups luôn chạy liên tục (giống People Loop gốc).
                // Chỉ dừng khi sắp đâm vào đuôi nhóm phía trước.
                if (m_track.CanGroupMoveForward(this, m_headT, safeDistT))
                {
                    float mult = m_track.SpeedMultiplierAt(m_headT);
                    m_headT = Mathf.Repeat(m_headT + m_speed * mult / m_track.TotalLength * dt, 1f);
                }
                // (Nếu bị chặn bởi nhóm phía trước, m_headT không tăng -> cả nhóm tự động đứng im)
            }

            if (m_peelCooldown > 0f) m_peelCooldown -= dt;

            // 2) Pose every entered member at its (row, col) slot, recording this frame's loop position.
            //    A reserved (pending) member keeps flowing with the crowd — it does NOT stop — until its
            //    peel turn, but its turn is decided by distance to the hole, not its column order.
            for (int i = 0; i < m_members.Count; i++)
            {
                var m = m_members[i];
                if (!m.entered) continue; // still hopping on; the group doesn't drive it yet

                float t = Mathf.Repeat(m_headT - m.row * m_rowArc, 1f);
                m.curT = t; // used by this frame's crossing + proximity tests below

                Vector3 pos = SlotWorld(t, m.col, out Quaternion rot);
                if (m.blend < 1f)
                {
                    // Ease out the small residual between where the hop landed and the live (moving)
                    // slot, so a member joins smoothly instead of snapping forward.
                    m.blend = Mathf.Min(1f, m.blend + dt / EntryBlend);
                    pos = Vector3.Lerp(m.person.transform.position, pos, m.blend);
                }
                m.person.SetGroupPose(pos, rot);
            }

            // 3) As the rank sweeps over a matching hole, reserve its open slots for the members
            //    physically CLOSEST to it — not whoever happens to sit first in column order.
            ReserveCrossedHoles();

            // 4) Drop the reserved member NEAREST its hole first, one per stagger window — so a whole
            //    single-colour rank that crossed the hole on one frame still enters one-by-one, but the
            //    closest person leads instead of following the column order.
            if (m_peelCooldown <= 0f) TryPeelClosest();

            // 5) Roll this frame's loop positions forward for next frame's crossing tests.
            for (int i = 0; i < m_members.Count; i++)
                if (m_members[i].entered) m_members[i].prevT = m_members[i].curT;


        }

        void ReserveCrossedHoles()
        {
            var holes = m_track.Holes;
            for (int h = 0; h < holes.Count; h++)
            {
                var hole = holes[h];
                if (hole == null) continue;

                // Reveal a hidden colour the moment any entered member runs close to it.
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (m.entered && WrapDistance(m.curT, hole.TrackT) < RevealWindow)
                    {
                        hole.RevealIfHidden();
                        break;
                    }
                }

                if (!hole.CanAccept(m_color)) continue;

                // Gather the still-unreserved members that swept across this hole this frame.
                m_crossers.Clear();
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (m.entered && m.pendingHole == null && m.person != null
                        && PassedForward(m.prevT, m.curT, hole.TrackT))
                        m_crossers.Add(m);
                }
                if (m_crossers.Count == 0) continue;

                // Hand the hole's free slots to the nearest crossers first.
                SortByDistanceTo(m_crossers, hole.JumpTarget);
                for (int i = 0; i < m_crossers.Count; i++)
                {
                    if (!hole.TryReserve(m_color)) break; // hole is now full
                    var m = m_crossers[i];
                    m.pendingHole = hole;       // reserved now; it peels in when it's the closest pending
                    m.person.MarkPendingPeel();
                }
            }
        }

        void TryPeelClosest()
        {
            Member best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < m_members.Count; i++)
            {
                var m = m_members[i];
                if (!m.entered || m.pendingHole == null || m.person == null) continue;

                float d = (m.person.transform.position - m.pendingHole.JumpTarget).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = m; }
            }
            if (best == null) return;

            Hole hole = best.pendingHole;
            m_members.Remove(best);
            best.person.PeelIntoHole(hole);   // detaches, hops in, commits, despawns
            m_peelCooldown = m_peelStagger;
        }

        static void SortByDistanceTo(List<Member> list, Vector3 target)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var m = list[i];
                float d = (m.person.transform.position - target).sqrMagnitude;
                int j = i - 1;
                while (j >= 0 && (list[j].person.transform.position - target).sqrMagnitude > d)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = m;
            }
        }

        // ---- formation math -------------------------------------------------

        Vector3 SlotWorld(float t, int col, out Quaternion rot)
        {
            Vector3 center = m_track.Evaluate(t, out Vector3 fwd);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude > 1e-6f) right.Normalize();

            float centered = col - (m_cols - 1) * 0.5f;
            Vector3 pos = center + right * (centered * m_colSpacing);
            pos.y = center.y; // the track is Y-flattened in Build; keep the block flat

            rot = fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd) : Quaternion.identity;
            return pos;
        }

        Member Find(People p)
        {
            for (int i = 0; i < m_members.Count; i++)
                if (m_members[i].person == p) return m_members[i];
            return null;
        }

        // ---- loop helpers (mirror People's so the group detects holes identically) ----

        static bool PassedForward(float oldT, float newT, float target)
        {
            if (Mathf.Approximately(oldT, newT)) return false;
            if (newT >= oldT) return target > oldT && target <= newT;
            return target > oldT || target <= newT; // wrapped past 1.0 → 0.0
        }

        static float WrapDistance(float a, float b)
        {
            float d = Mathf.Abs(Mathf.Repeat(a - b, 1f));
            return Mathf.Min(d, 1f - d);
        }
        public float TailT
        {
            get
            {
                if (m_members.Count == 0) return m_headT;
                int maxRow = 0;
                for (int i = 0; i < m_members.Count; i++)
                {
                    if (m_members[i].row > maxRow) maxRow = m_members[i].row;
                }
                return Mathf.Repeat(m_headT - maxRow * m_rowArc, 1f);
            }
        }
    }
}
