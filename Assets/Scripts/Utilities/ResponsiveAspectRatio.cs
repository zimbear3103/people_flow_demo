using UnityEngine;
using UnityEngine.UI;

public class ResponsiveAspectRatio : MonoBehaviour
{
    [ReadOnly]
    public float defaultAspect = 1179f / 2556f;

    private AspectRatioFitter m_fitter;
    private RectTransform m_rect;

    public bool stretchWidthOnPhone = false;
    void Start()
    {
        m_fitter = GetComponent<AspectRatioFitter>();
        m_rect = GetComponent<RectTransform>();

        float screenAspect = (float)Screen.width / Screen.height;

        if (screenAspect < 0.57)
        {
            if (stretchWidthOnPhone)
            {
                m_rect.anchorMin = new Vector2(0f, m_rect.anchorMin.y);
                m_rect.anchorMax = new Vector2(1f, m_rect.anchorMax.y);
                
                //m_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, m_rect.rect.height);
                m_rect.sizeDelta = new Vector2(0f, m_rect.rect.height);
            }
            else
            {
                m_fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                m_fitter.aspectRatio = screenAspect;
            }
        }
        else
        {
            if (!stretchWidthOnPhone)
            {
                m_fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                m_fitter.aspectRatio = defaultAspect;

                m_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2556);
                m_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1179);
            }
        }
    }
}
