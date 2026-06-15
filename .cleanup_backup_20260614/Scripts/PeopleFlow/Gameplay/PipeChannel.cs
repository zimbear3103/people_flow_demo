using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Defines the inner walls of the pipe so lightweight characters know where to clamp.
    /// Place on the pipe root and set the inner X bounds (or let it derive them from a
    /// width). Publishes the bounds to <see cref="PersonMovement"/> while enabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class PipeChannel : MonoBehaviour
    {
        [Tooltip("World-space inner X of the left wall (characters are kept to the right of this).")]
        [SerializeField] private float m_innerLeftX = -1.4f;

        [Tooltip("World-space inner X of the right wall (characters are kept to the left of this).")]
        [SerializeField] private float m_innerRightX = 1.4f;

        public float InnerLeftX => m_innerLeftX;
        public float InnerRightX => m_innerRightX;

        private void OnEnable()
        {
            PersonMovement.SetChannelBounds(m_innerLeftX, m_innerRightX);
        }

        private void OnDisable()
        {
            PersonMovement.ClearChannelBounds();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 top = transform.position + Vector3.up * 6f;
            Vector3 bottom = transform.position + Vector3.down * 6f;
            Gizmos.DrawLine(new Vector3(m_innerLeftX, top.y), new Vector3(m_innerLeftX, bottom.y));
            Gizmos.DrawLine(new Vector3(m_innerRightX, top.y), new Vector3(m_innerRightX, bottom.y));
        }
#endif
    }
}
