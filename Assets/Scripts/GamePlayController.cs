using PeopleFlow;
using UnityEngine;

public class GamePlayController : Singleton<GamePlayController>
{
    public enum GameStateType
    {
        None = 0,
        Tutorial,
        Playing,
        SettingIngame,
        Pause,
        Revive,
        StartLevel,
        PlayerDead,
        BossDead,
        EndLevel,
        QuitLevel
    }

    public enum ControlStatusType
    {
        None = 0,
        Enter,
        Update,
        Pause,
        Exit
    }

    [SerializeField] bool m_isLevelDesign = false;
    [SerializeField] bool m_usePeopleFlowPrototype = true;
    [Header("Game State")]
    [SerializeField, ReadOnly] GameStateType m_gameState = GameStateType.None;
    [SerializeField, ReadOnly] GameStateType m_nextGameState = GameStateType.None;
    [SerializeField, ReadOnly] ControlStatusType m_controlStatus = ControlStatusType.None;

    private GameStateType m_saveGameState = GameStateType.None;
    private int m_currentScore;
    private int m_currentLevel;

    private bool m_isPaused;
    private bool m_levelResult;

    private int m_countEnterStartLevel = 1;
    private int m_countLevelRevive = 0;
    private float m_levelTimeSpent = 0;
    private float m_playerDeadWaitingTime = 2;
    private float m_bossDeadWaitingTime = 2;
    private float m_WaitingTimer = 0.0f;
    public bool IsLevelDesign => m_isLevelDesign;
    public bool IsGamePlaying { set; get; } = false;
    public bool SaveIsGamePlaying { set; get; } = false;
    public bool IsPaused => m_isPaused;
    public GameStateType GameState => m_gameState;
    public GameStateType NextGameState => m_nextGameState;
    public GameStateType PrevGameState => m_saveGameState;
    public ControlStatusType ControlStatus => m_controlStatus;
    public bool IsInitialized { set; get; } = false;
    public bool IsTutorial { set; get; } = false;
    public int CountLevelRevive => m_countLevelRevive;

    private void Start()
    {
        IsInitialized = true;

        // People Flow prototype bridge: the module reports win/lose through
        // static events so it stays decoupled from this state machine.
        PeopleFlowGameController.LevelFailed += OnPeopleFlowLevelFailed;
        PeopleFlowGameController.LevelCompleted += OnPeopleFlowLevelCompleted;

        if (!m_isLevelDesign)
        {
            UserProfile.Instance.LoadGameData();
            var uiIngame = (UIInGame)UIManager.Instance.GetUIScreen(ScreenType.Gameplay);
            uiIngame.OnSetCoins(UserProfile.Instance.Coin);
            uiIngame.OnSetLevel(UserProfile.Instance.Level);

#if USE_CHEAT
            CheatGame.Instance.SetupCheat(
                () =>
                {
                    GameLog.Log(LogType.Log, $"win game");

                    if (PeopleFlowGameController.Instance != null)
                        PeopleFlowGameController.Instance.DebugWinLevel();
                },
                () =>
                {
                    GameLog.Log(LogType.Log, $"lose game");

                    if (PeopleFlowGameController.Instance != null)
                        PeopleFlowGameController.Instance.DebugFailLevel();
                }
            );
#endif// USE_CHEAT
        }
    }

    public void OnSetupGameLevel(int level)
    {
        if (m_usePeopleFlowPrototype)
        {
            PeopleFlowGameController.EnsureInstance().BeginLevel(level);
            return;
        }
    }

    private void OnExitInGameState()
    {
        GameLog.Log(LogType.Log, $"-----Game controller------Eixt state-----------:{GameState}  => next menu state: {NextGameState}  => next Main State: {MainStateManager.Instance.NextMainState}");

        if (MainStateManager.Instance.NextMainState != MainStateManager.MainStateType.None)
        {
            MainStateManager.Instance.SetStatus(MainStateManager.MainStatusType.Exit, MainStateManager.Instance.NextMainState);
            SetGameState(GameStateType.None);
            SetGameControlStatus(ControlStatusType.None);
        }
        else
        {
            if (NextGameState != GameStateType.None)
            {
                SetGameState(NextGameState);
            }
            else
            {
                SetGameControlStatus(ControlStatusType.None);
            }
        }
    }

