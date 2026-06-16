using System;
using UnityEngine;

public class CheatGame : Singleton<CheatGame>
{
    private int m_leftTouchCount = 0;
    private int m_rightTouchCount = 0;

    private float m_touchResetTime = 1f; // Thời gian tối đa giữa 2 lần chạm/click
    private float m_lastTouchTime = 0f;

    public Action CheatWin;
    public Action CheatLose;
    public void SetupCheat(Action winFunc, Action loseFunc)
    {
        CheatWin = winFunc;
        CheatLose = loseFunc;
    }    

    void Update()
    {
        bool touched = false;
        Vector2 inputPosition = Vector2.zero;

        // Xử lý trên Mobile (Touch)
        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    touched = true;
                    inputPosition = touch.position;
                    break; // chỉ xử lý 1 touch đầu tiên
                }
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // Xử lý chuột trên PC
        if (Input.GetMouseButtonDown(0))
        {
            touched = true;
            inputPosition = Input.mousePosition;
        }
#endif

        if (touched)
        {
            m_lastTouchTime = Time.time; // cập nhật thời gian lần cuối touch/click

            float screenMid = Screen.width / 2;
            float screenTop = Screen.height - Screen.height / 8;
            GameLog.Log(LogType.Log, $"screenMid = {screenMid} screenTop = {screenTop} inputPosition: {inputPosition}");

            if (inputPosition.x < screenMid && inputPosition.y > screenTop)
            {
                m_leftTouchCount++;
                m_rightTouchCount = 0;
                GameLog.Log(LogType.Log, $"Chạm/click bên trái: {m_leftTouchCount}");

                if (m_leftTouchCount >= 10)
                {
                    CheatLose?.Invoke();
                }
            }
            else if (inputPosition.x > screenMid && inputPosition.y > screenTop)
            {
                m_rightTouchCount++;
                m_leftTouchCount = 0;
                GameLog.Log(LogType.Log, $"Chạm/click bên phải: {m_rightTouchCount}");

                if (m_rightTouchCount >= 10)
                {
                    CheatWin?.Invoke();
                }
            }
        }

        // Reset nếu không thao tác trong thời gian giới hạn
        if (Time.time - m_lastTouchTime > m_touchResetTime)
        {
            m_leftTouchCount = 0;
            m_rightTouchCount = 0;
        }
    }
}
