namespace PeopleFlow
{
    public enum GameState
    {
        Boot,
        Playing,
        Paused,
        Win,
        Lose
    }

    public enum LoseReason
    {
        None,
        RunwayFull, // the runway jammed (full + no runner has anywhere left to go)
        TimeOut     // ran out of time before all holes were filled
    }

    public enum PeopleColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange
    }

    public enum HoleMechanic
    {
        None,
        Frozen, // inert + ice-tinted until "unlockAfterHolesCompleted" other holes are complete
        Gate    // visible but barred until "unlockAfterHolesCompleted" other holes are complete
    }

    public enum TrackShape
    {
        Oval,       // smooth ellipse from loopWidth × loopHeight
        Rectangle,  // loopWidth × loopHeight box; cornerRadius 0 = sharp "kinky" corners, >0 = rounded
        Square,     // square sized to min(loopWidth, loopHeight); cornerRadius applies as for Rectangle
        Custom      // designer-supplied corner points (LevelData.customWaypoints), the manual-setup path
    }

    public enum RoadVisual
    {
        RoadTiles,  // tile the Road prefab around the loop (falls back to a line if no prefab is set)
        Line        // draw the path as a simple line through the loop's path points
    }
}
