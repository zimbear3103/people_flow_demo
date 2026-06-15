using UnityEngine;

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
    }
}
