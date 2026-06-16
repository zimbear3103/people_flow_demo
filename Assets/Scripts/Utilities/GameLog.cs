using UnityEngine;

public class GameLog
{
    static public void Log(LogType type, string message)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR || USE_GAMELOG
        switch (type)
        {
            case LogType.Log:
                Log(message);
                break;
            case LogType.Warning:
                WarningLog(message);
                break;
            case LogType.Error:
                ErrorLog(message);
                break;
        }
#endif
    }

    private static void Log(string message)
    {
        Debug.Log(message);
    }

    private static void WarningLog(string message)
    {
        Debug.LogWarning(message);
    }

    private static void ErrorLog(string message)
    {
        Debug.LogError(message);
    }
}
