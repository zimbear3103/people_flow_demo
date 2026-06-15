using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A little coloured person running around the loop. (Named with the _People suffix so it
    /// never clashes with UnityEngine.CharacterController.) Advances a normalised position along
    /// the <see cref="RunwayTrack"/>, detects when it crosses a matching open hole, reserves a
    /// slot, then hops in and despawns.
    /// </summary>
    public class CharacterController_People : MonoBehaviour
    {
        const float RevealWindow = 0.06f;  // how close (in track-t) before a hidden hole reveals
        const float JumpHeight = 1.3f;
        const float JumpDuration = 0.32f;

        public PeopleColor Color { get; private set; }
        public bool IsJumping { get; private set; }

        RunwayTrack m_track;
        float m_t;
        float m_speed;

        // ---- factory --------------------------------------------------------

        public static CharacterController_People Spawn(PeopleColor color, RunwayTrack track,
            MaterialLibrary mats, float startT, float speed, Transform parent)
        {
            var root = new GameObject("Runner_" + color);
            if (parent != null) root.transform.SetParent(parent, false);

            var mat = mats.Colored(color);
            Prim.Create(PrimitiveType.Capsule, "Body", root.transform,
                new Vector3(0f, 0.5f, 0f), new Vector3(0.45f, 0.5f, 0.45f), mat);
            Prim.Create(PrimitiveType.Sphere, "Head", root.transform,
                new Vector3(0f, 1.02f, 0f), Vector3.one * 0.42f, mat);

            var c = root.AddComponent<CharacterController_People>();
            c.Init(color, track, startT, speed);
            return c;
        }

        void Init(PeopleColor color, RunwayTrack track, float startT, float speed)
        {
            Color = color;
            m_track = track;
            m_t = Mathf.Repeat(startT, 1f);
            m_speed = speed;

            transform.position = m_track.Evaluate(m_t, out var fwd);
            if (fwd.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(fwd);

            m_track.Register(this);
            StartCoroutine(TweenUtil.ScalePop(transform, Vector3.one, 0.22f));
        }

        // ---- run loop -------------------------------------------------------

        void Update()
        {
            if (IsJumping || m_track == null || m_track.TotalLength <= 0f) return;
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

                // Reveal hidden holes the runner is passing near (visual only).
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
            Vector3 target = hole.transform.position + Vector3.up * 0.1f;
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
