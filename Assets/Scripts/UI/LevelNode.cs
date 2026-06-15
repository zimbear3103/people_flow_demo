using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelNode : MonoBehaviour
{
    [SerializeField] Sprite m_levelLock;
    [SerializeField] Sprite m_levelUnlock;

    [SerializeField] Image m_levelNode;
    [SerializeField] TextMeshProUGUI m_levelText;

    [SerializeField] Image m_levelUnlockBg;
    [SerializeField] float m_timeEffectUnlock = 1f;
    [SerializeField] float m_effectDuration = 2f;

    private int m_level; 

    public int Level => m_level;
    public Transform PosCapy => m_levelNode.transform;

    public void SetLevel(int level)
    {
        m_level = level;
        m_levelText.text = level.ToString();
    }

    public void SetNoneLevel(int level)
    {
        m_level = level;
        m_levelText.text = "";
    }

    public void OnActiveLevel()
    {
        m_levelNode.sprite = m_levelUnlock;
        m_levelUnlockBg.gameObject.SetActive(true);
    }

    public void OnEffectLevel()
    {
        StartCoroutine(AnimateLineFill());     
    }

    public void ResetStateLevel()
    {
        m_levelNode.sprite = m_levelLock;
        m_levelUnlockBg.gameObject.SetActive(false);
    }

    private IEnumerator AnimateLineFill()
    {
        float timer = 0f;
        m_levelUnlockBg.gameObject.SetActive(true);

        while (timer < m_effectDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / m_effectDuration;
            m_levelUnlockBg.transform.localScale = new Vector3(1f, progress, 1f);
            yield return null;
        }

        m_levelUnlockBg.transform.localScale = Vector3.one;
        m_levelNode.sprite = m_levelUnlock;
    }
}
