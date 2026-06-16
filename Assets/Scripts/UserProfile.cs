using UnityEngine;
using System;

public class UserProfile : PersistenceSingleton<UserProfile>
{
    public Action<int> OnLevelChanged;
    private int m_level;
    public Action<int> OnScoreChanged;
    private int m_coin;
    public int Coin
    {
        get => m_coin;
        set
        {
            if (m_coin != value)
            {
                m_coin = value;
                OnScoreChanged?.Invoke(m_coin);
            }
        }
    }

    public int Level
    {
        get => m_level;
        set
        {
            if (m_level != value)
            {
                m_level = value;
                OnLevelChanged?.Invoke(m_level);
            }
        }
    }

    //SettingPopup
    public bool OnMusic { set; get; } = true;
    public bool OnSFX { set; get; } = true;
    public bool OnVibration { set; get; } = true;

    public bool IsInitialized { set; get; } = false;

    private void Start()
    {
        Debug.Log("[UserProfile] called Start");
        LoadGameData();
        IsInitialized = true;
    }

    public void SaveGameData()
    {
        PlayerPrefs.SetInt("UserCoin", Coin);
        PlayerPrefs.SetInt("UserLevel", Level);

        PlayerPrefs.SetInt("UserOnMusic", OnMusic ? 1 : 0);
        PlayerPrefs.SetInt("UserOnSFX", OnSFX ? 1 : 0);
    }

    public void LoadGameData()
    {
        Debug.Log("[UserProfile] called LoadGameData");
        Coin = PlayerPrefs.GetInt("UserCoin", 0);
        Level = PlayerPrefs.GetInt("UserLevel", 0);

        OnMusic = PlayerPrefs.GetInt("UserOnMusic", 1) == 1;
        OnSFX = PlayerPrefs.GetInt("UserOnSFX", 1) == 1;
        OnVibration = PlayerPrefs.GetInt("UserOnVibration", 1) == 1;
    }

    public void SaveOnMusicGame(bool isOn)
    {
        OnMusic = isOn;
        PlayerPrefs.SetInt("UserOnMusic", OnMusic ? 1 : 0);
    }

    public void SaveOnSFXGame(bool isOn)
    {
        OnSFX = isOn;
        PlayerPrefs.SetInt("UserOnSFX", OnSFX ? 1 : 0);
    }

    public void SaveOnVibrationGame(bool isOn)
    {
        OnVibration = isOn;
        PlayerPrefs.SetInt("UserOnVibration", OnVibration ? 1 : 0);
    }
}
