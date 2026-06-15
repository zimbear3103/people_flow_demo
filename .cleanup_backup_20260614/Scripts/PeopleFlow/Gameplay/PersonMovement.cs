using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Per-character motion through the pipe. Designed to scale to hundreds of objects.
    ///
    /// Two modes (pick in the inspector / prefab):
    ///   • LightweightCustom (DEFAULT, recommended for 500+):
    ///       A hand-rolled gravity integrator on a *Kinematic* Rigidbody2D. We avoid the
    ///       2D solver entirely (no contact resolution, no islands) and simply clamp the
    ///       character inside the pipe walls ourselves. Kinematic + MovePosition still
    ///       fires OnTriggerEnter2D against the (static, trigger) gates and target, which
    ///       is all the gameplay needs. This is dramatically cheaper than N dynamic bodies.
    ///   • Rigidbody2DPhysics:
    ///       Standard Dynamic Rigidbody2D with gravity, frozen rotation, colliding against
    ///       the static pipe walls. Simpler/juicier for small crowds, heavier at scale.
    ///
    /// Either way the character keeps a small CircleCollider2D so gates/target detect it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PersonMovement : MonoBehaviour, IPooledObject
    {
        public enum MovementMode
        {
            LightweightCustom,
            Rigidbody2DPhysics
        }

        [Header("Mode")]
        [SerializeField] private MovementMode m_mode = MovementMode.LightweightCustom;

        [Header("Lightweight gravity")]
        [Tooltip("Custom gravity (units/s^2). Negative = down.")]
        [SerializeField] private float m_gravity = -38f;
        [Tooltip("Terminal velocity. Capping fall speed prevents tunnelling through gate triggers.")]
        [SerializeField] private float m_maxFallSpeed = 22f;
        [Tooltip("How much horizontal speed is kept when bouncing off a pipe wall (0 = stop, 1 = perfect bounce).")]
        [SerializeField, Range(0f, 1f)] private float m_wallBounciness = 0.2f;
        [Tooltip("Approx. body radius, used to keep the character off the pipe walls.")]
        [SerializeField] private float m_radius = 0.18f;

        private Rigidbody2D m_rb;
        private Vector2 m_velocity;

        // ---- Shared pipe bounds (one pipe in the demo). Set by PipeChannel. -------------
        private static bool s_hasBounds;
        private static float s_minX;
        private static float s_maxX;

        public static void SetChannelBounds(float minX, float maxX)
        {
            s_minX = minX;
            s_maxX = maxX;
            s_hasBounds = true;
        }

        public static void ClearChannelBounds()
        {
            s_hasBounds = false;
        }
        // --------------------------------------------------------------------------------

        private void Awake()
        {
            m_rb = GetComponent<Rigidbody2D>();
            ConfigureBody();
        }

        private void ConfigureBody()
        {
            m_rb.freezeRotation = true;
            m_rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (m_mode == MovementMode.LightweightCustom)
            {
                // Kinematic: we move it ourselves, the solver never touches it.
                m_rb.bodyType = RigidbodyType2D.Kinematic;
                m_rb.gravityScale = 0f;
            }
            else
            {
                m_rb.bodyType = RigidbodyType2D.Dynamic;
                m_rb.gravityScale = 1f;
                m_rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        /// <summary>Reset hook from the pool (see <see cref="IPooledObject"/>).</summary>
        public void OnObjectSpawn()
        {
            m_velocity = Vector2.zero;
            if (m_mode == MovementMode.Rigidbody2DPhysics && m_rb != null)
            {
                m_rb.linearVelocity = Vector2.zero;   // Unity 6 renamed velocity -> linearVelocity
                m_rb.angularVelocity = 0f;
            }
        }

        /// <summary>
        /// Gives the character its initial "jump into the pipe" velocity.
        /// Called by the spawner and by math gates when cloning extra people.
        /// </summary>
        public void Launch(Vector2 velocity)
        {
            if (m_mode == MovementMode.Rigidbody2DPhysics && m_rb != null)
                m_rb.linearVelocity = velocity;
            else
                m_velocity = velocity;
        }

        private void FixedUpdate()
        {
            // Physics mode is driven entirely by the engine.
            if (m_mode != MovementMode.LightweightCustom)
                return;

            // Freeze in place while paused.
            if (PeopleFlowGameController.Paused)
                return;

            float dt = Time.fixedDeltaTime;

            // Integrate gravity (clamped to terminal velocity).
            m_velocity.y += m_gravity * dt;
            if (m_velocity.y < -m_maxFallSpeed)
                m_velocity.y = -m_maxFallSpeed;

            Vector2 pos = m_rb.position + m_velocity * dt;

            // Keep the body inside the pipe by clamping X and reflecting horizontal speed.
            if (s_hasBounds)
            {
                float left = s_minX + m_radius;
                float right = s_maxX - m_radius;

                if (pos.x < left)
                {
                    pos.x = left;
                    m_velocity.x = -m_velocity.x * m_wallBounciness;
                }
                else if (pos.x > right)
                {
                    pos.x = right;
                    m_velocity.x = -m_velocity.x * m_wallBounciness;
                }
            }

            m_rb.MovePosition(pos);
        }
    }
}
