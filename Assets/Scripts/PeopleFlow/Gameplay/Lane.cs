using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    public class Lane : MonoBehaviour
    {
        // Don't release if a runner is still this close to entry. Public + const because RunwayTrack
        // derives the runway's rank-packing capacity from the same spacing (2 * this per rank), so a
        // "full" bar matches how the groups actually pack — single source of truth for that geometry.
        public const float MinEntrySpacing = 0.95f;

        [Tooltip("Minimum seconds between group releases while held. This is a floor: the actual " +
                 "cadence is also bound by the hop time and entry spacing.")]
        [SerializeField] float m_releaseInterval = 0.22f;
        [Tooltip("Max minions in one group. Groups are single-colour: a tap/hold releases one colour " +
                 "block at a time (or this many of it, whichever is smaller).")]
        [Min(1)]
        [SerializeField] int m_groupSize = 4;
        [Tooltip("Horizontal gap (world units) between minions standing side-by-side within a group.")]
        [SerializeField] float m_memberSpacing = 0.5f;
        [Tooltip("Depth gap (world units) between consecutive groups in the waiting line.")]
        [SerializeField] float m_groupSpacing = 0.9f;
        [Tooltip("Min seconds between consecutive members of a group dropping into a HOLE (peel stagger), " +
                 "so a matching rank enters the hole one-by-one. Note: members hop ONTO the road strictly " +
                 "sequentially (each waits for the one ahead to land), so this no longer paces road entry.")]
        [SerializeField] float m_memberLaunchStagger = 0.08f;
        [Tooltip("Reverse the per-member hop order from left-to-right to right-to-left.")]
        [SerializeField] bool m_releaseRightToLeft = false;
        [Tooltip("Extra height above the tray top to stand the waiting minions at (0 = right on the tray).")]
        [SerializeField] float m_previewStandOffset = 0f;

        [SerializeField] Transform m_previewHolder = null;
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
        RunnerGroup m_lastGroup;   // the most recent group this lane launched; gates next-group spacing

        bool m_barrier;
        int m_unlockAfter;
        int m_observedCompleted;
        GameObject m_barrierVisual;

        public bool IsHeld { get; set; }

        public bool Barriered => m_barrier && m_observedCompleted < m_unlockAfter;
        public int RemainingCount => m_previews.Count;
        int GroupSize => Mathf.Max(1, m_groupSize);

        // ---- setup ----------------------------------------------------------

        public void Setup(LaneSetup setup, List<PeopleColor> supply, RunwayTrack track, MaterialLibrary mats,
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

            m_backDir = Vector3.back;

            m_rightDir = Vector3.right;

            SpawnPreviews(supply);

            if (GamePlayController.Instance != null)
            {
                GamePlayController.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GamePlayController.Instance.CompletedHoles;
                RefreshBarrierVisual();
            }
        }

        void OnDestroy()
        {
            if (GamePlayController.Instance != null)
                GamePlayController.Instance.OnHoleProgress -= OnHoleProgress;
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
                var p = People.SpawnPreview(m_characterPrefab, clustered[i], m_mats, m_previewHolder, m_previewHolder.position, faceRoad);
                if (p != null) m_previews.Add(p);
            }
            ReflowPreviews(snap: true);
        }

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

        Vector3 SlotPos(int group, int member, int groupCount)
        {
            float centered = member - (groupCount - 1) * 0.5f; // centre the row on the lane

            Vector3 p = transform.position;
            p.y = m_standY;
            return p + m_backDir * (group * m_groupSpacing) + m_rightDir * (centered * m_memberSpacing);
        }

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
            return Quaternion.LookRotation(Vector3.forward);
        }

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
            if (GamePlayController.Instance == null || !GamePlayController.Instance.IsGamePlaying)
            {
                m_wasHeld = false;
                return;
            }

            if (IsHeld && !m_wasHeld) m_timer = 0f; // first push fires instantly
            m_wasHeld = IsHeld;

            if (!IsHeld) return;

            // NOTE: a runway-full FAIL has two triggers. (1) ACTIVE overfill, here: while you hold a
            // lane, trying to launch a group that won't fit on the runway fails the level — pushing
            // more onto a full runway IS the lose. (2) PASSIVE deadlock, in RunwayTrack: a full runway
            // that can't drain (while no lane is pushing) fails too. Capacity is rank-aware (see
            // RunwayTrack.Build) so "full" lines up with the loop looking packed.

            m_timer -= Time.deltaTime;
            if (m_timer > 0f) return;

            // Pushing another group onto a runway that can't fit it = overfilling it = fail the level.
            // Only when a launch is actually wanted (previews left, not barriered) so an idle/blocked
            // lane never self-fails.
            if (m_previews.Count > 0 && !Barriered && WouldOverflowRunway())
            {
                GamePlayController.Instance.ReportRunwayJam();
                return;
            }

            if (CanRelease())
            {
                StartCoroutine(ReleaseGroup());
                m_timer = m_releaseInterval;
            }
        }

        // True when the front group physically can't fit on the runway right now (it's full) — the
        // signal to FAIL when the player keeps pushing. Count already includes in-flight members (they
        // register as they board), so this is an honest "no room right now" test. An EMPTY runway
        // always accepts the first group, so a group larger than a tiny manual capacity can still get
        // on instead of dead-failing the lane.
        bool WouldOverflowRunway()
        {
            if (m_track == null) return false;
            int groupCount = FrontGroupCount();
            if (groupCount <= 0) return false;
            return m_track.Count > 0 && m_track.Count + groupCount > m_track.Capacity;
        }

        bool CanRelease()
        {
            if (m_previews.Count == 0 || Barriered) return false;

            // Release the whole front (single-colour) group. Capacity is NOT gated here — pushing onto
            // a full runway fails the level (see Update / WouldOverflowRunway), it doesn't block.
            int groupCount = FrontGroupCount();
            if (groupCount <= 0) return false;

            // QUEUE-ONLY pacing (no spacing between groups): the single gate is that the previous group
            // has fully ASSEMBLED at the start — every member boarded. At that point the group sets off
            // (its head starts advancing) and frees the start area, so the next group can launch in. The
            // groups then follow nose-to-tail in queue order, paced purely by how long each takes to
            // gather, with no artificial rank gap or entry-spacing check.
            if (m_lastGroup != null && !m_lastGroup.AllEntered) return false;

            if (m_track != null)
            {
                // Tránh launch nhiều lane cùng 1 lúc: 
                // Nếu đang có bất kỳ group nào (từ bất kỳ lane nào) đang thả quân (IsAssembling), thì phải chờ.
                if (m_track.IsAnyGroupAssembling()) return false;

                // Tránh kẹt điểm launch: 
                // Chỉ cần đảm bảo đuôi group vừa đi qua đã rời xa Entry đủ (1 body-length)
                float safeDistT = MinEntrySpacing / Mathf.Max(0.1f, m_track.TotalLength);
                if (m_track.IsEntryBlocked(RunwayTrack.EntryT, safeDistT)) return false;
            }

            return true;
        }

        private IEnumerator ReleaseGroup()
        {
            m_previews.RemoveAll(p => p == null); // drop any destroyed entries defensively
            if (m_previews.Count == 0) yield return null;

            int count = FrontGroupCount();
            if (count <= 0) yield return null;
            if (m_track == null || m_track.TotalLength <= 0.01f) yield return null; // need a built track to form up on

            // Release the front group as ONE horizontal crowd block that mirrors its waiting row: every
            // member rides a single shared formation, side-by-side across the road in `count` columns
            // (a single rank, row 0). So a group of 4 in the lane runs the loop 4-abreast instead of
            // single file. The track owns/ticks the block; members still register individually so
            // runway capacity and jam detection see each of them.
            PeopleColor color = m_previews[0].Color;
            var group = m_track.CreateGroup(color, m_runSpeed, count, m_memberSpacing,
                GroupEntryGap(), m_memberLaunchStagger, RunwayTrack.EntryT);
            m_lastGroup = group; // gates the next release's spacing (see CanRelease)

            for (int k = 0; k < count; k++)
            {
                var member = m_previews[0];
                m_previews.RemoveAt(0);
                // Hand the runner off the lane to the shared runners root so it isn't moved by (or
                // tied to) the lane once it's running the loop.
                if (m_runnersRoot != null) member.transform.SetParent(m_runnersRoot, true);

                int col = k;                       // one rank: column = position in the lane row
                group.AddMember(member, 0, col);
                // Hop on strictly one-at-a-time (left-to-right / right-to-left): each member waits for the
                // one ahead of it to LAND before it leaps, instead of all leaping on a fixed timer.
                int order = m_releaseRightToLeft ? (count - 1 - k) : k;
                member.LaunchIntoGroup(m_track, group, 0, col, order);
            }

            yield return new WaitUntil(() => group.AllEntered);
            ReflowPreviews();
        }

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
