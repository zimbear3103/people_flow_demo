using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Thin logging wrapper so every PeopleFlow log is tagged and can be muted in one place.
    /// (The host project also has a global GameLog; this keeps the module self-contained.)
    /// </summary>
    public static class PFLog
    {
        public static bool Enabled = true;
        const string Tag = "<color=#7FB3FF>[PeopleFlow]</color> ";

        public static void Info(string msg)
        {
            if (Enabled) Debug.Log(Tag + msg);
        }

        public static void Warn(string msg)
        {
            if (Enabled) Debug.LogWarning(Tag + msg);
        }

        public static void Error(string msg) => Debug.LogError(Tag + msg);
    }
}
