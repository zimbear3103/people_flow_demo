using System;
using System.Collections;
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
        Win,
        Lose,
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
    [Header("PeopleFlow level")]
    [Tooltip("Authored LevelData assets (e.g. baked by ExportLevelData). OnSetupGameLevel picks one by level index.")]
    [SerializeField] LevelData[] m_levels;
    [Header("Game State")]
    [SerializeField, ReadOnly] GameStateType m_gameState = GameStateType.None;
    [SerializeField, ReadOnly] GameStateType m_nextGameState = GameStateType.None;
    [SerializeField, ReadOnly] ControlStatusType m_controlStatus = ControlStatusType.None;

    private GameStateType m_saveGameState = GameStateType.None;
    private int m_currentScore;
    //private int m_currentLevel;

    private bool m_isPaused;
    private bool m_levelResult;
    private bool m_resultShown;

    private int m_countEnterStartLevel = 1;
    private int m_countLevelRevive = 0;
    private float m_levelTimeSpent = 0;
    private float m_playerDeadWaitingTime = 0.5f;
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

    public int LevelCount => m_levels != null ? m_levels.Length : 0;
    public bool HasNextLevel => (UserProfile.Instance.Level + 1) < LevelCount;

    LevelData m_currentLevel;

    public int TotalHoles { get; private set; }
    public int CompletedHoles { get; private set; }
    public LoseReason LastLoseReason { get; private set; }
    public event Action<int, int> OnHoleProgress;
    public event Action OnLevelWin;
    public event Action<LoseReason> OnLevelLose;

    public bool IsPlaying => IsGamePlaying;

    public void ConfigureAsStandalone() => m_isLevelDesign = true;

    private void Start()
    {
        IsInitialized = true;

        if (!m_isLevelDesign)
        {
            UserProfile.Instance.LoadGameData();
            var uiIngame = (UIInGame)UIManager.Instance.GetUIScreen(ScreenType.Gameplay);
            uiIngame.OnSetCoins(UserProfile.Instance.Coin);
            uiIngame.OnSetLevel(UserProfile.Instance.Level);

#if USE_CHEAT
            //CheatGame.Instance.SetupCheat(
            //    () =>
            //    {
            //        GameLog.Log(LogType.Log, $"win game");
            //        ForceWin();
            //    },
            //    () =>
            //    {
            //        GameLog.Log(LogType.Log, $"lose game");
            //        ForceLose();
            //    }
            //);
#endif// USE_CHEAT
        }
    }

    public void OnSetupGameLevel(int level)
    {
        TeardownPeopleFlow();

        if (m_levels == null || m_levels.Length == 0)
        {
            GameLog.Log(LogType.Error, "[GamePlayController] No LevelData assigned in 'm_levels' — cannot set up a PeopleFlow level.");
            return;
        }
        m_currentLevel = m_levels[Mathf.Clamp(level, 0, m_levels.Length - 1)];
        if (m_currentLevel == null)
        {
            GameLog.Log(LogType.Error, $"[GamePlayController] LevelData at index {level} is null — cannot set up the level.");
            return;
        }


        var input = InputManager.Instance.GetComponent<InputManager>();
        var timer = Timer.Instance.GetComponent<Timer>();

        LevelManager.Instance.Build(m_currentLevel, input, timer);
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

            case GameStateType.Lose:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                                // Show the fail popup immediately. Waiting first would leave the game
                                // frozen (IsGamePlaying is already false) with no popup and dead input
                                // for the delay — which reads as "stuck input". Popup buttons drive next.
                                if (!m_resultShown)
                                {
                                    m_resultShown = true;
                                    UIManager.Instance.ShowPopup(PopupType.LevelFail);
                                }
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

            case GameStateType.Win:
                {
                    switch (m_controlStatus)
                    {
                        case ControlStatusType.Enter:
                            {
                                GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                                SetGameControlStatus(ControlStatusType.Update);
                                // Show the win popup immediately (see Lose state) — no frozen, input-dead
                                // delay before it appears. Popup buttons drive what happens next.
                                if (!m_resultShown)
                                {
                                    m_resultShown = true;
                                    UIManager.Instance.ShowPopup(PopupType.LevelComplete);
                                }
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

    public IEnumerator PlayerLose(float waitTime)
    {
        EventManager.Instance.Emit("lose");
        m_playerDeadWaitingTime = waitTime;
        m_WaitingTimer = 0.0f;
        IsGamePlaying = false;
        yield return new WaitForSeconds(waitTime);
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.Lose);
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
        TeardownPeopleFlow();
        if (IsTutorial)
        {
            IsTutorial = false;
            UIManager.Instance.OnFreeDataTutorial();
        }
    }

    public void BeginLevel(int totalHoles)
    {
        TotalHoles = totalHoles;
        CompletedHoles = 0;
        LastLoseReason = LoseReason.None;
        m_resultShown = false;
        m_WaitingTimer = 0.0f;
        Time.timeScale = 1f;
        IsGamePlaying = true;
        OnHoleProgress?.Invoke(CompletedHoles, TotalHoles);
    }

    public void ReportHoleCompleted(Hole hole)
    {
        if (!IsGamePlaying) return;
        CompletedHoles++;
        OnHoleProgress?.Invoke(CompletedHoles, TotalHoles);
        if (CompletedHoles >= TotalHoles) WinPeopleFlow();
    }

    public void ReportRunwayJam() { if (IsGamePlaying) LosePeopleFlow(LoseReason.RunwayFull); }
    public void ReportTimeOut() { if (IsGamePlaying) LosePeopleFlow(LoseReason.TimeOut); }

    public void ForceWin() { if (IsGamePlaying) WinPeopleFlow(); }
    public void ForceLose() { if (IsGamePlaying) LosePeopleFlow(LoseReason.RunwayFull); }

    void WinPeopleFlow()
    {
        IsGamePlaying = false;
        OnLevelWin?.Invoke();
        PlayerWin(m_bossDeadWaitingTime);
    }

    void LosePeopleFlow(LoseReason reason)
    {
        LastLoseReason = reason;
        IsGamePlaying = false;
        OnLevelLose?.Invoke(reason);
        StartCoroutine(PlayerLose(m_playerDeadWaitingTime));
    }

    public void PlayerWin(float waitTime)
    {
        m_bossDeadWaitingTime = waitTime;
        m_WaitingTimer = 0.0f;
        IsGamePlaying = false;
        SetGameControlStatus(ControlStatusType.Exit, GameStateType.Win);
    }

    void TeardownPeopleFlow()
    {
        Time.timeScale = 1f;
        m_isPaused = false;
        LevelManager.Instance.Clear();
    }

    public UIInGame OnGetUIIngame()
    {
        var uiIngame = (UIInGame)UIManager.Instance.GetUIScreen(ScreenType.Gameplay);
        return uiIngame;
    }
}
