using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHome : UIScreen
{
    [SerializeField] private Button m_playButton;
    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI m_levelText;
    [SerializeField] private TextMeshProUGUI m_coinsText;
    [SerializeField] private Button m_settingButton;
    [SerializeField] private Button m_coinButton;

    [SerializeField] private LevelNode[] m_levelNodes;

    [Header("Background")]
    [SerializeField] private Image m_mainBackground;
    [SerializeField] Sprite[] m_listBackgrounds;

    [Header("Capybara")]
    [SerializeField] private GameObject m_capybara;
    [SerializeField] private float m_speedCapybara = 1f;
    [SerializeField] private ScrollRect m_scrollLevel;

    [Header("Bottom Buttons")]
    [SerializeField] private Toggle m_shopButton;
    [SerializeField] private Toggle m_homeButton;
    [SerializeField] private Toggle m_leaderboardButton;

    private int m_currentLevel;
    private int m_countEffectLevel = 0;
    private int m_maxLevel = 10;

    private void OnEnable()
    {
        m_playButton.onClick.AddListener(OnPlayButtonPressed);
        m_settingButton.onClick.AddListener(OnSettingButtonPressed);
        m_coinButton.onClick.AddListener(OnCoinButtonPressed);
        m_shopButton.onValueChanged.AddListener(OnShopTogglePressed);
        m_homeButton.onValueChanged.AddListener(OnHomeTogglePressed);
        m_leaderboardButton.onValueChanged.AddListener(OnLeaderboardTogglePressed);
    }

    private void OnDisable()
    {
        m_playButton.onClick.RemoveListener(OnPlayButtonPressed);
        m_settingButton.onClick.RemoveListener(OnSettingButtonPressed);
        m_coinButton.onClick.RemoveListener(OnCoinButtonPressed);
        m_shopButton.onValueChanged.RemoveListener(OnShopTogglePressed);
        m_homeButton.onValueChanged.RemoveListener(OnHomeTogglePressed);
        m_leaderboardButton.onValueChanged.RemoveListener(OnLeaderboardTogglePressed);
    }

    private void Start()
    {
        m_currentLevel = UserProfile.Instance.Level + 1;
    }

    public override void Show()
    {
        base.Show();
        m_maxLevel = PeopleFlow.PeopleFlowSampleLevels.Count;
        SetLevelUI();
        OnSetLevel(UserProfile.Instance.Level);
        OnSetCoins(UserProfile.Instance.Coin);
        //SetBackgroundByLevel(UserProfile.Instance.Level + 1);
        SoundManager.Instance.OnPlayMusic(ESoundId.Bg_MainMenu, isLoop: true, 1f);
        m_currentLevel = UserProfile.Instance.Level + 1;
    }

    private void OnPlayButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonMain);
        MainStateManager.Instance.SetStatus(MainStateManager.MainStatusType.Exit, MainStateManager.MainStateType.Gameplay);       
    }
    public void OnSetLevel(int level)
    {
        m_levelText.text = $"Lv{level + 1}";
    }

    public void OnSetCoins(int coins)
    {
        m_coinsText.text = coins.ToString();
    }

    private void OnSettingButtonPressed()
    {
        UIManager.Instance.ShowPopup(PopupType.Setting);
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonMain);
    }

    private void OnCoinButtonPressed()
    {
        Debug.Log("Coin button pressed");
    }

    private void OnShopTogglePressed(bool isOn)
    {    
        if(isOn)
        {
          
        }
        else
        {
            
        }
    }

    private void OnHomeTogglePressed(bool isOn)
    {
        if (isOn)
        {

        }
        else
        {

        }
    }

    private void OnLeaderboardTogglePressed(bool isOn)
    {
        if (isOn)
        {

        }
        else
        {

        }
    }

    private void SetLevelUI()
    {
        if (m_currentLevel < (UserProfile.Instance.Level + 1))
        {
            if((UserProfile.Instance.Level + 1) <= m_maxLevel)
            {
                EffectUpdateLevel();
            }   
        }
        else
        {
            NonEffectUpdateLevel();
        }
    }

    public void OnSetMainBackground(Sprite background)
    {
        m_mainBackground.sprite = background;
    }

    private void EffectUpdateLevel()
    {
        if(m_currentLevel < (UserProfile.Instance.Level + 1))
        {
            for(int i = 0; i < m_levelNodes.Length; i++)
            {
                if((m_levelNodes[i].Level <= (UserProfile.Instance.Level + 1)) && (m_levelNodes[i].Level > m_currentLevel))
                {
                    m_levelNodes[i].OnEffectLevel();
                    m_currentLevel = UserProfile.Instance.Level + 1;
                    StartCoroutine(ScrollNextLevel(1f));
                    m_countEffectLevel++;
                    break;
                }
            }
        }
    }

    private void NonEffectUpdateLevel()
    {
        m_countEffectLevel = 0;
        ResetStateLevelUI();
        for (int i = 0; i < m_levelNodes.Length; i++)
        {
            int levelNode = i + UserProfile.Instance.Level + 1;
            if (levelNode > m_maxLevel)
            {
                m_levelNodes[i].SetNoneLevel(levelNode);
                m_levelNodes[i].gameObject.SetActive(false);
            }
            else
            {
                m_levelNodes[i].SetLevel(levelNode);
                m_levelNodes[i].gameObject.SetActive(true);
            }

            if (m_levelNodes[i].Level == (UserProfile.Instance.Level + 1))
            {
                m_levelNodes[i].OnActiveLevel();
                m_capybara.transform.position = m_levelNodes[i].PosCapy.position;
            }
        }
    }    

    private void ResetStateLevelUI()
    {
        for (int i = 0; i < m_levelNodes.Length; i++)
        {
            m_levelNodes[i].ResetStateLevel();
        }
    }

    private IEnumerator ScrollNextLevel(float duration = 1f)
    {
        m_scrollLevel.enabled = false;
        float startPos = m_scrollLevel.verticalNormalizedPosition;
        float targetPos = 0.44f;
        float time = 0f;
        Vector3 capyWorldPos = m_capybara.transform.position;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            m_scrollLevel.verticalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
            m_capybara.transform.position = capyWorldPos;
            yield return null;
        }

        m_scrollLevel.verticalNormalizedPosition = 0f;
        m_scrollLevel.enabled = true;
        NonEffectUpdateLevel();
    }
}
