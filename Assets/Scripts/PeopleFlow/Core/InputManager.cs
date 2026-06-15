using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PeopleFlow
{
    /// <summary>
    /// Detects tap &amp; hold per lane. Uses the new Input System's unified Pointer (mouse on PC,
    /// touch on mobile) when available, with a legacy Input fallback. Raycasts the pointer into
    /// the 3D scene and marks the hit lane as held — no custom layers/tags required.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        List<Lane> m_lanes = new List<Lane>();
        Camera m_camera;

        public void Bind(List<Lane> lanes, Camera cam)
        {
            m_lanes = lanes ?? new List<Lane>();
            m_camera = cam != null ? cam : Camera.main;
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                ClearAll();
                return;
            }

            if (!ReadPointer(out bool pressed, out Vector2 screenPos) || !pressed)
            {
                ClearAll();
                return;
            }

            if (m_camera == null) m_camera = Camera.main;
            if (m_camera == null) return;

            Lane hitLane = null;
            if (Physics.Raycast(m_camera.ScreenPointToRay(screenPos), out RaycastHit hit, 500f))
                hitLane = hit.collider.GetComponentInParent<Lane>();

            for (int i = 0; i < m_lanes.Count; i++)
                if (m_lanes[i] != null) m_lanes[i].IsHeld = m_lanes[i] == hitLane;
        }

        void ClearAll()
        {
            for (int i = 0; i < m_lanes.Count; i++)
                if (m_lanes[i] != null) m_lanes[i].IsHeld = false;
        }

        static bool ReadPointer(out bool pressed, out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            if (p != null)
            {
                pressed = p.press.isPressed;
                screenPos = p.position.ReadValue();
                return true;
            }
            pressed = false;
            screenPos = Vector2.zero;
            return false;
#else
            pressed = Input.GetMouseButton(0);
            screenPos = Input.mousePosition;
            return true;
#endif
        }
    }
}
