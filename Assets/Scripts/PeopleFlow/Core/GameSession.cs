using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeopleFlow
{
    public static class GameSession
    {
        public static int CurrentLevelIndex { get; set; }

        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        public static void PlayLevel(int index)
        {
            CurrentLevelIndex = Mathf.Max(0, index);
        }

        // ---- scene navigation (used by the standalone PeopleFlow UI; moved off the old GameManager) ----

        public static void Restart() => LoadScene(GameScene);
        public static void GoToMenu() => LoadScene(MenuScene);

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
