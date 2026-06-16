using UnityEngine;

public enum PopupType
{
    Setting, 
    LevelComplete,
    Alert,
    Continue, 
    Message,
    Warning,
    Tutorial,
    LevelFail
}
public class UIPopup : MonoBehaviour
{
    [SerializeField] protected PopupType m_type;
    [SerializeField] protected GameObject m_panel;

    public bool IsShowing => m_panel.activeInHierarchy;

    public PopupType Type => m_type;
    public GameObject Panel => m_panel;

    public virtual void Show()
    {
        m_panel.SetActive(true);
    }

    public virtual void Hide()
    {
        m_panel.SetActive(false);
    }
}
