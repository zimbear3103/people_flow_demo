using System;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    [SerializeField] UIScreen[] m_uiScreens;
    [SerializeField] UIPopup[] m_uiPopups;

    private void Start()
    {
        Application.targetFrameRate = 60;
    }

    #region UIScreen
    private void HideAllUIScreen()
    {
        for (int i = 0; i < m_uiScreens.Length; i++)
        {
            m_uiScreens[i].Hide();
        }
    }

    private T GetUIScreen<T>() where T : UIScreen
    {
        foreach (var screen in m_uiScreens)
        {
            if (screen is T)
            {
                return (T)screen;
            }
        }
        return null;
    }
    public void ShowScreen<T>() where T : UIScreen
    {
        HideAllUIScreen();

        var uiScreen = GetUIScreen<T>();
        uiScreen.Show();
    }

    public UIScreen GetUIScreen(ScreenType screen)
    {
        for (int i = 0; i < m_uiScreens.Length; i++)
        {
            if (m_uiScreens[i].Type == screen)
            {
                return m_uiScreens[i];
            }
        }

        return null;
    }
    public void ShowScreen(ScreenType screen)
    {
        HideAllUIScreen();

        var uiScreen = GetUIScreen(screen);
        uiScreen?.Show();
    }

    public ScreenType GetCurrentScreen()
    {
        ScreenType currentScreen = ScreenType.Home;
        for (int i = 0; i < m_uiScreens.Length; i++)
        {
            if (m_uiScreens[i].Panel.activeSelf)
            {
                currentScreen = m_uiScreens[i].Type;
            }
        }
        return currentScreen;
    }
    #endregion

    #region UIPopup
    public void HideAllUIPopup()
    {
        for (int i = 0; i < m_uiPopups.Length; i++)
        {
            m_uiPopups[i].Hide();
        }
    }

    private T GetUIPopup<T>() where T : UIPopup
    {
        foreach (var screen in m_uiPopups)
        {
            if (screen is T)
            {
                return (T)screen;
            }
        }
        return null;
    }
    public void ShowPopup<T>() where T : UIPopup
    {
        HideAllUIPopup();

        var uiScreen = GetUIPopup<T>();
        uiScreen.Show();
    }

    private UIPopup GetUIPopup(PopupType screen)
    {
        for (int i = 0; i < m_uiPopups.Length; i++)
        {
            if (m_uiPopups[i].Type == screen)
            {
                return m_uiPopups[i];
            }
        }

        return null;
    }
    public void ShowPopup(PopupType screen)
    {
        HideAllUIPopup();

        var uiScreen = GetUIPopup(screen);
        uiScreen?.Show();
    }
    #endregion

    public void ShowMessageBox(string title, string content, string textbtn, Action btnCallback, Action btnClose = null)
    {
        var uiMessage = GetUIPopup<UIMessage>();
        if (uiMessage != null)
        {
            uiMessage.OnShowMessageBox(title, content, textbtn, btnCallback, btnClose);
        }
        else
        {
            Debug.LogError("UIMessage popup not found!");
        }
    }

    public void ShowWarning(string warningText = "", float timeHide = 3f)
    {
        var uiWarning = GetUIPopup<UIWarning>();
        if (uiWarning != null)
        {
            uiWarning.ShowWarning(warningText, timeHide);
        }
        else
        {
            Debug.LogError("UIWarning popup not found!");
        }
    }

    public void OnSetBackgroundInHome(Sprite background)
    {
        var uiHome = GetUIScreen<UIHome>();
        if (uiHome != null)
        {
            uiHome.OnSetMainBackground(background);
        }
        else
        {
            Debug.LogError("UIHome screen not found!");
        }
    }

    public void ShowBuyItemPopup(string title, string content, int price, 
        Sprite itemImg, bool canBuyCoin, Action btnUseCoin, Action btnWatchAds, Action btnClose)
    {
        var uiMessage = GetUIPopup<UIMessage>();
        if (uiMessage != null)
        {
            uiMessage.OnShowBuyItemPopup(title,content,price,itemImg, canBuyCoin, btnUseCoin,btnWatchAds, btnClose);
        }
        else
        {
            Debug.LogError("UIMessage popup not found!");
        }
    }
    
    public void OnSetupTutorial(int tutorialLevel)
    {
        var uiTutorial = GetUIPopup<UITutorial>();
        if (uiTutorial != null)
        {
            uiTutorial.OnSetupTutorialData(tutorialLevel);
        }
        else
        {
            GameLog.Log(LogType.Error, "UITutorial popup not found!");
        }
    }

    public void OnFreeDataTutorial()
    {
        var uiTutorial = GetUIPopup<UITutorial>();
        if (uiTutorial != null)
        {
            uiTutorial.OnResetTutorial();
        }
        else
        {
            GameLog.Log(LogType.Error, "UITutorial popup not found!");
        }
    }
}
