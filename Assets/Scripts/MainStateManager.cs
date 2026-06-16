using System;
using System.Collections;
using UnityEngine;

public class MainStateManager : PersistenceSingleton<MainStateManager>
{
    public enum MainStateType
    {
        None = 0,
        SplashScreen,
        Loading,
        MainMenu,
        Gameplay,
    }

    public enum MainStatusType
    {
        None = 0,
        Enter,
        Update,
        Exit
    }

    [Header("State Properties")]
    [SerializeField, ReadOnly] MainStateType m_previousMainState;
    [SerializeField, ReadOnly] MainStateType m_mainState;
    [SerializeField, ReadOnly] MainStateType m_nextMainState;
    [SerializeField, ReadOnly] MainStatusType m_mainStatus;

    public MainStateType MainState => m_mainState;
    public MainStateType NextMainState => m_nextMainState;
    public MainStateType PreviousMainStatate => m_previousMainState;
    public MainStatusType MainStatus => m_mainStatus;
    public bool IsInitialized { set; get; } = false;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => (LoadingController.Instance != null && LoadingController.Instance.IsInitialized));
        IsInitialized = true;
        SetMainState(MainStateType.Loading);
    }

    private void Update()
    {
        switch (m_mainState)
        {
            case MainStateType.None:
                break;
            case MainStateType.SplashScreen:
                switch (m_mainStatus)
                {
                    case MainStatusType.Enter:
                        SetStatus(MainStatusType.Update);
                        StartCoroutine(LoadingController.Instance.StopSplashScreen());
                        break;
                    case MainStatusType.Update:
                        break;
                    case MainStatusType.Exit:
                        OnExitMainState();
                        break;
                }
                break;

            case MainStateType.Loading:
                switch (m_mainStatus)
                {
                    case MainStatusType.Enter:
                        SetStatus(MainStatusType.Update);
                        LoadingController.Instance.ShowLoadingScreen(CallbackLoadingScreen);
                        break;
                    case MainStatusType.Update:
                        break;
                    case MainStatusType.Exit:
                        OnExitMainState();
                        break;
                }
                break;

            case MainStateType.MainMenu:
                switch (m_mainStatus)
                {
                    case MainStatusType.Enter:
                        UIManager.Instance.ShowScreen(ScreenType.Home);
                        SetStatus(MainStatusType.Update);
                        break;
                    case MainStatusType.Update:
                        break;
                    case MainStatusType.Exit:
                        OnExitMainState();
                        break;
                }
                break;
            case MainStateType.Gameplay:
                switch (m_mainStatus)
                {
                    case MainStatusType.Enter:
                        SetStatus(MainStatusType.Update);
                        UIManager.Instance.ShowScreen(ScreenType.Gameplay);
                        SoundManager.Instance.OnPlayMusic(ESoundId.Bg_MainGame, isLoop: true, volume: 1f);
                        GamePlayController.Instance.StartLevel();
                        break;
                    case MainStatusType.Update:
                        GamePlayController.Instance.OnUpdate();
                        break;
                    case MainStatusType.Exit:
                        OnExitMainState();
                        break;
                }
                break;
        }

    }

    public void CallbackLoadingScreen()
    {
        SetStatus(MainStatusType.Exit, MainStateType.MainMenu);
    }
    #region State Management
    private void OnExitMainState()
    {
        if (NextMainState != MainStateType.None)
        {
            SetMainState(NextMainState);
        }
        else
        {
            SetStatus(MainStatusType.None);
        }
    }

    public void SetStatus(MainStatusType status, MainStateType nextMainState = MainStateType.None)
    {
        GameLog.Log(LogType.Log, $"[MainStateManager] SetStatus: current status {m_mainStatus} ==> new {status}");
        GameLog.Log(LogType.Log, $"[MainStateManager] SetStatus: current state {m_mainState} ==> new {nextMainState}");

        m_mainStatus = status;
        m_nextMainState = nextMainState;
    }

    public void SetMainState(MainStateType mainState)
    {
        GameLog.Log(LogType.Log, $"[MainStateManager] SetMainState: current state ==> new {mainState}");
        m_previousMainState = m_mainState;
        m_mainState = mainState;
        SetStatus(MainStatusType.Enter);
    }
    #endregion
}
