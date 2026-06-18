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

        // ---- factory --------------------------------------------------------

        /// <summary>
        /// Instantiate a runner from the assigned character prefab, tint its body to
        /// <paramref name="color"/>, and start it running from <paramref name="startT"/>.
        /// </summary>
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

        // ---- run loop -------------------------------------------------------

        void Update()
        {
            if (IsPreview)
            {
                // Ease toward the assigned slot so the waiting line slides forward as runners leave.
                // Only while playing; otherwise snap settled so previews don't drift on the win/lose screen.
                bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
                transform.position = playing
                    ? Vector3.Lerp(transform.position, PreviewTarget, 1f - Mathf.Exp(-m_previewSlideSpeed * Time.deltaTime))
                    : PreviewTarget;
                return;
            }

            if (IsJumping || IsEnteringRoad || m_track == null || m_track.TotalLength <= 0f) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            float oldT = m_t;
            float mult = m_track.SpeedMultiplierAt(m_t);
            m_t = Mathf.Repeat(m_t + m_speed * mult / m_track.TotalLength * Time.deltaTime, 1f);

            // Move along the loop and face travel direction.
            transform.position = m_track.Evaluate(m_t, out var fwd);
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fwd);

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
