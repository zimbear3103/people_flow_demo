using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : PersistenceSingleton<SceneController>
{
    private AsyncOperation m_asyncOp { get; set; }
    public AsyncOperation AsyncOp => m_asyncOp;
    private float m_sceneActivationDelayTime { set; get; } = 0.1f;
    private float m_sceneLoadingDelayTime { set; get; } = 0.1f;

    public IEnumerator LoadMainMenu(Action<float> updateCallback = null, Action callback = null)
    {
        m_asyncOp = SceneManager.LoadSceneAsync(1);
        m_asyncOp.allowSceneActivation = false;

        while (!m_asyncOp.isDone)
        {
            float progress = Mathf.Clamp(m_asyncOp.progress, 0.0f, 0.9f);
            updateCallback?.Invoke(progress);
            yield return new WaitForSeconds(m_sceneLoadingDelayTime);

            if (progress >= 0.9f)
            {
                yield return new WaitForSeconds(m_sceneActivationDelayTime);
                callback?.Invoke();
                m_asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }
        m_asyncOp = null;
    }
}
