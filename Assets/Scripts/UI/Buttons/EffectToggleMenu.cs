using UnityEngine;
using UnityEngine.UI;

public class EffectToggleMenu : MonoBehaviour
{
    [SerializeField] private GameObject m_backgroundButtonOn;
    [SerializeField] private Toggle m_toggleButton;
    [SerializeField] private Transform m_onToggleButtonTrans;
    [SerializeField] private Transform m_offToggleButtonTrans;


    public void OnEnable()
    {
        UpdateStateButton(m_toggleButton.isOn);
    }

    private void Start()
    {
        m_toggleButton.onValueChanged.AddListener(ToggleButtonChanged);
    }

    public void ToggleOnPressed()
    {
        m_backgroundButtonOn.SetActive(true);     
        m_toggleButton.transform.position = m_onToggleButtonTrans.position;
        m_toggleButton.transform.localScale = m_onToggleButtonTrans.localScale;
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonMain);
    }

    public void ToggleOffPressed()
    {
        m_backgroundButtonOn.SetActive(false);
        m_toggleButton.transform.position = m_offToggleButtonTrans.position;
        m_toggleButton.transform.localScale = m_offToggleButtonTrans.localScale;
    }

    private void ToggleButtonChanged(bool isOn)
    {
        if (m_toggleButton.isOn)
        {
            ToggleOnPressed();
        }
        else
        {
            ToggleOffPressed();
        }
    }  

    private void UpdateStateButton(bool isOn) 
    {
        if (m_toggleButton.isOn)
        {
            m_backgroundButtonOn.SetActive(true);
            m_toggleButton.transform.position = m_onToggleButtonTrans.position;
            m_toggleButton.transform.localScale = m_onToggleButtonTrans.localScale;
        }
        else
        {
            m_backgroundButtonOn.SetActive(false);
            m_toggleButton.transform.position = m_offToggleButtonTrans.position;
            m_toggleButton.transform.localScale = m_offToggleButtonTrans.localScale;
        }
    }
}
