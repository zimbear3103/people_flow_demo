using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class AssetManager : PersistenceSingleton<AssetManager>
{ 
    public enum EAssetStatus
    {
        None = 0,
        Completed,
        Error
    }

    [ReadOnly]
    [SerializeField] GameLevels m_gameLevels;
    [SerializeField] bool m_showDebugInfo = false;    

    public GameLevels GameLevels => m_gameLevels;
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

            AsyncOperationHandle<GameLevels> loadHandle = Addressables.LoadAssetAsync<GameLevels>("GameLevels");
            yield return loadHandle;

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {                
                m_gameLevels = loadHandle.Result;
                Debug.Log("[AssetManager] Load game level data: " + m_gameLevels.name);
                Debug.Log("[AssetManager] m_gameLevels.Datas: " + m_gameLevels.Datas);
                Debug.Log("[AssetManager] m_gameLevels.Datas.Count: " + m_gameLevels.Datas.Count);

                if (m_showDebugInfo)
                    m_debugStr += " gamelevels";

                OnCompletedLoading(EAssetStatus.Completed);
            }
            else
            {
                Debug.LogError("[AssetManager] Load game level data error!");

                if (m_showDebugInfo)
                    m_debugStr += " errors";

                OnCompletedLoading(EAssetStatus.Error);
            }
            
            //Addressables.Release(loadHandle);
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
