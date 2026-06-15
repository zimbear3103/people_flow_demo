
using PeopleFlow;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using UnityEngine.UI;

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
    [ReadOnly]
    [SerializeField] GameLevels m_gameLevels;
    [SerializeField] Image m_bgBottomImage;
    [SerializeField] Image m_bgTopImage;
 
    //[SerializeField] GameObject m_dragonPrefab;
    [SerializeField] GameObject m_mainCharacterPrefab;
    //[SerializeField] GameObject m_ammoBoxDataPrefab;
    //[SerializeField] GameObject m_splinePathPrefab;
    [SerializeField] GameObject m_canonHolderPrefab;
    [SerializeField] GameObject m_conveyBeltPrefab;
    [SerializeField] GameObject m_fogPrefab;

    [SerializeField] Transform m_gameZoneLocation;
    [SerializeField] Transform m_canonHolderLocation;
    [SerializeField] Transform m_capyLocation;
    [SerializeField] Transform m_ammoBoxLocation;
    [SerializeField] Transform m_conveyBeltLocation;
    [SerializeField] Transform m_movePathLocation;
    [SerializeField] Transform m_fogLocation;
    [ReadOnly]
    [SerializeField] List<BoxAmmo> m_boxAmmoList = new List<BoxAmmo>();
    [SerializeField] List<GameObject> m_boxAmmoPrefabList = new List<GameObject>();
    [SerializeField] GameObject m_portalPrefab;

    [Header("Game State")]
    [SerializeField, ReadOnly] GameStateType m_gameState = GameStateType.None;
    [SerializeField, ReadOnly] GameStateType m_nextGameState = GameStateType.None;
    [SerializeField, ReadOnly] ControlStatusType m_controlStatus = ControlStatusType.None;

    private GameStateType m_saveGameState = GameStateType.None;      
    private int m_currentScore;
    private int m_currentLevel;

    private bool m_isPaused;
    private bool m_levelResult;

    private GameObject m_dragonObj;
    private GameObject m_capyObj;
    private List<GameObject> m_ammoBoxObjects = new List<GameObject>();
    private List<GameObject> m_portalObjects = new List<GameObject>();
    private GameObject m_splinePathObj;
    private GameObject m_canonHolderObj;
    private GameObject m_conveyBeltSlotObj;
    private GameObject m_fogObj;
    private GameObject m_splineCapyPathObj;

    private LevelData m_currentLevelData;
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
    public LevelData CurrentLevelData => m_currentLevelData;
    public bool IsInitialized { set; get; } = false;
    public bool IsUsingConveyBelt { set; get; } = false;     
    public List<GameObject> AmmoBoxObjects => m_ammoBoxObjects;

    private static readonly string[] ColorMapValue = { "#ff4031", "#ffd112", "#ff31ab", "#31efff", "#3192ff", "#bb31ff", "#31ffa0" };
    public bool IsTutorial { set; get; } = false;
    public int CountLevelRevive => m_countLevelRevive;
    static public string ConvertEColorType2Hex(EColorType colorType)
    {
        return ColorMapValue[(int)colorType];
    }
    static public EColorType ConvertHexColor2EColorType(string hexColor)
    {
        EColorType colorType = EColorType.Red;

        for (int i = 0; i < ColorMapValue.Length; i++)
            if (hexColor == ColorMapValue[i])
            {
                colorType = (EColorType)i;
                break;
            }

        return colorType;
    }

    public Boss GetBoss()
    {
        if (m_dragonObj != null)
        {
            Boss boss = m_dragonObj.GetComponent<Boss>();
            return boss;
        }

        GameLog.Log(LogType.Error, $"!!!GetBoss error!");
        return null;
    } 
    
    public Capy GetCapy()
    {
        if (m_capyObj != null)
        {
            Capy capy = m_capyObj.GetComponent<Capy>();
            return capy;
        }

        GameLog.Log(LogType.Error, $"!!!GetCapy error!");
        return null;
    }   
    public ConveyBelt GetConveyBeltSlot()
    {
        if (m_conveyBeltSlotObj != null)
        {
            ConveyBelt conveyBelt = m_conveyBeltSlotObj.GetComponent<ConveyBelt>();
            return conveyBelt;
        }

        GameLog.Log(LogType.Error, $"!!!GetConveyBeltSlot error!");
        return null;
    }    
    
    public CanonHolder GetCanonHolder()
    {
        if (m_canonHolderObj != null)
        {
            CanonHolder canonHolder = m_canonHolderObj.GetComponent<CanonHolder>();
            return canonHolder;
        }

        GameLog.Log(LogType.Error, $"!!!GetCanonHolder error!");
        return null;
    }   
    
    private void Start()
    {
        IsInitialized = true;

        // People Flow prototype bridge: the module reports win/lose through
        // static events so it stays decoupled from this state machine.
        PeopleFlowGameController.LevelFailed += OnPeopleFlowLevelFailed;
        PeopleFlowGameController.LevelCompleted += OnPeopleFlowLevelCompleted;

        if (!m_isLevelDesign)
        {
            if (AssetManager.Instance != null)
            {
                m_gameLevels = AssetManager.Instance.GameLevels;
                Debug.Log($"[GamePlayController] Loaded legacy game levels: {(m_gameLevels != null ? m_gameLevels.Datas.Count : 0)}");
            }

            UserProfile.Instance.LoadGameData();
            var uiIngame = (UIInGame)UIManager.Instance.GetUIScreen(ScreenType.Gameplay);
            uiIngame.OnSetCoins(UserProfile.Instance.Coin);
            uiIngame.OnSetLevel(UserProfile.Instance.Level);

#if USE_CHEAT
            CheatGame.Instance.SetupCheat(
                () =>
                {
                    GameLog.Log(LogType.Log, $"win game");

                    if (m_usePeopleFlowPrototype)
                    {
                        if (PeopleFlowGameController.Instance != null)
                            PeopleFlowGameController.Instance.DebugWinLevel();
                    }
                    else
                    {
                        StartCoroutine(GetBoss().OnBossSuicide());
                    }
                },
                () =>
                {
                    GameLog.Log(LogType.Log, $"lose game");

                    if (m_usePeopleFlowPrototype)
                    {
                        if (PeopleFlowGameController.Instance != null)
                            PeopleFlowGameController.Instance.DebugFailLevel();
                    }
                    else
                    {
                        GetCapy().OnSetState(Capy.ECapyState.Death);
                    }
                }
            );
#endif// USE_CHEAT
        }
    }

    private void OnSetupAmmoBox(CustomLevelData boxAmmoLevelData)
    {
        for (int i = 0; i < boxAmmoLevelData.AllPortalData.Count; i++)
        {
            PortalData portalData = boxAmmoLevelData.AllPortalData[i];
            GameObject portalObj = Instantiate(m_portalPrefab, m_ammoBoxLocation);
            Portal portal = portalObj.GetComponent<Portal>();
                           
            portalObj.transform.localPosition = portalData.Position;
            portalObj.transform.localScale = portalData.Scale;
            m_portalObjects.Add(portalObj);
        }

        for(int i = 0; i < boxAmmoLevelData.AllBoxAmmoData.Count; i++)
        {
            BoxAmmoData boxAmmoData = boxAmmoLevelData.AllBoxAmmoData[i];
            GameObject boxAmmoObj = CreateBoxAmmo(boxAmmoData.BoxType, boxAmmoData.MovementDirection);

            boxAmmoObj.transform.localPosition = boxAmmoData.Position;
            boxAmmoObj.transform.localScale = boxAmmoData.Scale;
            boxAmmoObj.name = boxAmmoObj.name.Replace("(Clone)", "") + $"_{i + 1}";

            BoxAmmo boxAmmo = boxAmmoObj.GetComponent<BoxAmmo>();
            boxAmmo.OnSetData(boxAmmoData);

            m_ammoBoxObjects.Add(boxAmmoObj);
            m_boxAmmoList.Add(boxAmmo);

            if (boxAmmoData.IsInConveyBelt)
                IsUsingConveyBelt = true;

            if(boxAmmoData.PortalID >= 0)
            {
                m_portalObjects[boxAmmoData.PortalID].GetComponent<Portal>().OnAddBoxAmmo(boxAmmoObj);
                boxAmmo.OnActiveNextBoxAmmoInPortal = m_portalObjects[boxAmmoData.PortalID].GetComponent<Portal>().OnActiveNextBoxAmmo;
            }    
        }    
    }

    private GameObject CreateBoxAmmo(BoxType boxType, EMovementDirection moveDir)
    {
        GameObject boxAmmo = null;

        switch(boxType )
        {
            case BoxType.Small:
                {
                    if(moveDir == EMovementDirection.Up || moveDir == EMovementDirection.Down)
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[0], m_ammoBoxLocation);
                    } 
                    else if(moveDir == EMovementDirection.Left || moveDir == EMovementDirection.Right)
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[3], m_ammoBoxLocation);
                    }
                    else 
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[6], m_ammoBoxLocation);
                    }
                    break;
                }
            case BoxType.Medium:
                {
                    if (moveDir == EMovementDirection.Up || moveDir == EMovementDirection.Down)
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[1], m_ammoBoxLocation);
                    }
                    else if (moveDir == EMovementDirection.Left || moveDir == EMovementDirection.Right)
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[4], m_ammoBoxLocation);
                    }
                    else
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[7], m_ammoBoxLocation);
                    }
                    break;
                }
            case BoxType.Big:
                {
                    if (moveDir == EMovementDirection.Up || moveDir == EMovementDirection.Down)
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[2], m_ammoBoxLocation);
                    }
                    else if (moveDir == EMovementDirection.Left || moveDir == EMovementDirection.Right)
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[5], m_ammoBoxLocation);
                    }
                    else
                    {
                        boxAmmo = Instantiate(m_boxAmmoPrefabList[8], m_ammoBoxLocation);
                    }
                    break;
                }
        }    

        return boxAmmo;
    }   
    
    public void OnSetupGameLevel(int level)
    {
        if (m_usePeopleFlowPrototype)
        {
            PeopleFlowGameController.EnsureInstance().BeginLevel(level);
            return;
        }

        //int indexLevel = FirebaseManager.Instance.RemoteConfig.Data.LevelDesign.GetLevelIdByIndex(level);

        //if (indexLevel < m_gameLevels.Datas.Count && indexLevel >= 0)
        //{
        //    LevelData ld = m_gameLevels.Datas[indexLevel];
        //    m_currentLevelData = ld;

        //    var levelConfig = FirebaseManager.Instance.RemoteConfig.Data.LevelDesign.GetLevelConfigByLevel(level);

        //    m_canonHolderObj = Instantiate(m_canonHolderPrefab, m_canonHolderLocation);

        //    if (levelConfig != null)
        //    {
        //        var slot = levelConfig.slot;
        //        m_canonHolderObj.GetComponent<CanonHolder>().OnSetupDataBulletSlot(slot);
        //    }
        //    else
        //    {
        //        m_canonHolderObj.GetComponent<CanonHolder>().OnSetupDataBulletSlot(ld.BoxAmmoLevelData.slotsActive);
        //    }
        //    //
        //    OnSetupAmmoBox(ld.BoxAmmoLevelData);
        //    m_bgBottomImage.color = ld.BackgroundColor;
        //    m_bgTopImage.sprite = ld.BackgroundImage;

        //    m_splinePathObj = Instantiate(ld.BossPathPrefab, m_movePathLocation);

        //    m_capyObj = Instantiate(m_mainCharacterPrefab, m_capyLocation);

        //    m_splineCapyPathObj = Instantiate(ld.CapyPathPrefab, m_movePathLocation);
         
        //    if (m_splineCapyPathObj != null)
        //    {
        //        GetCapy().OnSetupDataCapy(m_splineCapyPathObj.GetComponent<SplineContainer>(), ld.CapyConfig);
        //    }      
        //    else
        //    {
        //        Debug.LogError("m_splineCapyPathObj is null!!!");
        //    }

        //    if (IsUsingConveyBelt)
        //    {
        //        m_conveyBeltSlotObj = Instantiate(m_conveyBeltPrefab, m_conveyBeltLocation);
        //        GetConveyBeltSlot().InitConveyBelt(m_boxAmmoList);
        //    }

        //    OnCreateFog(levelConfig, ld);

        //    //create boss object
        //    m_dragonObj = new GameObject("Boss");

        //    m_dragonObj.transform.SetParent(m_movePathLocation);
        //    m_dragonObj.transform.localPosition = Vector3.zero;
        //    m_dragonObj.transform.localRotation = Quaternion.identity;
        //    m_dragonObj.transform.localScale = Vector3.one;
        //    m_dragonObj.AddComponent<Boss>();
        //    m_dragonObj.AddComponent<SortingGroup>();
        //    //m_dragonObj = Instantiate(obj, m_gameZoneLocation);

        //    Boss dragon = m_dragonObj.GetComponent<Boss>();
        //    dragon.OnSetupData(ld.BossConfig, m_splinePathObj.GetComponent<SplineContainer>(), m_boxAmmoList);

        //    SortingGroup sortingGroup = m_dragonObj.GetComponent<SortingGroup>();
        //    sortingGroup.sortingLayerName = "Gameplay";
        //}   
        //else
        //{
        //    UIManager.Instance.ShowMessageBox("Error", "The selected level is invalid.\nPlease return to the home screen!", "Accept",
        //    () =>
        //    {                    
        //        UIManager.Instance.ShowScreen(ScreenType.Home);
        //    },
        //    () =>
        //    {                    
        //        UIManager.Instance.ShowScreen(ScreenType.Home);
        //    });
        //}
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

                                if (!m_usePeopleFlowPrototype)
                                    GetBoss().OnSetStatus(Boss.EBossStatus.Running);
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
                                GetBoss().OnRevive();
                                GetCapy().OnRevive();                                    
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
                            //{
                            //    GameLog.Log(LogType.Log, $"-----Ingame------Enter state-----------:{m_gameState}");
                            //    SetGameControlStatus(ControlStatusType.Update);

                            //    if (GetCapy().CapyState == Capy.ECapyState.Death)
                            //    {
                            //        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsInitialized)
                            //        {
                            //            if (CountLevelRevive < FirebaseManager.Instance.RemoteConfig.Data.Revive.revive_times || FirebaseManager.Instance.RemoteConfig.Data.Revive.revive_times == 99)
                            //            {
                            //                UIManager.Instance.ShowPopup(PopupType.Continue);
                            //            }
                            //            else
                            //            {
                            //                UIManager.Instance.ShowPopup(PopupType.LevelFail);
                            //            }
                            //        }
                            //        else
                            //        {
                            //            UIManager.Instance.ShowPopup(PopupType.LevelFail);
                            //            GameLog.Log(LogType.Error, "FirebaseManager is not Initialized!");
                            //        }
                            //        TrackingLevelFail();
                            //    }
                            //    else if (GetBoss().BossStatus == Boss.EBossStatus.Death)
                            //    {
                            //        UIManager.Instance.ShowPopup(PopupType.LevelComplete);

                            //        if (UserProfile.Instance.Level + 1 < m_gameLevels.Datas.Count)
                            //        {
                            //            UserProfile.Instance.Level += 1;
                            //        }

                            //        TrackingData.Instance.ResetTrackingData();
                            //        UserProfile.Instance.SaveGameData();
                            //        TrackingData.Instance.SaveTrackingData();

                            //        TrackingLevelUp();
                            //    }
                            //}
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

        m_currentLevelData = null;

        foreach (var obj in m_ammoBoxObjects)
            Destroy(obj);
        
        foreach (var obj in m_portalObjects)
            Destroy(obj);        
        
        if (IsTutorial)
        {
            IsTutorial = false;
            UIManager.Instance.OnFreeDataTutorial();
        }
    }

    public void OnCrateBomb(bool active)
    {
        foreach (var box in m_boxAmmoList)
        {
            box.CrateBomb = active;
        }
    }

    public bool IsActiveCrateBomb()
    {
        foreach (var box in m_boxAmmoList)
        {
            if (!box.CrateBomb)
                return false;
        }
        return true;
    }

    public UIInGame OnGetUIIngame()
    {
        var uiIngame = (UIInGame)UIManager.Instance.GetUIScreen(ScreenType.Gameplay);
        return uiIngame;
    }

    #region PeopleFlow Prototype Bridge
    private void OnDestroy()
    {
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

