using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ItemTypes
{
    Unlock, 
    Crate,
    LongRange, 
    Sort,
    GunOverload
}

public abstract class ItemButton : MonoBehaviour
{
    [SerializeField] ItemTypes itemType;
    [SerializeField] GameObject quantityObj;
    [SerializeField] TextMeshProUGUI quantityText;
    [SerializeField] GameObject adsObj;
    [SerializeField] Sprite m_itemImg;

    public int quantity { get; set; } = 0;
    public bool isAdsAvailable { get; set; } = true;
    public bool enableButton { get; set; } = true;
    public int itemPrice { get; set; } = 300;

    private float bossSpeedDebuff = 70f;
    private Button m_button;

    //Setup Popup Buy Item
    private string m_nameItemPopup;
    private string m_featureItem;

    private string m_nameItemTracking;
    public int countReceiveItem { get; set; } = 0;
    public bool isBuyWithCoin { get; set; } = false;

    public void Awake()
    {
        m_button = GetComponentInChildren<Button>();
    }

    public virtual void OnEnable()
    {
        m_button.onClick.AddListener(OnButtonPressed);
    }

    public void OnDisable()
    {
        m_button.onClick.RemoveListener(OnButtonPressed);
    }

    public virtual void OnSetItemCountText(int count)
    {
        quantityText.text = count.ToString();
    }

    public void OnSetEnableButton(bool isActive)
    {
        m_button.interactable = isActive;
    }

    public virtual void OnButtonPressed()
    {
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        if (quantity <= 0)
        {
            OnShowBuyItemPopup();
        }
        else if (quantity > 0)
        {
            OnUseItem();
        }

        OnUpdateUI();
    }

    private void OnUseItem()
    {
        //Tracking
        OnButtonLogic();
        quantity--;
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.Ingame_GeneralBooster);
        OnUpdateUI();
    }

    public void OnCloseButtonPressed()
    {
    }

    public virtual void OnPlayAds()
    {
        GameLog.Log(LogType.Log, "[Item] Play Ads");
        SoundManager.Instance.OnPlaySfxAudio(ESoundId.UI_Click_Other);
        //While play Ads complete, +1 Item
        //this.quantity++;
        //this.countReceiveItem++;
        //TrackingSourceItem(FunctionName.VideoAd);
        //OnSetItemCountText(this.quantity);
        //SaveData();
        //UIManager.Instance.HideAllUIPopup();
        //OnUseItem();
    }

    public virtual void OnShowBuyItemPopup()
    {
        OnPauseGame();
        UIManager.Instance.ShowBuyItemPopup(m_nameItemPopup, m_featureItem, itemPrice, m_itemImg, isBuyWithCoin,
            btnUseCoin: () => UseIconBuy(),
            btnWatchAds: () => OnPlayAds(),
            btnClose: () => OnCloseButtonPressed());
    }

    public void ReduceSpeedBoss()
    {
        if (GamePlayController.Instance.GetBoss() != null)
        {
            GamePlayController.Instance.GetBoss().ReduceSpeedBoss(bossSpeedDebuff);
        }
    }

    public abstract void OnButtonLogic();

    public virtual void OnUpdateUI()
    {
        if (quantity <= 0)
        {
            quantity = 0;
            OnSetItemCountText(quantity);
            //if (isAdsAvailable)
            //{
            //    adsObj.SetActive(true);
            //    quantityObj.SetActive(false);
            //}
            //else
            //{
            //    adsObj.SetActive(false);
            //    quantityObj.SetActive(true);
            //    OnSetEnableButton(false);
            //    OnSetItemCountText(quantity);
            //    enableButton = false;
            //}
            //OnResumeGame();
        }
        else
        {
            adsObj.SetActive(false);
            quantityObj.SetActive(true);
            OnSetEnableButton(true);
            OnSetItemCountText(quantity);
        }

        SaveData();
    }

    public void SetInfo(string namItemPopup, string nameItemTracking, string feature)
    {
        m_nameItemPopup = namItemPopup;
        m_featureItem = feature;
        m_nameItemTracking = nameItemTracking;
    }

    private void SaveData()
    {
        UserProfile.Instance.SaveItemGame(this.itemType, this.quantity);
    }

    private void UseIconBuy()
    {
        if (UserProfile.Instance.Coin >= itemPrice)
        {

            UserProfile.Instance.Coin -= itemPrice;
            UserProfile.Instance.SaveGameData();
            this.quantity++;
            this.countReceiveItem++;


            OnSetItemCountText(this.quantity);
            SaveData();
            UIManager.Instance.HideAllUIPopup();

            OnResumeGame();

            OnUseItem();
        }
    }

    private void OnPauseGame()
    {
        if (GamePlayController.Instance != null)
        {
            GamePlayController.Instance.OnPause();
        }
    }

    private void OnResumeGame()
    {
        if (GamePlayController.Instance != null)
        {
            GamePlayController.Instance.OnResume();
        }
    }
}
