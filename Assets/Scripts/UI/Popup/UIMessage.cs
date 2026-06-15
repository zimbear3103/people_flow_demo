using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMessage : UIPopup
{
    [Header("MessageBoard")]
    [SerializeField] private GameObject m_messageBoardPopup;
    [SerializeField] private Button m_middleButton;
    [SerializeField] private TextMeshProUGUI m_textMiddleBtn;
    [SerializeField] private Button m_closeButton;
    [SerializeField] private TextMeshProUGUI m_titleText;
    [SerializeField] private TextMeshProUGUI m_contentText;

    private Action m_buttonCallback;
    private Action m_buttonCloseMessCallback;

    [Header("Buy Item In Game")]
    [SerializeField] private GameObject m_BuyItemPopup;
    [SerializeField] private TextMeshProUGUI m_titleBuyItemText;
    [SerializeField] private TextMeshProUGUI m_contentBuyItemText;
    [SerializeField] private Image m_itemImg;
    [SerializeField] private Button m_closeBuyitemPopupButton;
    [SerializeField] private Button m_useCoinButton;
    [SerializeField] private Button m_watchAdsButton;
    [SerializeField] private TextMeshProUGUI m_priceText;

    private Action m_buttonUseCoinCallBack;
    private Action m_buttonWatchAdsCallBack;
    private Action m_buttonCloseBuyItemCallback;

    public void OnEnable()
    {
        m_middleButton.onClick.AddListener(OnMiddleButtonPressed);
        m_closeButton.onClick.AddListener(OnCloseButtonPressed);
        m_useCoinButton.onClick.AddListener(OnUseCoinButtonPressed);
        m_watchAdsButton.onClick.AddListener(OnWatchAdsButtonPressed);
        m_closeBuyitemPopupButton.onClick.AddListener(OnCloseBuyItemButtonPressed);
    }
    public void OnDisable()
    {
        m_middleButton.onClick.RemoveListener(OnMiddleButtonPressed);
        m_closeButton.onClick.RemoveListener(OnCloseButtonPressed);
        m_useCoinButton.onClick.RemoveListener(OnUseCoinButtonPressed);
        m_watchAdsButton.onClick.RemoveListener(OnWatchAdsButtonPressed);
        m_closeBuyitemPopupButton.onClick.RemoveListener(OnCloseBuyItemButtonPressed);
    }

    private void OnMiddleButtonPressed()
    {
        Hide();
        m_buttonCallback?.Invoke();
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
    }

    private void OnUseCoinButtonPressed()
    {
        m_buttonUseCoinCallBack?.Invoke();
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
    }

    private void OnWatchAdsButtonPressed()
    {
        m_buttonWatchAdsCallBack?.Invoke();
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
    }

    private void OnCloseButtonPressed()
    {
        Hide();
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonNegative);
        m_buttonCloseMessCallback?.Invoke();
    }

    private void OnCloseBuyItemButtonPressed()
    {
        m_buttonCloseBuyItemCallback?.Invoke();
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_ButtonNegative);
        Hide();
        if (GamePlayController.Instance != null)
        {
            GamePlayController.Instance.OnResume();
        }       
    }

    public void SetTitleText(string title)
    {
        m_titleText.text = title;
    }

    public void SetContentText(string contentText)
    {
        m_contentText.text = contentText;
    }

    public void OnShowMessageBox(string title, string content, string textbtn, Action btnCallback, Action btnClose = null)
                                    
    {
        Panel.SetActive(true);
        m_messageBoardPopup.SetActive(true);
        m_BuyItemPopup.SetActive(false);

        m_buttonCallback = btnCallback;
        m_buttonCloseMessCallback = btnClose;

        m_titleText.text = title;
        m_contentText.text = content;
        m_textMiddleBtn.text = textbtn;

        Show();
    }

    public void OnShowBuyItemPopup(string title, string content,int price, Sprite itemImg, 
        bool canBuyCoin, Action btnUseCoin, Action btnWatchAds,Action btnClose)
    {
        Panel.SetActive(true);
        m_BuyItemPopup.SetActive(true);
        m_messageBoardPopup.SetActive(false);

        m_titleBuyItemText.text = title;
        m_contentBuyItemText.text= content;
        m_itemImg.sprite = itemImg;

        m_buttonUseCoinCallBack = btnUseCoin; 
        m_buttonWatchAdsCallBack = btnWatchAds;
        m_buttonCloseBuyItemCallback = btnClose;

        m_priceText.text = price.ToString();

        if (UserProfile.Instance.Coin >= price)
        {
            m_useCoinButton.interactable = true;
        }
        else
            m_useCoinButton.interactable = false;

        if (canBuyCoin)
            m_useCoinButton.gameObject.SetActive(true);
        else 
            m_useCoinButton.gameObject.SetActive(false);
    }    
}
