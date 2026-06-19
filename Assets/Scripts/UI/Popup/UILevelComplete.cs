using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILevelComplete : UIPopup
{
    [SerializeField] private Button m_leftButton;
    [SerializeField] private TextMeshProUGUI m_coinLeftText;
    [SerializeField] private Button m_rightButton;
    [SerializeField] private TextMeshProUGUI m_coinRightText;

    private void OnEnable()
    {
        m_leftButton.onClick.AddListener(OnLeftButtonPressed);
        m_rightButton.onClick.AddListener(OnRightButtonPressed);
    }

    public void OnDisable()
    {
        m_leftButton.onClick.RemoveListener(OnLeftButtonPressed);
        m_rightButton.onClick.RemoveListener(OnRightButtonPressed);
    }

    public override void Show()
    {
        base.Show();
        SoundManager.Instance.OnPlayMusic(ESoundId.Bg_Victory, isLoop: false, volume: 1f);
    }

    private void OnLeftButtonPressed()
    { 
        UserProfile.Instance.SaveGameData();
        OnPlayCoinAnimation(true);
        GamePlayController.Instance.QuitLevel();
        Hide();
    }

    private void OnRightButtonPressed()
    {
        OnPlayCoinAnimation(false);

        if (GamePlayController.Instance.HasNextLevel)
        {
            UserProfile.Instance.Level += 1;
            UserProfile.Instance.SaveGameData();
            GamePlayController.Instance.StartLevel();
        }
        else
        {
            UserProfile.Instance.SaveGameData();
            GamePlayController.Instance.RestartLevel();
        }

        Hide();
    }

    public void SetCoinLeftText(int coin)
    {
        m_coinLeftText.text = $"+{coin}";
    }

    public void SetCoinRightText(int coin)
    {
        m_coinRightText.text = $"+{coin}";
    }

    private void OnPlayCoinAnimation(bool isLeftButton)
    {
        var uiIngame = GamePlayController.Instance.OnGetUIIngame();

        if (uiIngame != null)
        {
            int coinAfterAdd = UserProfile.Instance.Coin;
            var ingameCoinBar = uiIngame.OnGetCoinObj();

            // NOTE: the coin-fly tween was dropped along with the legacy 'Coin' component
            // (removed in the capybara/boss cleanup). We still sync the HUD coin total so
            // the player's balance updates on the home screen.
            if (ingameCoinBar != null)
                StartCoroutine(AnimationSetCoin(coinAfterAdd));
        }
    }

    private IEnumerator AnimationSetCoin(int coin)
    {
        yield return new WaitForSeconds(0.1f);
        var uiHome = (UIHome)UIManager.Instance.GetUIScreen(ScreenType.Home);
        uiHome.OnSetCoins(coin);
    }
}
