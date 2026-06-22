using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PeopleFlow
{
    public class InputManager : Singleton<InputManager>
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
            if (GamePlayController.Instance == null || !GamePlayController.Instance.IsGamePlaying)
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
            if (m_camera == null) m_camera = FindAnyObjectByType<Camera>();
            if (m_camera == null) return;

            Ray ray = m_camera.ScreenPointToRay(screenPos);

            Lane hitLane = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                hitLane = hit.collider.GetComponentInParent<Lane>();

            // The waiting minions stand above the lane's (ground-level) collider and carry no collider
            // of their own, so a tap aimed at them can sail over the collider and hit nothing. Fall
            // back to picking the lane the tap projects onto, so tapping the visible queue still works.
            if (hitLane == null) hitLane = PickLaneUnderRay(ray);

            for (int i = 0; i < m_lanes.Count; i++)
                if (m_lanes[i] != null) m_lanes[i].IsHeld = m_lanes[i] == hitLane;
        }

        Lane PickLaneUnderRay(Ray ray)
        {
            Lane best = null;
            float bestSqr = 9f; // ignore taps more than ~3 units from any lane pad (lanes sit ~4 apart)
            for (int i = 0; i < m_lanes.Count; i++)
            {
                var lane = m_lanes[i];
                if (lane == null) continue;
                Vector3 pad = lane.transform.position;
                var ground = new Plane(Vector3.up, pad);
                if (!ground.Raycast(ray, out float dist)) continue;
                float sqr = (ray.GetPoint(dist) - pad).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = lane; }
            }
            return best;
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
