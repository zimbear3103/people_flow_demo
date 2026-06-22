using UnityEngine;

namespace PeopleFlow
{
    public static class SaveManager
    {
        const string KeyHighest = "PF_HighestUnlocked"; // highest level index the player may pick
        const string KeyClearedPrefix = "PF_Cleared_";   // per-level cleared flag
        const string KeyMusic = "PF_MusicOn";
        const string KeySfx = "PF_SfxOn";

        // ---- progression ----------------------------------------------------

        public static int HighestUnlocked
        {
            get => PlayerPrefs.GetInt(KeyHighest, 0);
            private set { PlayerPrefs.SetInt(KeyHighest, value); PlayerPrefs.Save(); }
        }

        public static bool IsCleared(int levelIndex)
            => PlayerPrefs.GetInt(KeyClearedPrefix + levelIndex, 0) == 1;

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
