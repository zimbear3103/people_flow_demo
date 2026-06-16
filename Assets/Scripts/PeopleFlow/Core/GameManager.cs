using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeopleFlow
{
    /// <summary>
    /// Central referee for one level. Owns the <see cref="GameState"/>, counts completed holes,
    /// and decides win/lose. Everything else talks to the game through its events instead of
    /// referencing each other directly.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public int CompletedHoles { get; private set; }
        public int TotalHoles { get; private set; }
        public LoseReason LastLoseReason { get; private set; }

        public event Action<GameState> OnStateChanged;
        public event Action<int, int> OnHoleProgress; // completed, total
        public event Action OnLevelWin;
        public event Action<LoseReason> OnLevelLose;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Time.timeScale = 1f; // recover from a paused state across scene reloads
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- lifecycle ------------------------------------------------------

        public void BeginLevel(int totalHoles)
        {
            TotalHoles = totalHoles;
            CompletedHoles = 0;
            LastLoseReason = LoseReason.None;
            Time.timeScale = 1f;
            SetState(GameState.Playing);
            OnHoleProgress?.Invoke(CompletedHoles, TotalHoles);
        }

        public void ReportHoleCompleted(Hole hole)
        {
            if (State != GameState.Playing) return;
            CompletedHoles++;
            OnHoleProgress?.Invoke(CompletedHoles, TotalHoles);
            if (CompletedHoles >= TotalHoles) Win();
        }

        public void ReportRunwayJam()
        {
            if (State == GameState.Playing) Lose(LoseReason.RunwayFull);
        }

        public void ReportTimeOut()
        {
            if (State == GameState.Playing) Lose(LoseReason.TimeOut);
        }

        /// <summary>Cheat / bridge hook: force an immediate win.</summary>
        public void ForceWin()
        {
            if (State == GameState.Playing || State == GameState.Paused) { Time.timeScale = 1f; Win(); }
        }

        /// <summary>Cheat / bridge hook: force an immediate loss.</summary>
        public void ForceLose(LoseReason reason = LoseReason.RunwayFull)
        {
            if (State == GameState.Playing || State == GameState.Paused) { Time.timeScale = 1f; Lose(reason); }
        }

        void Win()
        {
            SetState(GameState.Win);
            SaveManager.MarkCleared(GameSession.CurrentLevelIndex);
            AudioManager.Instance?.PlayWin();
            OnLevelWin?.Invoke();
        }

        void Lose(LoseReason reason)
        {
            LastLoseReason = reason;
            SetState(GameState.Lose);
            AudioManager.Instance?.PlayLose();
            OnLevelLose?.Invoke(reason);
        }

        // ---- pause ----------------------------------------------------------

        public void Pause()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        // ---- navigation -----------------------------------------------------

        public void Restart() => LoadScene(GameSession.GameScene);
        public void GoToMenu() => LoadScene(GameSession.MenuScene);

        public void GoToNextLevel()
        {
            int next = GameSession.CurrentLevelIndex + 1;
            if (next >= DefaultLevels.Count) { GoToMenu(); return; }
            GameSession.CurrentLevelIndex = next;
            LoadScene(GameSession.GameScene);
        }

        void LoadScene(string sceneName)
        {
            Time.timeScale = 1f;
            // Fall back to reloading the active scene if the named scene isn't in Build Settings.
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                SceneManager.LoadScene(sceneName);
            else
            {
                PFLog.Warn($"Scene '{sceneName}' not in Build Settings — reloading current scene.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        void SetState(GameState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
