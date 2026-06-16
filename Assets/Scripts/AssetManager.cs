using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetManager : PersistenceSingleton<AssetManager>
{ 
    public enum EAssetStatus
    {
        None = 0,
        Completed,
        Error
    }

    [SerializeField] bool m_showDebugInfo = false;

    public EAssetStatus AssetStatus { set; get; }  = EAssetStatus.None;
    public float PercentComplete { set; get; } = 0;
    string m_debugStr = "";

    private void Start()
    { 
        StartCoroutine(OnDownloadAndShow());
    }

    IEnumerator OnDownloadAndShow()
    {
        Addressables.ClearDependencyCacheAsync("dlc1");       
        AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync("dlc1");

        // Theo dõi quá trình tải
        while (!downloadHandle.IsDone)
        {
            float progress = downloadHandle.PercentComplete;
            PercentComplete = progress;
            Debug.Log("Đang tải: " + (progress * 100f).ToString("F0") + "%");

            if (m_showDebugInfo)
                m_debugStr = "Đang tải: " + (progress * 100f).ToString("F0") + "%";

            yield return null;
        }

        if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log("Tải xong dependencies!");

            if (m_showDebugInfo)
                m_debugStr += " Tải ok";

            OnCompletedLoading(EAssetStatus.Completed);
        }
        else
        {
            Debug.LogError("Download thất bại: " + downloadHandle.OperationException);

            if (m_showDebugInfo)
                m_debugStr += "Download thất bại: " + downloadHandle.OperationException;

            OnCompletedLoading(EAssetStatus.Error);
        }

        Addressables.Release(downloadHandle);       
    }

    void OnCompletedLoading(EAssetStatus assetStatus)
    {
        AssetStatus = assetStatus;       
    }    

    private void OnGUI()
    {
        if (m_showDebugInfo)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 50,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red },
            };

            GUI.Label(new Rect(10, 100, Screen.width, Screen.height), "" + m_debugStr, style);
        }
    }
}
