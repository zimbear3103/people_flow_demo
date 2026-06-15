using UnityEngine;

namespace PeopleFlow
{
    /// <summary>Per-level tuning for the prototype. Pure offline/standalone data.</summary>
    [System.Serializable]
    public struct PeopleFlowLevelConfig
    {
        public int RequiredCount;   // people needed to fill the target container
        public float SpawnRate;     // people released per second while holding

        public PeopleFlowLevelConfig(int requiredCount, float spawnRate)
        {
            RequiredCount = requiredCount;
            SpawnRate = spawnRate;
        }
    }

    /// <summary>
    /// Static, hard-coded sample levels for the People Flow prototype. The host
    /// (GamePlayController / UIHome) reads <see cref="Count"/> to bound level progression,
    /// and the controller reads <see cref="Get"/> to configure a level. No I/O, no network.
    /// </summary>
    public static class PeopleFlowSampleLevels
    {
        private static readonly PeopleFlowLevelConfig[] s_levels =
        {
            new PeopleFlowLevelConfig(40,  10f),
            new PeopleFlowLevelConfig(80,  12f),
            new PeopleFlowLevelConfig(150, 14f),
        };

        /// <summary>Number of available sample levels.</summary>
        public static int Count => s_levels.Length;

        /// <summary>Returns the config for a level index, clamped to the valid range.</summary>
        public static PeopleFlowLevelConfig Get(int index)
        {
            if (s_levels.Length == 0)
                return new PeopleFlowLevelConfig(50, 10f);
            return s_levels[Mathf.Clamp(index, 0, s_levels.Length - 1)];
        }
    }
}
