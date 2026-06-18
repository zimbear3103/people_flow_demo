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

    /// <summary>The geometry of the closed runway loop. All shapes share the generic arc-length
    /// path engine in <see cref="RunwayTrack"/>; they differ only in the corner points generated.</summary>
    public enum TrackShape
    {
        Oval,       // smooth ellipse from loopWidth × loopHeight
        Rectangle,  // loopWidth × loopHeight box; cornerRadius 0 = sharp "kinky" corners, >0 = rounded
        Square,     // square sized to min(loopWidth, loopHeight); cornerRadius applies as for Rectangle
        Custom      // designer-supplied corner points (LevelData.customWaypoints), the manual-setup path
    }

    /// <summary>How the runway is drawn. Both visuals follow the same <see cref="RunwayTrack"/> path,
    /// so they match whatever <see cref="TrackShape"/> is in use.</summary>
    public enum RoadVisual
    {
        RoadTiles,  // tile the Road prefab around the loop (falls back to a line if no prefab is set)
        Line        // draw the path as a LineRenderer through the loop's corner points
    }
}
