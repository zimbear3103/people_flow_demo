using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIAlert : UIPopup
{
    [SerializeField] private Button m_middleButton;
    [SerializeField] private Button m_leftButton;
    [SerializeField] private Button m_rightButton;
    [SerializeField] private Button m_closeButton;

    [SerializeField] private TextMeshProUGUI m_titleText;
    [SerializeField] private TextMeshProUGUI m_contentText;

    public void OnEnable()
    {
        m_middleButton.onClick.AddListener(OnMiddleButtonPressed);
        m_leftButton.onClick.AddListener(OnLeftButtonPressed);
        m_rightButton.onClick.AddListener(OnRightButtonPressed);
        m_closeButton.onClick.AddListener(OnCloseButtonPressed);
    }
    public void OnDisable()
    {
        m_middleButton.onClick.RemoveListener(OnMiddleButtonPressed);
        m_leftButton.onClick.RemoveListener(OnLeftButtonPressed);
        m_rightButton.onClick.RemoveListener(OnRightButtonPressed);
        m_closeButton.onClick.RemoveListener(OnCloseButtonPressed);
    }


    //Restart Button
    private void OnMiddleButtonPressed()
    {

    }

    // Lost coin to continue
    private void OnLeftButtonPressed()
    {

    }

    // Watch ad to continue
    private void OnRightButtonPressed()
    {

    }

    private void OnCloseButtonPressed()
    {
        Hide();
    }

    public void SetTitleText(string title)
    {
        m_titleText.text = title;
    }

    public void SetContentText(string contentText)
    {
        m_contentText.text = contentText;
    }
}
