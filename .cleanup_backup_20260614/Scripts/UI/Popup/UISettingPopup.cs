using Christina.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UISettingPopup : UIPopup
{
    [Header("Setting Buttons")]
    [SerializeField] private ToggleSwitch m_bgmButton;
    [SerializeField] private ToggleSwitch m_sfxButton;
    [SerializeField] private ToggleSwitch m_vibrationButton;

    [Header("Bottom Buttons")]
    [SerializeField] private GameObject m_bottomButtonGroup;
    [SerializeField] private Button m_leftButton;
    [SerializeField] private Button m_rightButton;
    [SerializeField] private Button m_middleButton;
    [SerializeField] private Button m_closeButton;

    private bool m_previouStateBmgButton;
    private bool m_previouStateSfxButton;
    private bool m_previouStateVibrationButton;

    private void OnEnable()
    {
        m_closeButton.onClick.AddListener(OnCloseButtonPressed);

        m_bgmButton.onToggleOn.AddListener(OnBGMButtonToggledOn);
        m_bgmButton.onToggleOff.AddListener(OnBGMButtonToggledOff);
        
        m_sfxButton.onToggleOn.AddListener(OnSFXButtonToggledOn);
        m_sfxButton.onToggleOff.AddListener(OnSFXButtonToggledOff);
        
        m_vibrationButton.onToggleOn.AddListener(OnVibrationButtonToggledOn);
        m_vibrationButton.onToggleOff.AddListener(OnVibrationButtonToggledOff);

        m_leftButton.onClick.AddListener(OnLeftButtonPressed);
        m_rightButton.onClick.AddListener(OnRightButtonPressed);
        m_middleButton.onClick.AddListener(OnMiddleButtonPressed);
    }

    private void OnDisable()
    {
        m_closeButton.onClick.RemoveListener(OnCloseButtonPressed);

        m_bgmButton.onToggleOn.RemoveListener(OnBGMButtonToggledOn);
        m_bgmButton.onToggleOff.RemoveListener(OnBGMButtonToggledOff);
        
        m_sfxButton.onToggleOn.RemoveListener(OnSFXButtonToggledOn);
        m_sfxButton.onToggleOff.RemoveListener(OnSFXButtonToggledOff);
        
        m_vibrationButton.onToggleOn.RemoveListener(OnVibrationButtonToggledOn);
        m_vibrationButton.onToggleOff.RemoveListener(OnVibrationButtonToggledOff);
        
        m_leftButton.onClick.RemoveListener(OnLeftButtonPressed);
        m_rightButton.onClick.RemoveListener(OnRightButtonPressed);
        m_middleButton.onClick.RemoveListener(OnMiddleButtonPressed);
    }

    override public void Show()
    {
        base.Show();

        if (UIManager.Instance.GetCurrentScreen() == ScreenType.Home)
        {
            OnBottomButtonGroupActive(false);
        }
        else if (UIManager.Instance.GetCurrentScreen() == ScreenType.Gameplay)
        {
            OnBottomButtonGroupActive(true);
        }

        m_bgmButton.SetState(UserProfile.Instance.OnMusic);
        m_sfxButton.SetState(UserProfile.Instance.OnSFX);
        m_vibrationButton.SetState(UserProfile.Instance.OnVibration);

        m_previouStateBmgButton = UserProfile.Instance.OnMusic;
        m_previouStateSfxButton = UserProfile.Instance.OnSFX;
        m_previouStateVibrationButton = UserProfile.Instance.OnVibration;
    }

    private void OnBGMButtonToggledOn()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMuteMusic(false);
        UserProfile.Instance.SaveOnMusicGame(true);       
    }

    private void OnBGMButtonToggledOff()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMuteMusic(true);
        UserProfile.Instance.SaveOnMusicGame(false);     
    }

    private void OnSFXButtonToggledOn()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMuteSFX(false);
            SoundManager.Instance.SetMuteVoice(false);
        }
        UserProfile.Instance.SaveOnSFXGame(true);
    }   
    private void OnSFXButtonToggledOff()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMuteSFX(true);
            SoundManager.Instance.SetMuteVoice(true);
        }
        UserProfile.Instance.SaveOnSFXGame(false);
    }
    private void OnVibrationButtonToggledOn()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        UserProfile.Instance.SaveOnVibrationGame(true);
    }
    private void OnVibrationButtonToggledOff()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        UserProfile.Instance.SaveOnVibrationGame(false);       
    }

    //Button restart level
    private void OnLeftButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        Hide();

        UserProfile.Instance.SaveGameData();

        GamePlayController.Instance.RestartLevel();       
    }

    //Button Home
    private void OnRightButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        Hide();

        UserProfile.Instance.SaveGameData();

        GamePlayController.Instance.QuitLevel();       
    }

    //Button Continue
    private void OnMiddleButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        Hide();
        GamePlayController.Instance.LeaveSettingPopupStatus();        
    }

    private void OnCloseButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonNegative);
        Hide();
        GamePlayController.Instance.LeaveSettingPopupStatus();       
    }

    public void OnBottomButtonGroupActive(bool isActive)
    {
        m_bottomButtonGroup.SetActive(isActive);
    }
}
