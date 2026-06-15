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

    public int UnlockItem { set; get; } = 0;
    public int CrateItem { set; get; } = 0;
    public int LongRangeItem { set; get; } = 0;
    public int SortItem { set; get; } = 0;
    public int GunOverloadItem { set; get; } = 0;

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

        UnlockItem = PlayerPrefs.GetInt("UserUnlockItem", 0);
        CrateItem = PlayerPrefs.GetInt("UserCrateItem", 0);
        LongRangeItem = PlayerPrefs.GetInt("UserLongRangeItem", 0);
        SortItem = PlayerPrefs.GetInt("UserSortItem", 0);
        GunOverloadItem = PlayerPrefs.GetInt("UserGunOverloadItem", 0);
        OnMusic = PlayerPrefs.GetInt("UserOnMusic", 1) == 1;
        OnSFX = PlayerPrefs.GetInt("UserOnSFX", 1) == 1;
        OnVibration = PlayerPrefs.GetInt("UserOnVibration", 1) == 1;
    }

    public void SaveItemGame(ItemTypes itemType, int quantity)
    {
        switch (itemType)
        {
            case ItemTypes.Unlock:
                UnlockItem = quantity;
                PlayerPrefs.SetInt("UserUnlockItem", UnlockItem);
                break;
            case ItemTypes.Crate:
                CrateItem = quantity;
                PlayerPrefs.SetInt("UserCrateItem", CrateItem);
                break;
            case ItemTypes.LongRange:
                LongRangeItem = quantity;
                PlayerPrefs.SetInt("UserLongRangeItem", LongRangeItem);
                break;
            case ItemTypes.Sort:
                SortItem = quantity;
                PlayerPrefs.SetInt("UserSortItem", SortItem);
                break;
            case ItemTypes.GunOverload:
                GunOverloadItem = quantity;
                PlayerPrefs.SetInt("UserGunOverloadItem", GunOverloadItem);
                break;
        }
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
