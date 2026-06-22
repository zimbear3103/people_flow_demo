using System.Collections;
using UnityEngine;

namespace PeopleFlow
{
    public class People : MonoBehaviour
    {
        [SerializeField]
        private float RevealWindow = 0.06f;
        [SerializeField]
        private float JumpHeight = 1.3f;
        [SerializeField]
        private float JumpDuration = 0.01f;
        [SerializeField]
        private float m_previewSlideSpeed = 6f; // how quickly a waiting minion eases to its lane slot

        public PeopleColor Color { get; private set; }
        public bool IsJumping { get; private set; }       // hopping into a hole
        public bool IsPreview { get; private set; }        // standing in a lane queue, not yet on the track
        public bool IsEnteringRoad { get; private set; }   // hopping from the lane onto the track
        public bool IsPendingPeel { get; private set; }    // reserved a hole, waiting its turn to drop in
        public float TrackT => m_t;

        public Vector3 PreviewTarget { get; set; }

        public Vector3 SpacingPosition => IsEnteringRoad ? m_entryLanding : transform.position;

        RunwayTrack m_track;
        float m_t;
        float m_speed;
        Vector3 m_entryLanding;
        RunnerGroup m_group;   // non-null while this runner is part of a moving crowd block
        private Animator m_animator;

        public bool IsInGroup => m_group != null;
        // ---- factory --------------------------------------------------------

        private void Start()
        {
            m_animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {   
            EventManager.Instance.On("lose", onDie);
        }
        private void OnDisable()
        {
            //EventManager.Instance.Off("lose", onDie);
        }
        public static People Spawn(GameObject prefab, PeopleColor color, RunwayTrack track,
            MaterialLibrary mats, float startT, float speed, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogError("People.Spawn: no character prefab assigned — cannot release a runner.");
                return null;
            }

            var go = PoolingObject.Instance.Get(prefab, parent);
            var c = go.GetComponent<People>();
            if (c == null)
            {
                Debug.LogError($"People.Spawn: character prefab '{prefab.name}' has no People component.");
                Object.Destroy(go);
                return null;
            }

            go.name = "Runner_" + color;
            c.ResetState();
            c.ApplyColor(mats, color);
            c.Init(color, track, startT, speed);
            return c;
        }

        public static People SpawnPreview(GameObject prefab, PeopleColor color, MaterialLibrary mats,
            Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            if (prefab == null)
            {
                Debug.LogError("People.SpawnPreview: no character prefab assigned — cannot show a lane preview.");
                return null;
            }

            var go = PoolingObject.Instance.Get(prefab, parent);
            var c = go.GetComponent<People>();
            if (c == null)
            {
                Debug.LogError($"People.SpawnPreview: character prefab '{prefab.name}' has no People component.");
                Object.Destroy(go);
                return null;
            }

            go.name = "Preview_" + color;
            c.ResetState();
            c.ApplyColor(mats, color);
            c.InitPreview(color, worldPos, worldRot);
            return c;
        }

        internal void ResetState()
        {
            StopAllCoroutines(); // drop any in-flight hop/pop from the previous life
            IsJumping = false;
            IsPreview = false;
            IsEnteringRoad = false;
            IsPendingPeel = false;
            m_group = null;
            m_track = null;
            m_t = 0f;
            m_speed = 0f;
            m_entryLanding = Vector3.zero;
            // Start() caches this, but it hasn't run yet on a brand-new instance's first spawn.
            if (m_animator == null) m_animator = GetComponent<Animator>();
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
            StartCoroutine(Tweener.ScalePop(transform, baseScale, 0.22f));
        }

        void InitPreview(PeopleColor color, Vector3 worldPos, Quaternion worldRot)
        {
            Color = color;
            IsPreview = true;
            transform.SetPositionAndRotation(worldPos, worldRot);
            PreviewTarget = worldPos;

            Vector3 baseScale = transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;
            StartCoroutine(Tweener.ScalePop(transform, baseScale, 0.22f));
        }

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

        IEnumerator LaunchRoutine(float t, Quaternion landRot, float delay)
        {
            for (float e = 0f; e < delay; e += Time.deltaTime)
                yield return null;

            yield return Tweener.HopArc(transform, m_entryLanding, JumpHeight, JumpDuration, () =>
            {
                m_t = t;
                transform.rotation = landRot;
                IsEnteringRoad = false; // begins running on the next Update
            });
        }

        // ---- crowd block ----------------------------------------------------

        internal void SetGroup(RunnerGroup group) => m_group = group;

        public void LaunchIntoGroup(RunwayTrack track, RunnerGroup group, int row, int col, int launchOrder = 0)
        {
            if (track == null || group == null) return;
            transform.position = PreviewTarget; // start the hop from the exact tray slot
            IsPreview = false;
            IsEnteringRoad = true;
            m_track = track;
            m_group = group;
            m_entryLanding = group.SlotWorldNow(row, col, out _); // approximate entry occupancy for spacing
            track.Register(this);

            StartCoroutine(GroupEntryRoutine(group, row, col, Mathf.Max(0, launchOrder)));
        }

        IEnumerator GroupEntryRoutine(RunnerGroup group, int row, int col, int launchOrder)
        {
            // Bail if the level is being torn down/rebuilt mid-entry (track destroyed) so we don't hop
            // toward a slot on a dead track. (group is a plain object guarded non-null by the caller.)
            if (group == null || m_track == null) yield break;

            // Wait until every member ahead of us has finished its entry hop, so the group lands on the
            // road strictly one-at-a-time (no overlapping leaps). Bail only on a real STALL (no member
            // ahead has landed for a while) so a destroyed predecessor can't hang us forever — this stays
            // correct no matter how big the group is, since it resets each time progress is made.
            int lastSeen = group.EnteredCount;
            float stall = 0f;
            while (group.EnteredCount < launchOrder)
            {
                if (group.EnteredCount != lastSeen) { lastSeen = group.EnteredCount; stall = 0f; }
                else stall += Time.deltaTime;
                if (stall > 2f) break;
                yield return null;
            }

            // Aim the hop at the slot's current position; the group eases out the small residual (the
            // slot drifts forward while we are mid-air) with a short blend once we land.
            Vector3 landing = group.SlotWorldNow(row, col, out Quaternion landRot);
            m_entryLanding = landing;
            transform.rotation = landRot;
            yield return Tweener.HopArc(transform, landing, JumpHeight, JumpDuration, () =>
            {
                transform.rotation = landRot;
                IsEnteringRoad = false;
                group.NotifyEntered(this);
            });
        }

        internal void SetGroupPose(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            if (m_animator != null) m_animator.SetBool("isRun", true);
        }

        internal void MarkPendingPeel() => IsPendingPeel = true;

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
            StartCoroutine(Tweener.HopInto(transform, target, JumpHeight, JumpDuration, () =>
            {
                hole.Commit();
                if (m_track != null) m_track.Unregister(this);
                // Recycle instead of destroying so the next spawn reuses this body (see PoolingObject).
                if (PoolingObject.Instance != null) PoolingObject.Instance.Release(gameObject);
                else Destroy(gameObject);
            }));
        }

        // ---- helpers --------------------------------------------------------

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

        private void onDie()
        {
            if (!m_animator) return;

            if (!IsPreview)
            m_animator.SetBool("isDie", true);
        }
    }
}
