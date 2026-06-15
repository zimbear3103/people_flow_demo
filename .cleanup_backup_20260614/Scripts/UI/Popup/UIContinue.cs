using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIContinue : UIPopup
{
    [SerializeField] private Button m_closeButton;
    [SerializeField] private Button m_leftButton;
    [SerializeField] private Button m_rightButton;

    [SerializeField] private TextMeshProUGUI m_textLeftButton;

    private void OnEnable()
    {
        m_closeButton.onClick.AddListener(OnCloseButtonPressed);
        m_leftButton.onClick.AddListener(OnLeftButtonPressed);
        m_rightButton.onClick.AddListener(OnRightButtonPressed);
    }

    private void OnDisable()
    {
        m_closeButton.onClick.RemoveListener(OnCloseButtonPressed);
        m_leftButton.onClick.RemoveListener(OnLeftButtonPressed);
        m_rightButton.onClick.RemoveListener(OnRightButtonPressed);
    }

    public override void Show()
    {
        base.Show();
        OnCheckReviveRemain();
        SoundManager.Instance.OnPlayMusic(ESoundId.Bg_Deafeat, isLoop: false, volume: 1f);
    }

    private void OnCheckReviveRemain()
    {
        //if (GamePlayController.Instance.CountLevelRevive < 0)
        //{
        //    if (UserProfile.Instance.Coin >= 100)
        //    {
        //        m_leftButton.interactable = true;
        //    }
        //    else
        //    {
        //        m_leftButton.interactable = false;
        //    }

        //    if (FirebaseManager.Instance.RemoteConfig.Data.Revive.revive_enable_coin)
        //    {
        //        m_leftButton.gameObject.SetActive(true);
        //    }
        //    else
        //    {
        //        m_leftButton.gameObject.SetActive(false);
        //    }
        //}
        //else
        //{
        //    m_leftButton.interactable = false;
        //    m_rightButton.interactable = false;
        //}
    }
    private void OnCloseButtonPressed()
    {
        Hide();

        UserProfile.Instance.SaveGameData();

        UIManager.Instance.ShowPopup(PopupType.LevelFail);
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonNegative);        
    }

    //Lost coin to continue
    private void OnLeftButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        Hide();

        UserProfile.Instance.SaveGameData();

        GamePlayController.Instance.ReviveLevel();
    }

    //Watch ad to continue
    private void OnRightButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        UserProfile.Instance.SaveGameData();
    }
}
