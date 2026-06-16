using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ResetScrollMenu : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private ScrollRect m_scrollRect;
    [SerializeField] private float m_returnSpeed = 5f;

    [Range(0f, 1f)]
    [SerializeField] private float m_limitscrollTop = 0.95f;

    [Range(0f, 1f)]
    [SerializeField] private float m_limitscrollBottom = 0f;

    private bool m_isDragging = false;
    private bool m_shouldReturn = false;

    public void OnBeginDrag(PointerEventData eventData)
    {
        m_isDragging = true;
        m_shouldReturn = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        m_isDragging = false;
        m_shouldReturn = true;
    }

    void Update()
    {
        if (m_shouldReturn && !m_isDragging)
        {
            m_scrollRect.verticalNormalizedPosition = Mathf.Lerp(
                m_scrollRect.verticalNormalizedPosition,
                0f,
                Time.deltaTime * m_returnSpeed
            );

            if (m_scrollRect.verticalNormalizedPosition < 0.001f)
            {
                m_scrollRect.verticalNormalizedPosition = 0f;
                m_shouldReturn = false;
            }
        }

        LimitScrollPosition();
    }

    void LimitScrollPosition()
    {
        if (m_scrollRect.verticalNormalizedPosition < m_limitscrollBottom)
        {
            m_scrollRect.verticalNormalizedPosition = m_limitscrollBottom;
        }

        if (m_scrollRect.verticalNormalizedPosition > m_limitscrollTop)
        {
            m_scrollRect.verticalNormalizedPosition = m_limitscrollTop;
        }
    }
}