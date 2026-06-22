using System;
using UnityEngine;

namespace PeopleFlow
{
    public class Timer : Singleton<Timer>
    {
        public float Remaining { get; private set; }
        public float Total { get; private set; }

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
            if (GamePlayController.Instance == null || !GamePlayController.Instance.IsGamePlaying) return;

            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                m_running = false;
                OnTick?.Invoke(Remaining, Total);
                GamePlayController.Instance.ReportTimeOut();
                return;
            }
            OnTick?.Invoke(Remaining, Total);
        }
    }
}
