using System.Collections;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private float m_fpsUpdateInterval = 1.0f;
    private int m_framesSinceLastUpdate = 0;
    private float m_currentFPS = 0f;
    private bool IsInitialized { set; get; } = false;
    private void Start()
    {           
        IsInitialized = true;
        StartCoroutine(UpdateFPS());            
    }

    private IEnumerator UpdateFPS()
    {
        while (true)
        {
            yield return new WaitForSeconds(m_fpsUpdateInterval);
            m_currentFPS = m_framesSinceLastUpdate / m_fpsUpdateInterval;
            m_framesSinceLastUpdate = 0;
        }
    }

    private void Update()
    {
        if(IsInitialized)
            m_framesSinceLastUpdate++;
    }

    private void OnGUI()
    {            
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 50,
            fontStyle = FontStyle.Bold, 
            alignment = TextAnchor.MiddleCenter, 
            normal = { textColor = Color.red },               
        };
        GUI.Label(new Rect(10, 100, 500, 100), "FPS: " + m_currentFPS.ToString("F2"), style);            
    }
}

