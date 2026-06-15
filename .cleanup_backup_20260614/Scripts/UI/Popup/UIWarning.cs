using System.Collections;
using TMPro;
using UnityEngine;

public class UIWarning : UIPopup
{
    [SerializeField] private TextMeshProUGUI m_warningText;

    [SerializeField] private Animator m_animator;

    private bool m_isShow = false;
    private float m_timeHide = 3f;

    public void SetWarningText(string warningText)
    {
        m_warningText.text = warningText;
    }

    public override void Show()
    {
        base.Show();
        m_animator.Play("Show");
        m_isShow = true;
    }

    public override void Hide()
    {    
        base.Hide();
        m_isShow = false;
    }

    public void ShowWarning(string warningText = "", float timeHide = 3f)
    {
        if(!string.IsNullOrEmpty(warningText))
        {
            SetWarningText(warningText);
        }
        Show();
        m_timeHide = timeHide;
        //StartCoroutine(HidePopup(timeHide));
    }

    private void Update()
    {
        if(m_isShow)
        {
            m_timeHide -= Time.deltaTime;
            if(m_timeHide <= 0f)
            {
                m_isShow = false;
                StartCoroutine(HidePopup());
            }
        }
    }

    private IEnumerator HidePopup()
    {
        m_animator.Play("Hide");
        yield return new WaitForSeconds(0.35f);
        Hide();
    }
}
