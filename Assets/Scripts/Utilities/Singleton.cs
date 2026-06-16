using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T m_instance;

    public static T Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindAnyObjectByType(typeof(T)) as T;
            }

            return m_instance;
        }
    }

    public static bool HasInstance => m_instance != null;

    // Virtual so subclasses can extend teardown (e.g. unsubscribing events) WITHOUT
    // shadowing this and silently dropping the unregister. Overrides MUST call base.
    protected virtual void OnDestroy()
    {
        m_instance = null;
    }
}

public class PersistenceSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T m_instance;

    public static T Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindAnyObjectByType(typeof(T)) as T;
            }

            if (m_instance)
            {
                DontDestroyOnLoad(m_instance);
            }
            else
            {
                Debug.LogError("!!! ---------------------nulll");
            }

            return m_instance;
        }
    }
}