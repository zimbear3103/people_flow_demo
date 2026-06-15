using UnityEngine;

public enum ScreenType
{
    Home,
    Gameplay,
    TopHub,
}
public class UIScreen : MonoBehaviour
{
    [SerializeField] protected ScreenType m_type;
    [SerializeField] protected GameObject m_panel;

    public virtual ScreenType Type => m_type;
    public virtual GameObject Panel => m_panel;

    public virtual void Show()
    {
        m_panel.SetActive(true);
    }

    public virtual void Hide()
    {
        m_panel.SetActive(false);
    }
}