    public void OnUpdate()
    {
        switch (m_gameState)
        {
            case GameStateType.None:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.Tutorial:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                UIManager.Instance.ShowPopup(PopupType.Tutorial);
                                SetGameControlStatus(ControlStatusType.Update);
                            }
                            break;
                        case ControlStatusType.Update:

                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.Playing:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                                IsGamePlaying = true;
                            }
                            break;
                        case ControlStatusType.Update:
                            m_levelTimeSpent += Time.deltaTime;
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.StartLevel:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Exit, GameStateType.Playing);
                                OnFreeData();
                                OnSetupGameLevel(UserProfile.Instance.Level);
                            }
                            break;
                        case ControlStatusType.Update:
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;

            case GameStateType.PlayerDead:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                            }
                            break;
                        case ControlStatusType.Update:
                            {
                                m_WaitingTimer += Time.deltaTime;

                                if (m_WaitingTimer >= m_playerDeadWaitingTime)
                                {
                                    EndLevel();
                                }
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;

            case GameStateType.BossDead:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                            }
                            break;
                        case ControlStatusType.Update:
                            {
                                m_WaitingTimer += Time.deltaTime;

                                if (m_WaitingTimer >= m_bossDeadWaitingTime)
                                {
                                    EndLevel();
                                }
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;

            case GameStateType.Pause:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.Revive:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Exit, GameStateType.Playing);
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.EndLevel:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.SettingIngame:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                                UIManager.Instance.ShowPopup(PopupType.Setting);
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
            case GameStateType.QuitLevel:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                                OnFreeData();
                                MainStateManager.Instance.SetStatus(MainStateManager.MainStatusType.Exit, MainStateManager.MainStateType.MainMenu);
                            }
                            break;
                        case ControlStatusType.Exit:
                            {
                                OnExitInGameState();
                            }
                            break;
                    }
                }
                break;
        }
    }

    public void SetGameState(GameStateType inGameState)
    {
        GameLog.Log(LogType.Log, $"[GameController]  OnSetInGameState InGameState = {GameState} New InGameState = {inGameState} ");
        m_gameState = inGameState;
        SetGameControlStatus(ControlStatusType.Enter);
    }
    private void SetGameControlStatus(ControlStatusType statusInGameState, GameStateType nextInGameState = GameStateType.None, MainStateManager.MainStateType nextMainState = MainStateManager.MainStateType.None)
    {
        GameLog.Log(LogType.Log, $"[GameController] OnSetInGameStatus SetStatus: previous {m_controlStatus} ==> {statusInGameState}");
        GameLog.Log(LogType.Log, $"[GameController] OnSetInGameStatus SetNextSubInGameState: current: {m_controlStatus} ==> new: {nextInGameState}");
        m_controlStatus = statusInGameState;
        m_nextGameState = nextInGameState;

        if (nextMainState != MainStateManager.MainStateType.None)
            MainStateManager.Instance.SetStatus(MainStateManager.Instance.MainStatus, nextMainState);
    }

    public void QuitLevel()
    {
        IsGamePlaying = false;
        m_countEnterStartLevel = 1;
        m_countLevelRevive = 0;

        SetGameControlStatus(ControlStatusType.Exit, GameStateType.QuitLevel);
    }
    public void EndLevel()
    {
        IsGamePlaying = false;
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.EndLevel);
    }

    public void PlayerDead(float waitTime)
    {
        m_playerDeadWaitingTime = waitTime;
        m_WaitingTimer = 0.0f;
        IsGamePlaying = false;
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.PlayerDead);
    }
    public void BossDead(float waitTime)
    {
        m_bossDeadWaitingTime = waitTime;
        m_WaitingTimer = 0.0f;
        IsGamePlaying = false;
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.BossDead);
    }

    public void StartLevel()
    {
        UserProfile.Instance.SaveGameData();

        m_levelTimeSpent = 0;
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.StartLevel);
    }
    public void RestartLevel()
    {
        IsGamePlaying = false;
        m_countEnterStartLevel += 1;
        StartLevel();
    }
    public void ReviveLevel()
    {
        SoundManager.Instance.OnPlayMusic(ESoundId.Bg_MainGame, isLoop: true, volume: 1f);
        IsGamePlaying = false;
        m_countEnterStartLevel += 1;
        m_countLevelRevive += 1;
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.Revive);
    }

    public void EnterSettingPopupStatus()
    {
        SaveIsGamePlaying = IsGamePlaying;
        m_saveGameState = m_gameState;
        IsGamePlaying = false;

        if (m_usePeopleFlowPrototype && PeopleFlowGameController.Instance != null)
            PeopleFlowGameController.Instance.PauseGame();

        SetGameControlStatus(ControlStatusType.Exit, GameStateType.SettingIngame);
    }

    public void LeaveSettingPopupStatus()
    {
        if (IsTutorial)
        {
            SetGameControlStatus(ControlStatusType.Exit, GameStateType.Tutorial);

            if (!m_isPaused)
                IsGamePlaying = true;
        }
        else
        {
            SetGameControlStatus(ControlStatusType.Exit, m_saveGameState);
            IsGamePlaying = SaveIsGamePlaying;

            if (m_usePeopleFlowPrototype && PeopleFlowGameController.Instance != null)
                PeopleFlowGameController.Instance.ResumeGame();
        }
    }

    public void OnTutorial()
    {
        IsTutorial = true;
        UIManager.Instance.OnSetupTutorial(0);
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.Tutorial);
    }
    public void OnPause()
    {
        if (!m_isPaused)
        {
            SaveIsGamePlaying = IsGamePlaying;
            m_isPaused = true;
            IsGamePlaying = false;
            m_saveGameState = m_gameState;
            m_gameState = GameStateType.Pause;
            GameLog.Log(LogType.Log, $"OnPause IsPaused = {IsPaused}  Time.timeScale = {Time.timeScale}");
            SetGameControlStatus(ControlStatusType.Exit, GameStateType.Pause);
        }
    }
    public void OnResume()
    {
        if (m_isPaused)
        {
            m_isPaused = false;
            IsGamePlaying = SaveIsGamePlaying;
            m_gameState = m_saveGameState;
            m_saveGameState = GameStateType.None;
            GameLog.Log(LogType.Log, $"OnResume IsPaused = {IsPaused}  Time.timeScale = {Time.timeScale}");
            SetGameControlStatus(ControlStatusType.Exit, GameStateType.Playing);
        }
    }

    public void OnFreeData()
    {
        if (m_usePeopleFlowPrototype && PeopleFlowGameController.Instance != null)
            PeopleFlowGameController.Instance.QuitLevel();

        if (IsTutorial)
        {
            IsTutorial = false;
            UIManager.Instance.OnFreeDataTutorial();
        }
    }

    public UIInGame OnGetUIIngame()
    {
        var uiIngame = (UIInGame)UIManager.Instance.GetUIScreen(ScreenType.Gameplay);
        return uiIngame;
    }

    #region PeopleFlow Prototype Bridge
    protected override void OnDestroy()
    {
        base.OnDestroy();
        PeopleFlowGameController.LevelFailed -= OnPeopleFlowLevelFailed;
        PeopleFlowGameController.LevelCompleted -= OnPeopleFlowLevelCompleted;
    }

    private void OnPeopleFlowLevelFailed()
    {
        // Short beat so the death particles read before the popup covers them,
        // reusing the existing PlayerDead -> EndLevel state flow.
        const float failPopupDelay = 1.5f;

        PlayerDead(failPopupDelay);
        StartCoroutine(Tweener.IE_DelayForAction(failPopupDelay, () =>
        {
            UIManager.Instance.ShowPopup(PopupType.LevelFail);
        }));
    }

    private void OnPeopleFlowLevelCompleted(int survivors)
    {
        // Reward scales with the surviving crowd, mirroring the genre standard.
        int coinReward = Mathf.Max(1, survivors) * 2;
        UserProfile.Instance.Coin += coinReward;

        if (UserProfile.Instance.Level + 1 < PeopleFlowSampleLevels.Count)
        {
            UserProfile.Instance.Level += 1;
        }
        UserProfile.Instance.SaveGameData();

        var levelCompletePopup = FindAnyObjectByType<UILevelComplete>(FindObjectsInactive.Include);
        if (levelCompletePopup != null)
        {
            levelCompletePopup.SetCoinLeftText(coinReward);
            levelCompletePopup.SetCoinRightText(coinReward * 2);
        }

        EndLevel();
        StartCoroutine(Tweener.IE_DelayForAction(1.2f, () =>
        {
            UIManager.Instance.ShowPopup(PopupType.LevelComplete);
        }));
    }
    #endregion
}
