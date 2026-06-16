using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ETutorialStatus
{
    None = 0,
    Pause,
    Play,
    EndTutorial
}
public class UITutorial : UIPopup
{
    [SerializeField] List<GameObject> m_stepsObj;
    [SerializeField] List<GameObject> m_listTutorialLevel;

    private bool m_isStartTutorial;
    private bool m_OnUsedItem;
    private int m_currentStep = 1;
    private ETutorialStatus m_tutorialStatus = ETutorialStatus.None;
    public ETutorialStatus TutorialStatus => m_tutorialStatus;
    public override void Show()
    {
        base.Show();
    }

    private void Update()
    {
        if (GamePlayController.Instance.IsTutorial)
        {
            switch (m_tutorialStatus)
            {
                case ETutorialStatus.None:
                    break;
                case ETutorialStatus.Pause:
                    GamePlayController.Instance.IsGamePlaying = false;
                    break;
                case ETutorialStatus.Play:
                    GamePlayController.Instance.IsGamePlaying = true;
                    break;
                case ETutorialStatus.EndTutorial:
                    OnResetTutorial();
                    break;
            }
        }
    }

    private void Start()
    {
        UserProfile.Instance.Level = 0;
        UserProfile.Instance.SaveGameData();
    }
    public void OnSetupTutorialData(int tutorialLevel)
    {
        if (!m_isStartTutorial)
        {
            m_listTutorialLevel[tutorialLevel].SetActive(true);
            m_isStartTutorial = true;
        }

        m_stepsObj.Clear();
        foreach (Transform step in m_listTutorialLevel[tutorialLevel].transform)
        {
            m_stepsObj.Add(step.gameObject);
            step.gameObject.SetActive(false);
        }

        m_currentStep = 0;
        m_stepsObj[m_currentStep].SetActive(true);
        OnPauseTutorial();
    }
    public void OnResetTutorial()
    {
        m_isStartTutorial = false;
        GamePlayController.Instance.IsGamePlaying = true;
        m_tutorialStatus = ETutorialStatus.None;
        m_stepsObj[m_currentStep].SetActive(false);
    }

    public void OnNextStep()
    {
        //if (m_currentStep < 0 && m_currentStep > m_stepsObj.Count) return;        

        m_stepsObj[m_currentStep].SetActive(false);
        Debug.Log(m_currentStep + " previous ===============");
        m_currentStep++;
        Debug.Log(m_currentStep + " after ============");

        m_stepsObj[m_currentStep].SetActive(true);
    }
    public void OnNextStepTimeDelay(float timeDelay)
    {
        if (m_currentStep < 0 && m_currentStep > m_stepsObj.Count) return;

        m_stepsObj[m_currentStep].SetActive(false);

        m_currentStep++;

        StartCoroutine(OnDelayAction(timeDelay, () => m_stepsObj[m_currentStep].SetActive(true)));
    }
    public void OnClickBoxAmmoTutorial(int boxAmmo)
    {
        if (m_OnUsedItem)
        {
            m_OnUsedItem = false;
        }
    }
    private IEnumerator OnDelayAction(float time, Action action)
    {
        yield return new WaitForSeconds(time);
        action.Invoke();
    }
    public void OnUsedCrateBombTutorial()
    {
        m_OnUsedItem = true;
    }

    public void OnIntroCrateBombTutorial()
    {
    }

    public void OnPauseTutorial()
    {
        m_tutorialStatus = ETutorialStatus.Pause;
    }

    public void OnPauseTutorialTime(float time)
    {
        var saveStatus = m_tutorialStatus;
        m_tutorialStatus = ETutorialStatus.Pause;
        StartCoroutine(OnDelayAction(time, () => m_tutorialStatus = saveStatus));
    }
    public void OnResumeTutorial()
    {
        m_tutorialStatus = ETutorialStatus.Play;
    }

    public void OnResumeTutorialTime(float time)
    {
        var saveStatus = m_tutorialStatus;
        m_tutorialStatus = ETutorialStatus.Play;
        StartCoroutine(OnDelayAction(time, () => m_tutorialStatus = saveStatus));
    }
    public void OnEndTutorial()
    {
        m_tutorialStatus = ETutorialStatus.EndTutorial;
    }

}
