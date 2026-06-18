using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A waiting queue of coloured characters at the bottom of the screen. The queued characters
    /// stand on the lane in single-colour groups — each group a horizontal row, groups stepping back
    /// from the tray. While the player holds on this lane, the front group hops onto the runway every
    /// <c>releaseInterval</c> seconds (subject to runway capacity, spacing and an optional barrier)
    /// and the remaining groups slide forward. Group size is editor-configurable.
    /// </summary>
    public class Lane : MonoBehaviour
    {
        const float MinEntrySpacing = 0.95f; // don't release if a runner is still this close to entry

        [Tooltip("Minimum seconds between group releases while held. This is a floor: the actual " +
                 "cadence is also bound by the hop time and entry spacing.")]
        [SerializeField] float m_releaseInterval = 0.22f;
        [Tooltip("Max minions in one group. Groups are single-colour: a tap/hold releases one colour " +
                 "block at a time (or this many of it, whichever is smaller).")]
        [Min(1)]
        [SerializeField] int m_groupSize = 3;
        [Tooltip("Horizontal gap (world units) between minions standing side-by-side within a group.")]
        [SerializeField] float m_memberSpacing = 0.5f;
        [Tooltip("Depth gap (world units) between consecutive groups in the waiting line.")]
        [SerializeField] float m_groupSpacing = 0.9f;
        [Tooltip("Seconds between consecutive members of a released group hopping onto the road, so a " +
                 "group enters as an ordered wave instead of all members leaping at once. 0 = simultaneous.")]
        [SerializeField] float m_memberLaunchStagger = 0.08f;
        [Tooltip("Reverse the per-member hop order from left-to-right to right-to-left.")]
        [SerializeField] bool m_releaseRightToLeft = false;
        [Tooltip("Extra height above the tray top to stand the waiting minions at (0 = right on the tray).")]
        [SerializeField] float m_previewStandOffset = 0f;

        readonly List<People> m_previews = new List<People>(); // waiting minions; index 0 = front, grouped by GroupSize
        RunwayTrack m_track;
        MaterialLibrary m_mats;
        Transform m_runnersRoot;
        GameObject m_characterPrefab;
        float m_runSpeed;
        Vector3 m_entryPos;
        Vector3 m_backDir = Vector3.back;   // queue depth direction (away from the road)
        Vector3 m_rightDir = Vector3.right; // horizontal direction a group's members spread along
        float m_standY;                      // world Y the waiting minions stand at (tray top)

        float m_timer;
        bool m_wasHeld;

        // Barrier (section 7)
        bool m_barrier;
        int m_unlockAfter;
        int m_observedCompleted;
        GameObject m_barrierVisual;

        /// <summary>Set by <see cref="InputManager"/> every frame: is the player holding this lane?</summary>
        public bool IsHeld { get; set; }

        public bool Barriered => m_barrier && m_observedCompleted < m_unlockAfter;
        public int RemainingCount => m_previews.Count;
        int GroupSize => Mathf.Max(1, m_groupSize);

        // ---- setup ----------------------------------------------------------

        public void Setup(LaneSetup setup, RunwayTrack track, MaterialLibrary mats,
            Transform runnersRoot, GameObject characterPrefab, float runSpeed, Vector3 padPosition)
        {
            m_track = track;
            m_mats = mats;
            m_runnersRoot = runnersRoot;
            m_characterPrefab = characterPrefab;
            m_runSpeed = runSpeed;
            m_entryPos = track.Evaluate(RunwayTrack.EntryT);

            // Group size comes from the level data so it matches how the supply was dealt (whole
            // single-colour groups); fall back to the prefab's serialized value if unset.
            if (setup.groupSize > 0) m_groupSize = setup.groupSize;

            m_barrier = setup.barrier;
            m_unlockAfter = setup.unlockAfterHolesCompleted;

            transform.position = padPosition;
            BindVisuals();

            // The waiting line runs straight back from the tray, away from the road. All lanes share
            // one entry point (x = 0) but sit at different x, so drop the x component — otherwise an
            // off-centre lane's line would angle diagonally toward the entry instead of staying in
            // its own column.
            Vector3 back = padPosition - m_entryPos;
            back.x = 0f;
            back.y = 0f;
            m_backDir = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;

            // Horizontal axis a group's members spread along (perpendicular to the depth direction).
            m_rightDir = Vector3.Cross(Vector3.up, m_backDir);
            m_rightDir = m_rightDir.sqrMagnitude > 1e-4f ? m_rightDir.normalized : Vector3.right;

            SpawnPreviews(setup.characters);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GameManager.Instance.CompletedHoles;
                RefreshBarrierVisual();
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHoleProgress -= OnHoleProgress;
        }

        // ---- waiting line ---------------------------------------------------

        void SpawnPreviews(List<PeopleColor> colors)
        {
            if (colors == null) return;
            // Cluster identical colours together (stable by first appearance) so every waiting group is
            // a single colour. Round-robin dealt lanes interleave colours, which would otherwise put
            // mixed colours in one group.
            var clustered = ClusterByColor(colors);
            Quaternion faceRoad = FaceRoadRotation();
            for (int i = 0; i < clustered.Count; i++)
            {
                // Parent previews under this lane so the waiting line belongs to (and stays on) it.
                // On release they're re-parented to the shared runners root (see ReleaseGroup).
                // Spawn at the lane origin; ReflowPreviews(snap) immediately places it in its slot.
                var p = People.SpawnPreview(m_characterPrefab, clustered[i], m_mats, transform, transform.position, faceRoad);
                if (p != null) m_previews.Add(p); // a missing/invalid prefab is already logged by SpawnPreview
            }
            ReflowPreviews(snap: true);
        }

        /// <summary>Reorder colours so identical colours are contiguous, preserving the order in which
        /// each colour first appears. Turns an interleaved queue (R,G,B,R,G,B) into colour blocks
        /// (R,R,G,G,B,B) so groups end up single-coloured.</summary>
        static List<PeopleColor> ClusterByColor(List<PeopleColor> colors)
        {
            var order = new List<PeopleColor>();       // colours by first appearance
            var counts = new Dictionary<PeopleColor, int>();
            foreach (var c in colors)
            {
                if (counts.TryGetValue(c, out int n)) counts[c] = n + 1;
                else { counts[c] = 1; order.Add(c); }
            }

            var result = new List<PeopleColor>(colors.Count);
            foreach (var c in order)
                for (int i = 0; i < counts[c]; i++) result.Add(c);
            return result;
        }

        /// <summary>World position for the <paramref name="member"/>-th minion of the
        /// <paramref name="group"/>-th waiting group, a horizontal row of <paramref name="groupCount"/>
        /// centred on the lane; groups step back from the tray.</summary>
        Vector3 SlotPos(int group, int member, int groupCount)
        {
            float centered = member - (groupCount - 1) * 0.5f; // centre the row on the lane

            Vector3 p = transform.position;
            p.y = m_standY;
            return p + m_backDir * (group * m_groupSpacing) + m_rightDir * (centered * m_memberSpacing);
        }

        /// <summary>Number of minions in the front (next-to-release) group: the leading run of one
        /// colour, capped at <see cref="GroupSize"/>. 0 if the queue is empty.</summary>
        int FrontGroupCount()
        {
            m_previews.RemoveAll(p => p == null);
            if (m_previews.Count == 0) return 0;
            PeopleColor c = m_previews[0].Color;
            int n = 0;
            while (n < m_previews.Count && n < GroupSize && m_previews[n].Color == c) n++;
            return n;
        }

        Quaternion FaceRoadRotation()
        {
            // Face along the lane's own column toward the road (the opposite of the queue direction),
            // so the whole line faces one way instead of angling toward the shared central entry.
            Vector3 fwd = -m_backDir;
            return fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd) : transform.rotation;
        }

        /// <summary>Lay the waiting line out as single-colour groups: a new group starts on each colour
        /// change or once a group reaches <see cref="GroupSize"/>. Sets each preview's slide target
        /// (and snaps its position too on first spawn).</summary>
        void ReflowPreviews(bool snap = false)
        {
            m_previews.RemoveAll(p => p == null);

            int i = 0, group = 0;
            while (i < m_previews.Count)
            {
                PeopleColor c = m_previews[i].Color;
                int count = 0;
                while (i + count < m_previews.Count && count < GroupSize && m_previews[i + count].Color == c)
                    count++;

                for (int m = 0; m < count; m++)
                {
                    var p = m_previews[i + m];
                    Vector3 pos = SlotPos(group, m, count);
                    p.PreviewTarget = pos;
                    if (snap) p.transform.position = pos;
                }

                i += count;
                group++;
            }
        }

        // ---- release loop ---------------------------------------------------

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                m_wasHeld = false;
                return;
            }

            if (IsHeld && !m_wasHeld) m_timer = 0f; // first push fires instantly
            m_wasHeld = IsHeld;

            if (!IsHeld) return;
            m_timer -= Time.deltaTime;
            if (m_timer > 0f) return;

            if (CanRelease())
            {
                ReleaseGroup();
                m_timer = m_releaseInterval;
            }
        }

        bool CanRelease()
        {
            if (m_previews.Count == 0 || Barriered) return false;

            // Only release if the whole front (single-colour) group will fit on the runway. Cap by
            // capacity so a colour block larger than the runway can still drain (in capacity-sized
            // chunks) instead of dead-locking.
            int groupCount = Mathf.Min(FrontGroupCount(), m_track.Capacity);
            if (groupCount <= 0) return false;
            if (m_track.Count + groupCount > m_track.Capacity) return false;

            // Every landing slot the group will occupy must be clear (no runner within one body-length),
            // so a new group never lands on the previous group's still-advancing members.
            float gap = GroupEntryGap();
            for (int k = 0; k < groupCount; k++)
            {
                Vector3 landing = m_track.Evaluate(RunwayTrack.EntryT + k * gap);
                if (m_track.HasRunnerNear(landing, MinEntrySpacing)) return false;
            }
            return true;
        }

        void ReleaseGroup()
        {
            m_previews.RemoveAll(p => p == null); // drop any destroyed entries defensively
            if (m_previews.Count == 0) return;

            int count = Mathf.Min(FrontGroupCount(), m_track.Capacity);
            if (count <= 0) return;
            // Stagger the group's landing points one body-length apart along the loop so the members
            // enter single-file from the bottom-centre entry instead of stacking on one spot.
            float gap = GroupEntryGap();
            for (int k = 0; k < count; k++)
            {
                var member = m_previews[0];
                m_previews.RemoveAt(0);
                // Hand the runner off the lane to the shared runners root so it isn't moved by (or
                // tied to) the lane once it's running the loop.
                if (m_runnersRoot != null) member.transform.SetParent(m_runnersRoot, true);
                // m_previews[0] is the group's leftmost member, so launching by ascending k gives a
                // left-to-right wave; reversing the delay order flips it to right-to-left.
                int order = m_releaseRightToLeft ? (count - 1 - k) : k;
                member.LaunchToRoad(m_track, RunwayTrack.EntryT + k * gap, m_runSpeed, order * m_memberLaunchStagger);
            }

            AudioManager.Instance?.PlayPush();
            ReflowPreviews();
        }

        /// <summary>Normalised loop distance between consecutive group members (one body-length).</summary>
        float GroupEntryGap() => m_track.TotalLength > 0.01f ? MinEntrySpacing / m_track.TotalLength : 0.02f;

        // ---- barrier --------------------------------------------------------

        void OnHoleProgress(int completed, int total)
        {
            m_observedCompleted = completed;
            RefreshBarrierVisual();
        }

        void RefreshBarrierVisual()
        {
            if (m_barrierVisual != null) m_barrierVisual.SetActive(Barriered);
        }

        // ---- visuals (bound to the drag-and-drop Lane prefab) ---------------

        void BindVisuals()
        {
            EnsureCollider();
            m_standY = ComputeStandY();

            // Use a named child as the barrier/blocked indicator if the art provides one.
            var bar = Prim.FindDescendant(transform, "Barrier", "Frozen", "Gate", "Lock");
            if (bar != null) m_barrierVisual = bar.gameObject;
            RefreshBarrierVisual(); // hide it unless this lane is actually barriered
        }

        /// <summary>World Y the waiting minions stand at: the top of the "Tray" child if present,
        /// else the top of the lane's renderers, plus the designer-tunable offset.</summary>
        float ComputeStandY()
        {
            var tray = Prim.FindDescendant(transform, "Tray");
            var trayRend = tray != null ? tray.GetComponent<Renderer>() : null;
            if (trayRend != null) return trayRend.bounds.max.y + m_previewStandOffset;

            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return transform.position.y + m_previewStandOffset;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.max.y + m_previewStandOffset;
        }

        /// <summary>
        /// InputManager raycasts the pointer at lanes, so the prefab needs a collider. If the art
        /// prefab doesn't ship one, add a box sized to its renderers (falling back to a sensible
        /// footprint) so taps still register.
        /// </summary>
        void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null) return;

            var box = gameObject.AddComponent<BoxCollider>();
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds wb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) wb.Encapsulate(rends[i].bounds);
                box.center = transform.InverseTransformPoint(wb.center);
                // Convert world size to local; clamp the divisor so a tiny scale can't blow up the box.
                Vector3 ls = transform.lossyScale;
                box.size = new Vector3(
                    wb.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                    wb.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                    wb.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            }
            else
            {
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(1.2f, 0.6f, 1.2f);
            }
        }
    }
}
