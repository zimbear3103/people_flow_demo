using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeopleFlow
{
    /// <summary>
    /// Tiny static carrier for state that must survive a scene load (which level to start).
    /// The MainMenu writes <see cref="CurrentLevelIndex"/>; the Game scene's bootstrap reads it.
    /// </summary>
    public static class GameSession
    {
        /// <summary>Zero-based index into the available levels.</summary>
        public static int CurrentLevelIndex { get; set; }

        /// <summary>Scene names — must match the scenes added to Build Settings.</summary>
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        /// <summary>Clamp + load the requested level index, persisting it for the next scene.</summary>
        public static void PlayLevel(int index)
        {
            CurrentLevelIndex = Mathf.Max(0, index);
        }

        // ---- scene navigation (used by the standalone PeopleFlow UI; moved off the old GameManager) ----

        public static void Restart() => LoadScene(GameScene);
        public static void GoToMenu() => LoadScene(MenuScene);

        /// <summary>Advance to the next built-in level, or back to the menu if there are no more.</summary>
        public static void GoToNextLevel()
        {
            int next = CurrentLevelIndex + 1;
            if (next >= DefaultLevels.Count) { GoToMenu(); return; }
            CurrentLevelIndex = next;
            LoadScene(GameScene);
        }

        static void LoadScene(string sceneName)
        {
            Time.timeScale = 1f;
            // Fall back to reloading the active scene if the named scene isn't in Build Settings.
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                SceneManager.LoadScene(sceneName);
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
