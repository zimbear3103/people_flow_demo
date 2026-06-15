using System.Collections;
using TMPro;
using UnityEngine;

public class LongRangeButton : ItemButton
{
    [SerializeField] private float m_timeEffect;
    [SerializeField] private TextMeshProUGUI m_nameButtonText;

    private bool m_activeEffect = false;
    private float m_timeTemp;
    private bool m_isDefaut = true;
    private string m_nameItem; 

    public override void OnEnable()
    {
        base.OnEnable();
        StartCoroutine(DelayInit());
        m_timeTemp = m_timeEffect;
    }

    private void Start()
    {
        m_timeTemp = m_timeEffect;
        m_nameItem = "Long Range";
        SetInfo(m_nameItem,"Range", "The cannon fired further for seconds");
    }

    private void Update()
    {
        if (m_activeEffect && GamePlayController.Instance.GetCanonHolder())
        {
            m_isDefaut = false;
            if (GamePlayController.Instance.IsGamePlaying)
            {
                m_timeEffect -= Time.deltaTime;
                UpdateUITime(m_timeEffect);
            }
            GamePlayController.Instance.GetCanonHolder().longRange = true;
            OnSetEnableButton(false);

            if (m_timeEffect <= 0)
            {
                m_activeEffect = false;
                GamePlayController.Instance.GetCanonHolder().longRange = false;
            }
        }
        else
        {
            if (!m_isDefaut)
            {
                m_isDefaut = true;
                m_nameButtonText.text = m_nameItem;
                m_timeEffect = m_timeTemp;
                OnSetEnableButton(true);
                if (GamePlayController.Instance.GetBoss() != null)
                {
                    GamePlayController.Instance.GetBoss().ResetMoveSpeed();
                }
            }
        }
    }

    public override void OnButtonLogic()
    {
        m_activeEffect = true;
        ReduceSpeedBoss();
        SetStateButton();
    }

    private void SetStateButton()
    {
        if (GamePlayController.Instance.GetCanonHolder() != null)
        {
            if(m_activeEffect)
            {
                OnSetEnableButton(false);
            }
            else
            {
                OnSetEnableButton(true);
            }
        }
    }

    public  IEnumerator DelayInit()
    {        
        yield return new WaitUntil(() => (GamePlayController.Instance != null && GamePlayController.Instance.GetCanonHolder()));
        quantity = UserProfile.Instance.LongRangeItem;

        OnUpdateUI();
        SetStateButton();
        enableButton = true;
        m_activeEffect = false;
        GamePlayController.Instance.GetCanonHolder().longRange = false;
        m_nameButtonText.text = m_nameItem;
    }

    private void UpdateUITime(float time)
    {
        int timeTemp = Mathf.RoundToInt(time);
        m_nameButtonText.text = $"{timeTemp}s";
    }    

}
