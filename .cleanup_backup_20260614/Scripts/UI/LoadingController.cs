using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct BackgroundLoading
{
    public Sprite Background;
    public Sprite LoadingHandle;
}

public class LoadingController : Singleton<LoadingController>
{

    [SerializeField] Slider m_progressSlider;
    [SerializeField] GameObject m_loadingScreen;
    [SerializeField] Image m_backgroundLoading;
    [SerializeField] Image m_dragonLoading;


    [SerializeField] GameObject m_splashScreen;
    [SerializeField] float m_displayTime = 0.1f;

    [Header("Img Loading")]
    [SerializeField] BackgroundLoading[] m_backgroundLoadings;
    [SerializeField] float m_timeChangeBackground;

    private Action LoadingDoneCallBack;

    private int m_lastIDBg = 0;
    private float m_tempTimeChange = 0;

    public bool IsInitialized { set; get; } = false;

    private void Start()
    {
        m_splashScreen.SetActive(false);
        m_loadingScreen.SetActive(true);
        IsInitialized = true;
    }

    private void Update()
    {
        if (m_loadingScreen.activeSelf)
        {
            m_tempTimeChange -= Time.deltaTime;
            if(m_tempTimeChange <= 0 && m_backgroundLoadings.Length > 1)
            {
                m_tempTimeChange = m_timeChangeBackground;
                OnRandomBackground();
            }

            if(AssetManager.Instance != null)
            {
                m_progressSlider.value = AssetManager.Instance.PercentComplete;
            }    
        }
        else
        {
            m_tempTimeChange = m_timeChangeBackground;
        }
    }

    public IEnumerator StopSplashScreen()
    {
        yield return new WaitForSeconds(m_displayTime);
        m_splashScreen.SetActive(false);
        MainStateManager.Instance.SetStatus(MainStateManager.MainStatusType.Exit, MainStateManager.MainStateType.Loading);
    }

    public void ShowLoadingScreen(Action callback = null)
    {
        m_loadingScreen.SetActive(true);
        LoadingDoneCallBack = callback;
        StartCoroutine(OnWaitForLoading());            
    }

    IEnumerator OnWaitForLoading()
    {
        yield return new WaitUntil(() =>
            AssetManager.Instance != null &&
            (AssetManager.Instance.AssetStatus == AssetManager.EAssetStatus.Completed ||
             AssetManager.Instance.AssetStatus == AssetManager.EAssetStatus.Error));

        StartCoroutine(SceneController.Instance.LoadMainMenu((progress) =>
        {                
        }, () =>
        {
            m_progressSlider.value = 1;
            LoadingDoneCallBack?.Invoke();
        }));

    }    

    public void SetBackgroundLoading(int idBg)
    {
        m_backgroundLoading.sprite = m_backgroundLoadings[idBg].Background;
        m_dragonLoading.sprite = m_backgroundLoadings[idBg].LoadingHandle;
    }

    private void OnRandomBackground()
    {
        int newID;
        do
        {
            newID = UnityEngine.Random.Range(0, m_backgroundLoadings.Length);
        }
        while (newID == m_lastIDBg);

        m_lastIDBg = newID;
        SetBackgroundLoading(newID);
    }
}

