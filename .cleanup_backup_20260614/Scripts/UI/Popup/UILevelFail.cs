using UnityEngine;
using UnityEngine.UI;

public class UILevelFail : UIPopup
{
    [SerializeField] private Button m_leftButton;
    [SerializeField] private Button m_rightButton;

    private void OnEnable()
    {
        m_leftButton.onClick.AddListener(OnLeftButtonPressed);
        m_rightButton.onClick.AddListener(OnRightButtonPressed);
    }

    private void OnDisable()
    {
        m_leftButton.onClick.RemoveListener(OnLeftButtonPressed);
        m_rightButton.onClick.RemoveListener(OnRightButtonPressed);
    }

    private void OnLeftButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        Hide();                
        GamePlayController.Instance.QuitLevel();
    }

    private void OnRightButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        Hide();
        GamePlayController.Instance.RestartLevel();
    }
}
