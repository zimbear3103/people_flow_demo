using PeopleFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIInGame : UIScreen
{
    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI m_levelText;
    [SerializeField] private TextMeshProUGUI m_coinsText;
    [SerializeField] private GameObject m_coinsImg;
    [SerializeField] private Button m_settingButton;
    [SerializeField] private Button m_coinButton;
    [SerializeField] private TextMeshProUGUI m_timer;
    [SerializeField] private Slider m_capacitySlider;

    private int m_lastShownSecs = -1;

    private void OnEnable()
    {
        m_settingButton.onClick.AddListener(OnSettingButtonPressed);
        m_coinButton.onClick.AddListener(OnCointButtonPressed);
        UserProfile.Instance.OnScoreChanged = OnSetCoins;
        UserProfile.Instance.OnLevelChanged = OnSetLevel;
        m_lastShownSecs = -1;
    }

    private void OnDisable()
    {
        m_settingButton.onClick.RemoveListener(OnSettingButtonPressed);
        m_coinButton.onClick.RemoveListener(OnCointButtonPressed);
    }

    public override void Show()
    {
        base.Show();       
        OnSetLevel(UserProfile.Instance.Level);
        OnSetCoins(UserProfile.Instance.Coin);
        //SetBackgroundByLevel(UserProfile.Instance.Level + 1);
    }

    public void OnSetLevel(int level)
    {
        m_levelText.text = $"Lv{level + 1}";
    }

    public void OnSetCoins(int coins)
    {
        m_coinsText.text = coins.ToString();
    }

    public void OnSetTimer(float time)
    {
        int secs = Mathf.CeilToInt(Mathf.Max(0f, time));
        if (secs == m_lastShownSecs) return;
        m_lastShownSecs = secs;
        if (m_timer != null) m_timer.text = $"Time: {secs / 60}:{secs % 60:00}";
    }

    private void Update()
    {
        if (Panel == null || !Panel.activeInHierarchy) return;

        if (Timer.Instance != null) OnSetTimer(Timer.Instance.Remaining);

        if (m_capacitySlider != null && LevelManager.Instance != null)
        {
            var track = LevelManager.Instance.ActiveTrack;
            if (track != null) m_capacitySlider.value = track.Fill;
        }
    }
    private void OnSettingButtonPressed()
    {
        GamePlayController.Instance.EnterSettingPopupStatus();
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
    }

    private void OnCointButtonPressed()
    {
        
    }

    public GameObject OnGetCoinObj()
    {        
        return m_coinsImg;
    }
}
