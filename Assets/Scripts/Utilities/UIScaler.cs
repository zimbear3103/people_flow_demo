using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIScaler : MonoBehaviour
{
    [SerializeField] CanvasScaler m_canvasScaler;
    public static int ScreenWidth { set; get; }
    public static int ScreenHeight { set; get; }
    public static bool IsScreenSquare { set; get; }
    //public CanvasLayer Layer;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => (Screen.height > 100 && Screen.width != Screen.height));
        AdjustScaler();         
    }

    public void AdjustScaler()
    {
        float ratio = (float)Screen.width/ (float)Screen.height;
        ScreenWidth = Screen.width;
        ScreenHeight = Screen.height;

        if(ratio < 0.37)
        {
            IsScreenSquare = false;
            m_canvasScaler.matchWidthOrHeight = 0;
        }
        else if (ratio < 0.56f)
        {
            IsScreenSquare = false;
            m_canvasScaler.matchWidthOrHeight = ratio / 2;
        }
        else
        {
            IsScreenSquare = true;
            m_canvasScaler.matchWidthOrHeight = 1;
        }

        Debug.Log($"[UIScaler] Adjust Scaler ratio = {ratio}  width = {Screen.width}  height = {Screen.height}  MatchWidthOrHeight = {m_canvasScaler.matchWidthOrHeight}");
    }
}
