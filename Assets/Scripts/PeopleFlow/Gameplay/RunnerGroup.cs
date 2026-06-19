using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A released crowd: a single-colour block of runners that travel together as one rigid
    /// formation around the loop. The group owns ONE abstract head position along the track; every
    /// member sits in a fixed (row, col) grid slot measured back from the head (rows) and across the
    /// road width (columns), so the block hugs and bends around curves like a real crowd. Members hop
    /// on one-by-one while assembling, and peel into a matching hole one-by-one (a short stagger), but
    /// in between they run as a packed block instead of a single file.
    ///
    /// It is a plain object (no GameObject of its own), created by <see cref="Lane"/> and owned +
    /// ticked by <see cref="RunwayTrack"/>, so it inherits the track's build / pause / restart
    /// lifecycle for free and never leaks: <see cref="RunwayTrack.Build"/> drops every group, and its
    /// member <see cref="People"/> are destroyed with the rest of the level. Because the head is an
    /// abstract anchor (not a member), a member peeling off — even the front one — never breaks the
    /// formation.
    /// </summary>
    public class RunnerGroup
    {
        class Member
        {
            public People person;
            public int row, col;
            public bool entered;       // finished its entry hop; now driven in formation
            public float blend;        // 0..1 ease from the entry landing to the live slot
            public float prevT;        // loop position last tick (for hole-crossing tests)
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
        float m_headT;
        float m_peelCooldown;          // counts down between peels (keeps hole entry one-by-one)

        public PeopleColor Color => m_color;
        public bool IsEmpty => m_members.Count == 0;

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
            p.SetGroup(this);
        }

        /// <summary>World slot for (<paramref name="row"/>, <paramref name="col"/>) at the head's
        /// current position — used by an entering member to aim its hop.</summary>
        public Vector3 SlotWorldNow(int row, int col, out Quaternion rot)
        {
            float t = Mathf.Repeat(m_headT - row * m_rowArc, 1f);
            return SlotWorld(t, col, out rot);
        }

        /// <summary>An entry hop has landed: start driving this member in formation, easing it from
        /// where it landed to its live slot over a short blend.</summary>
        public void NotifyEntered(People p)
        {
            var m = Find(p);
            if (m == null) return;
            m.entered = true;
            m.blend = 0f;
            m.prevT = Mathf.Repeat(m_headT - m.row * m_rowArc, 1f);
        }

        // ---- per-frame, driven by RunwayTrack.Update ------------------------

        public void Tick(float dt)
        {
            m_members.RemoveAll(m => m.person == null); // drop destroyed members defensively
            if (m_members.Count == 0 || m_track == null || m_track.TotalLength <= 0.01f) return;

            // 1) Advance the single shared head anchor. Speed is sampled at the head ONLY, so the whole
            //    block accelerates as one through an arrow zone instead of shearing when only its front
            //    row has entered the zone.
            float mult = m_track.SpeedMultiplierAt(m_headT);
            m_headT = Mathf.Repeat(m_headT + m_speed * mult / m_track.TotalLength * dt, 1f);

            if (m_peelCooldown > 0f) m_peelCooldown -= dt;

            // 2) Pose every entered member; scan unreserved ones for a matching hole. A reserved
            //    (pending) member keeps flowing with the crowd — it does NOT stop — until its peel turn.
            for (int i = 0; i < m_members.Count; i++)
            {
                var m = m_members[i];
                if (!m.entered) continue; // still hopping on; the group doesn't drive it yet

                float t = Mathf.Repeat(m_headT - m.row * m_rowArc, 1f);

                if (m.pendingHole == null)
                    ScanHoles(m, m.prevT, t);

                Vector3 pos = SlotWorld(t, m.col, out Quaternion rot);
                if (m.blend < 1f)
                {
                    // Ease out the small residual between where the hop landed and the live (moving)
                    // slot, so a member joins smoothly instead of snapping forward.
                    m.blend = Mathf.Min(1f, m.blend + dt / EntryBlend);
                    pos = Vector3.Lerp(m.person.transform.position, pos, m.blend);
                }
                m.person.SetGroupPose(pos, rot);

                m.prevT = t;
            }

            // 3) Let at most one reserved member drop into its hole per stagger window, so a whole
            //    single-colour row that crossed the hole on the same frame still enters one-by-one.
            if (m_peelCooldown <= 0f) TryPeelOne();
        }

        void ScanHoles(Member m, float oldT, float newT)
        {
            var holes = m_track.Holes;
            for (int i = 0; i < holes.Count; i++)
            {
                var hole = holes[i];
                if (hole == null) continue;

                if (WrapDistance(newT, hole.TrackT) < RevealWindow)
                    hole.RevealIfHidden();

                if (PassedForward(oldT, newT, hole.TrackT) && hole.CanAccept(m_color) && hole.TryReserve(m_color))
                {
                    m.pendingHole = hole; // reserved now; the member hops in when its peel turn comes
                    m.person.MarkPendingPeel();
                    return;
                }
            }
        }

        void TryPeelOne()
        {
            for (int i = 0; i < m_members.Count; i++)
            {
                var m = m_members[i];
                if (!m.entered || m.pendingHole == null || m.person == null) continue;

                Hole hole = m.pendingHole;
                m_members.RemoveAt(i);
                m.person.PeelIntoHole(hole);   // detaches, hops in, commits, despawns
                m_peelCooldown = m_peelStagger;
                return;
            }
        }

        // ---- formation math -------------------------------------------------

        /// <summary>World position of a slot at loop position <paramref name="t"/> and column
        /// <paramref name="col"/>: the centreline point offset sideways (perpendicular to travel) so
        /// the column sits across the road, centred on the lane. Faces travel direction.</summary>
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
    }
}
