using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIInGame : UIScreen
{
    [Header("Background")]
    [SerializeField] private Image m_topBakground;
    [SerializeField] private Image m_bottomBackground;

    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI m_levelText;
    [SerializeField] private TextMeshProUGUI m_coinsText;
    [SerializeField] private GameObject m_coinsImg;
    [SerializeField] private Button m_settingButton;
    [SerializeField] private Button m_coinButton;


    private void OnEnable()
    {
        m_settingButton.onClick.AddListener(OnSettingButtonPressed);
        m_coinButton.onClick.AddListener(OnCointButtonPressed);
        UserProfile.Instance.OnScoreChanged = OnSetCoins;
        UserProfile.Instance.OnLevelChanged = OnSetLevel;
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

    public void OnSetTopBackgroundColor(Sprite topBackground)
    {
        m_topBakground.sprite = topBackground;
    }

    public void OnSetBottomBackgroundColor(Sprite bottomBackground)
    {
        m_bottomBackground.sprite = bottomBackground;
    }

    public void OnSetColorBottomBackground(Color color)
    {
        m_bottomBackground.color = color;
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
