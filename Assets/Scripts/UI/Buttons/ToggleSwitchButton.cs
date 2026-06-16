using Christina.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToggleSwitchButton : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite m_toggleOn;
    [SerializeField] private Sprite m_handleOn;
    [SerializeField] private Sprite m_toggleOff;
    [SerializeField] private Sprite m_handleOff;

    [Header("Toggle Switch")]
    [SerializeField] private Image m_background;
    [SerializeField] private Image m_handle;
    [SerializeField] private TextMeshProUGUI m_textHandle;

    private ToggleSwitch m_toggle;

    private void Start()
    {
        m_toggle = GetComponentInChildren<ToggleSwitch>();
    }

    public void ToggleOnPressed()
    {
        m_background.sprite = m_toggleOn;
        m_handle.sprite = m_handleOn;
        m_textHandle.text = "On";
    }
    public void ToggleOffPressed()
    {
        m_background.sprite = m_toggleOff;
        m_handle.sprite = m_handleOff;
        m_textHandle.text = "Off";
    }
}
