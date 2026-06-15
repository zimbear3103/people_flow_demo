using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PeopleFlow
{
    /// <summary>
    /// Tap &amp; hold input for the flow. Reads the legacy Input API (the project runs
    /// activeInputHandler = Both, so this works on Editor, Standalone and mobile touch).
    /// Exposes a single <see cref="IsHolding"/> flag that <see cref="FlowSpawner"/> polls,
    /// plus start/stop events for UI/juice hooks.
    /// </summary>
    public class PlayerInput : Singleton<PlayerInput>
    {
        [Tooltip("Ignore presses that start over a UI element (so tapping buttons doesn't spawn).")]
        [SerializeField] private bool m_blockWhenOverUI = true;

        /// <summary>True while the player is pressing/holding the screen.</summary>
        public bool IsHolding { get; private set; }

        public static event Action OnHoldStarted;
        public static event Action OnHoldEnded;

        private void Update()
        {
            bool held = ReadPointerHeld();

            if (held && !IsHolding)
            {
                IsHolding = true;
                OnHoldStarted?.Invoke();
            }
            else if (!held && IsHolding)
            {
                IsHolding = false;
                OnHoldEnded?.Invoke();
            }
        }

        private bool ReadPointerHeld()
        {
            // Gameplay gate: never accept input while paused / between levels.
            // (Use Instance, not HasInstance: this project's Singleton only primes the
            //  cached instance once Instance is accessed.)
            PeopleFlowGameController controller = PeopleFlowGameController.Instance;
            if (controller != null && !controller.CanPlay)
                return false;

            // Touch takes priority on device.
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began && m_blockWhenOverUI && IsPointerOverUI(touch.fingerId))
                    return false;

                return touch.phase == TouchPhase.Began
                    || touch.phase == TouchPhase.Moved
                    || touch.phase == TouchPhase.Stationary;
            }

            // Mouse / Editor fallback.
            if (Input.GetMouseButton(0))
            {
                if (m_blockWhenOverUI && IsPointerOverUI(-1))
                    return false;
                return true;
            }

            return false;
        }

        private static bool IsPointerOverUI(int pointerId)
        {
            if (EventSystem.current == null)
                return false;

            return pointerId < 0
                ? EventSystem.current.IsPointerOverGameObject()
                : EventSystem.current.IsPointerOverGameObject(pointerId);
        }
    }
}
