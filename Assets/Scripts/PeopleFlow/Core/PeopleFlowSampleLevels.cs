namespace PeopleFlow
{
    /// <summary>
    /// Stable facade the host game (UIHome / GamePlayController) references for the sample-level
    /// count and lookup. Delegates to <see cref="DefaultLevels"/> so there is a single source of
    /// truth for how many built-in levels exist.
    /// </summary>
    public static class PeopleFlowSampleLevels
    {
        /// <summary>Number of built-in sample levels.</summary>
        public static int Count => DefaultLevels.Count;

        /// <summary>Get a built-in level by 0-based index (clamped).</summary>
        public static LevelData Get(int index) => DefaultLevels.Get(index);
    }
}
