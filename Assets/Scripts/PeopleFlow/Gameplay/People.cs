using System.Collections;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A little coloured person running around the loop. Advances a normalised position along
    /// the <see cref="RunwayTrack"/>, detects when it crosses a matching open hole, reserves a
    /// slot, then hops in and despawns.
    /// </summary>
    public class People : MonoBehaviour
    {
        [SerializeField]
        private float RevealWindow = 0.06f;
        [SerializeField]
        private float JumpHeight = 1.3f;
        [SerializeField]
        private float JumpDuration = 0.32f;
        [SerializeField]
        private float m_previewSlideSpeed = 6f; // how quickly a waiting minion eases to its lane slot

        public PeopleColor Color { get; private set; }
        public bool IsJumping { get; private set; }       // hopping into a hole
        public bool IsPreview { get; private set; }        // standing in a lane queue, not yet on the track
        public bool IsEnteringRoad { get; private set; }   // hopping from the lane onto the track
        public bool IsPendingPeel { get; private set; }    // reserved a hole, waiting its turn to drop in

        /// <summary>The slot in the lane line this preview should ease toward (set by <see cref="Lane"/>).</summary>
        public Vector3 PreviewTarget { get; set; }

        /// <summary>Position to use for entry-spacing: while entering, the landing point it has
        /// reserved; otherwise the live position. Lets the lane treat an in-flight runner as already
        /// occupying the entry so a held lane doesn't stack hops on top of each other.</summary>
        public Vector3 SpacingPosition => IsEnteringRoad ? m_entryLanding : transform.position;

        RunwayTrack m_track;
        float m_t;
        float m_speed;
        Vector3 m_entryLanding;
        RunnerGroup m_group;   // non-null while this runner is part of a moving crowd block
        private Animator m_animator;

        /// <summary>True while this runner belongs to a <see cref="RunnerGroup"/> that drives its
        /// position; such a runner does not self-advance along the loop.</summary>
        public bool IsInGroup => m_group != null;
        // ---- factory --------------------------------------------------------

        /// <summary>
        /// Instantiate a runner from the assigned character prefab, tint its body to
        /// <paramref name="color"/>, and start it running from <paramref name="startT"/>.
        /// </summary>\
        private void Start()
        {
            m_animator = GetComponent<Animator>();
        }
        public static People Spawn(GameObject prefab, PeopleColor color, RunwayTrack track,
            MaterialLibrary mats, float startT, float speed, Transform parent)
        {
            if (prefab == null)
            {
                PFLog.Error("People.Spawn: no character prefab assigned — cannot release a runner.");
                return null;
            }

            var go = Instantiate(prefab, parent);
            var c = go.GetComponent<People>();
            if (c == null)
            {
                PFLog.Error($"People.Spawn: character prefab '{prefab.name}' has no People component.");
                Object.Destroy(go);
                return null;
            }

            go.name = "Runner_" + color;
            c.ApplyColor(mats, color);
            c.Init(color, track, startT, speed);
            return c;
        }

        /// <summary>
        /// Instantiate a runner as a lane preview: coloured and placed standing on the lane, but not
        /// registered or running. Call <see cref="LaunchToRoad"/> to send it onto the track.
        /// </summary>
        public static People SpawnPreview(GameObject prefab, PeopleColor color, MaterialLibrary mats,
            Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            if (prefab == null)
            {
                PFLog.Error("People.SpawnPreview: no character prefab assigned — cannot show a lane preview.");
                return null;
            }

            var go = Instantiate(prefab, parent);
            var c = go.GetComponent<People>();
            if (c == null)
            {
                PFLog.Error($"People.SpawnPreview: character prefab '{prefab.name}' has no People component.");
                Object.Destroy(go);
                return null;
            }

            go.name = "Preview_" + color;
            c.ApplyColor(mats, color);
            c.InitPreview(color, worldPos, worldRot);
            return c;
        }

        void ApplyColor(MaterialLibrary mats, PeopleColor color)
        {
            Prim.Tint(Prim.CollectTintable(gameObject), mats.Colored(color));
        }

        void Init(PeopleColor color, RunwayTrack track, float startT, float speed)
        {
            Color = color;
            m_track = track;
            m_t = Mathf.Repeat(startT, 1f);
            m_speed = speed;

            transform.position = m_track.Evaluate(m_t, out var fwd);
            if (fwd.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(fwd);

            // Pop in from zero up to the prefab's authored scale.
            Vector3 baseScale = transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;

            m_track.Register(this);
            StartCoroutine(TweenUtil.ScalePop(transform, baseScale, 0.22f));
        }

        void InitPreview(PeopleColor color, Vector3 worldPos, Quaternion worldRot)
        {
            Color = color;
            IsPreview = true;
            transform.SetPositionAndRotation(worldPos, worldRot);
            PreviewTarget = worldPos;

            Vector3 baseScale = transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;
            StartCoroutine(TweenUtil.ScalePop(transform, baseScale, 0.22f));
        }

        /// <summary>
        /// Send a preview minion from its lane slot onto the track: hop in an arc to the entry point,
        /// then start running. Registers immediately so it counts toward runway capacity and reserves
        /// the entry slot for the duration of the hop. An optional <paramref name="delay"/> holds the
        /// minion at its tray slot before hopping, so a released group enters as an ordered wave
        /// (left-to-right / right-to-left) instead of all members leaping at once.
        /// </summary>
        public void LaunchToRoad(RunwayTrack track, float startT, float speed, float delay = 0f)
        {
            if (track == null) return;
            transform.position = PreviewTarget; // start the hop from the exact tray slot (it may still be easing forward)
            IsPreview = false;
            IsEnteringRoad = true;
            m_track = track;
            m_speed = speed;

            float t = Mathf.Repeat(startT, 1f);
            m_entryLanding = track.Evaluate(t, out var fwd);
            Quaternion landRot = fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd) : transform.rotation;
            transform.rotation = landRot;
            track.Register(this);

            StartCoroutine(LaunchRoutine(t, landRot, Mathf.Max(0f, delay)));
        }

        /// <summary>Hold at the tray slot for <paramref name="delay"/> seconds (this member's turn in
        /// the group's wave), then hop onto the track and begin running. The minion is already
        /// registered + <see cref="IsEnteringRoad"/> during the hold, so it neither runs nor reflows
        /// as a preview and its reserved landing still blocks the entry for spacing.</summary>
        IEnumerator LaunchRoutine(float t, Quaternion landRot, float delay)
        {
            for (float e = 0f; e < delay; e += Time.deltaTime)
                yield return null;

            yield return TweenUtil.HopArc(transform, m_entryLanding, JumpHeight, JumpDuration, () =>
            {
                m_t = t;
                transform.rotation = landRot;
                IsEnteringRoad = false; // begins running on the next Update
            });
        }

        // ---- crowd block ----------------------------------------------------

        /// <summary>The <see cref="RunnerGroup"/> calls this when this runner joins a block. It then
        /// drives the runner's pose, so <see cref="Update"/> stops self-advancing it.</summary>
        internal void SetGroup(RunnerGroup group) => m_group = group;

        /// <summary>
        /// Send a preview minion from its lane slot into a moving crowd <see cref="RunnerGroup"/>: hop
        /// one-by-one (after <paramref name="delay"/>) toward this member's grid slot, then hand control
        /// to the group, which drives it in formation. Registers immediately so it still counts toward
        /// runway capacity, and reports a near-entry spacing position so a held lane doesn't spawn the
        /// next crowd on top of this one before it has formed up.
        /// </summary>
        public void LaunchIntoGroup(RunwayTrack track, RunnerGroup group, int row, int col, float delay = 0f)
        {
            if (track == null || group == null) return;
            transform.position = PreviewTarget; // start the hop from the exact tray slot
            IsPreview = false;
            IsEnteringRoad = true;
            m_track = track;
            m_group = group;
            m_entryLanding = group.SlotWorldNow(row, col, out _); // approximate entry occupancy for spacing
            track.Register(this);

            StartCoroutine(GroupEntryRoutine(group, row, col, Mathf.Max(0f, delay)));
        }

        IEnumerator GroupEntryRoutine(RunnerGroup group, int row, int col, float delay)
        {
            for (float e = 0f; e < delay; e += Time.deltaTime)
                yield return null;

            // Aim the hop at the slot's current position; the group eases out the small residual (the
            // slot drifts forward while we are mid-air) with a short blend once we land.
            Vector3 landing = group.SlotWorldNow(row, col, out Quaternion landRot);
            m_entryLanding = landing;
            transform.rotation = landRot;
            yield return TweenUtil.HopArc(transform, landing, JumpHeight, JumpDuration, () =>
            {
                transform.rotation = landRot;
                IsEnteringRoad = false;
                group.NotifyEntered(this);
            });
        }

        /// <summary>The group writes this runner's world pose each frame while it runs in formation.</summary>
        internal void SetGroupPose(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            if (m_animator != null) m_animator.SetBool("isRun", true);
        }

        /// <summary>The group calls this when it reserves a hole's slot for this runner — it will drop
        /// in shortly. Flags it as a pending move so the runway-jam check doesn't treat a crowd waiting
        /// its peel turn as a dead-locked, full runway.</summary>
        internal void MarkPendingPeel() => IsPendingPeel = true;

        /// <summary>The group hands this runner the hole it reserved when its turn comes to drop in:
        /// detach from the crowd and hop in via the shared jump path.</summary>
        internal void PeelIntoHole(Hole hole)
        {
            m_group = null;        // the group has already removed us from its formation
            IsPendingPeel = false; // IsJumping now covers us for the jam check
            BeginJump(hole);
        }

        // ---- run loop -------------------------------------------------------

        void Update()
        {
            if (IsPreview)
            {
                // Ease toward the assigned slot so the waiting line slides forward as runners leave.
                // Only while playing; otherwise snap settled so previews don't drift on the win/lose screen.
                bool playing = GamePlayController.Instance != null && GamePlayController.Instance.IsGamePlaying;
                transform.position = playing
                    ? Vector3.Lerp(transform.position, PreviewTarget, 1f - Mathf.Exp(-m_previewSlideSpeed * Time.deltaTime))
                    : PreviewTarget;
                return;
            }

            if (IsJumping || IsEnteringRoad || m_track == null || m_track.TotalLength <= 0f)
            {
                m_animator.SetBool("isRun", false);
                return; 
            }
            if (GamePlayController.Instance == null || !GamePlayController.Instance.IsGamePlaying) return;

            if (IsInGroup) return; // a crowd member is positioned by its RunnerGroup, not self-driven

            float oldT = m_t;
            float mult = m_track.SpeedMultiplierAt(m_t);
            m_t = Mathf.Repeat(m_t + m_speed * mult / m_track.TotalLength * Time.deltaTime, 1f);

            // Move along the loop and face travel direction.
            transform.position = m_track.Evaluate(m_t, out var fwd);
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fwd);
            m_animator.SetBool("isRun", true);

            CheckHoles(oldT, m_t);
        }

        void CheckHoles(float oldT, float newT)
        {
            var holes = m_track.Holes;
            for (int i = 0; i < holes.Count; i++)
            {
                var hole = holes[i];
                if (hole == null) continue;

                if (WrapDistance(newT, hole.TrackT) < RevealWindow)
                    hole.RevealIfHidden();

                if (PassedForward(oldT, newT, hole.TrackT) && hole.CanAccept(Color) && hole.TryReserve(Color))
                {
                    BeginJump(hole);
                    return;
                }
            }
        }

        void BeginJump(Hole hole)
        {
            IsJumping = true;
            Vector3 target = hole.JumpTarget + Vector3.up * 0.1f;
            StartCoroutine(TweenUtil.HopInto(transform, target, JumpHeight, JumpDuration, () =>
            {
                hole.Commit();
                if (m_track != null) m_track.Unregister(this);
                Destroy(gameObject);
            }));
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>True if moving forward from oldT to newT crossed target (wrap-aware).</summary>
        static bool PassedForward(float oldT, float newT, float target)
        {
            if (Mathf.Approximately(oldT, newT)) return false;
            if (newT >= oldT) return target > oldT && target <= newT;
            return target > oldT || target <= newT; // wrapped past 1.0 → 0.0
        }

        /// <summary>Shortest distance between two normalised positions on the loop.</summary>
        static float WrapDistance(float a, float b)
        {
            float d = Mathf.Abs(Mathf.Repeat(a - b, 1f));
            return Mathf.Min(d, 1f - d);
        }
    }
}