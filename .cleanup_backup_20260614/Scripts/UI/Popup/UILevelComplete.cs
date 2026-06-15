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
    [SerializeField] private GameObject m_coin;

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
        SoundManager.Instance.OnPlayVoiceAudio(ESoundId.Voice_CapyWin);
    }
    // Continue Button
    private void OnLeftButtonPressed()
    { 
        UserProfile.Instance.SaveGameData();
        OnPlayCoinAnimation(true);
        GamePlayController.Instance.QuitLevel();
        Hide();
    }

    // Watch ad double coin
    private void OnRightButtonPressed()
    {
        // While watch ad complete
        // ++ Coin
        //GamePlayController.Instance.TrackingSourceCoin(FunctionName.Result);
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
            ////            
            if (ingameCoinBar != null)
            {
                if (isLeftButton)
                {
                    var coinObj = Instantiate(m_coin, m_leftButton.transform.position, Quaternion.identity, transform.parent);
                    coinObj.GetComponent<Coin>().AnimationMoveToTarget(ingameCoinBar, 2f, () => StartCoroutine(AnimationSetCoin(coinAfterAdd)));
                }
                else
                {
                    var coinObj = Instantiate(m_coin, m_rightButton.transform.position, Quaternion.identity, transform.parent);
                    coinObj.GetComponent<Coin>().AnimationMoveToTarget(ingameCoinBar, 2f, () => StartCoroutine(AnimationSetCoin(coinAfterAdd)));
                }
            }
        }
    }

    private IEnumerator AnimationSetCoin(int coin)
    {
        yield return new WaitForSeconds(0.1f);
        var uiHome = (UIHome)UIManager.Instance.GetUIScreen(ScreenType.Home);
        uiHome.OnSetCoins(coin);
    }
}
