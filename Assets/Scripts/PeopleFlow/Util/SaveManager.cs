using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Persists cleared levels and audio settings via PlayerPrefs. Static + stateless so any
    /// system can read/write without a reference. Keys are namespaced with "PF_".
    /// </summary>
    public static class SaveManager
    {
        const string KeyHighest = "PF_HighestUnlocked"; // highest level index the player may pick
        const string KeyClearedPrefix = "PF_Cleared_";   // per-level cleared flag
        const string KeyMusic = "PF_MusicOn";
        const string KeySfx = "PF_SfxOn";

        // ---- progression ----------------------------------------------------

        /// <summary>Highest level index the player has unlocked (0-based). Always at least 0.</summary>
        public static int HighestUnlocked
        {
            get => PlayerPrefs.GetInt(KeyHighest, 0);
            private set { PlayerPrefs.SetInt(KeyHighest, value); PlayerPrefs.Save(); }
        }

        public static bool IsCleared(int levelIndex)
            => PlayerPrefs.GetInt(KeyClearedPrefix + levelIndex, 0) == 1;

        /// <summary>Marks a level cleared and unlocks the next one.</summary>
        public static void MarkCleared(int levelIndex)
        {
            PlayerPrefs.SetInt(KeyClearedPrefix + levelIndex, 1);
            if (levelIndex + 1 > HighestUnlocked)
                HighestUnlocked = levelIndex + 1;
            PlayerPrefs.Save();
        }

        // ---- settings -------------------------------------------------------

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(KeyMusic, 1) == 1;
            set { PlayerPrefs.SetInt(KeyMusic, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SfxOn
        {
            get => PlayerPrefs.GetInt(KeySfx, 1) == 1;
            set { PlayerPrefs.SetInt(KeySfx, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
