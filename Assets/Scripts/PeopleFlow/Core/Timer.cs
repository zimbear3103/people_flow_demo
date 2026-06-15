using System;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Counts down the level time while the game is Playing (uses scaled time, so pausing the
    /// game via Time.timeScale also pauses the clock). Reports timeout to <see cref="GameManager"/>.
    /// </summary>
    public class Timer : MonoBehaviour
    {
        public float Remaining { get; private set; }
        public float Total { get; private set; }

        /// <summary>Fired every frame while running: (remaining seconds, total seconds).</summary>
        public event Action<float, float> OnTick;

        bool m_running;

        public void Begin(float seconds)
        {
            Total = Mathf.Max(0.01f, seconds);
            Remaining = Total;
            m_running = true;
            OnTick?.Invoke(Remaining, Total);
        }

        void Update()
        {
            if (!m_running) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                m_running = false;
                OnTick?.Invoke(Remaining, Total);
                GameManager.Instance.ReportTimeOut();
                return;
            }
            OnTick?.Invoke(Remaining, Total);
        }
    }
}
