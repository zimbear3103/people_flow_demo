namespace PeopleFlow
{
    /// <summary>High-level game flow states owned by <see cref="GameManager"/>.</summary>
    public enum GameState
    {
        Boot,
        Playing,
        Paused,
        Win,
        Lose
    }

    /// <summary>Why a level was lost (shown on the lose popup).</summary>
    public enum LoseReason
    {
        None,
        RunwayFull, // the runway jammed (full + no runner has anywhere left to go)
        TimeOut     // ran out of time before all holes were filled
    }

    /// <summary>
    /// The set of character / hole colours. Keep the ordering in sync with
    /// <see cref="ColorPalette"/>. Add more colours here and in the palette to extend the game.
    /// </summary>
    public enum PeopleColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange
    }

    /// <summary>Special behaviour applied to a hole (section 7 mechanics).</summary>
    public enum HoleMechanic
    {
        None,
        Frozen, // inert + ice-tinted until "unlockAfterHolesCompleted" other holes are complete
        Gate    // visible but barred until "unlockAfterHolesCompleted" other holes are complete
    }
}
