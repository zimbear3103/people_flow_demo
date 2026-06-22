using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    public static class SupplyDealer
    {
        public const int DefaultGroupSize = 4;

        public static void ComputeDemand(LevelData level, List<PeopleColor> order, Dictionary<PeopleColor, int> demand)
        {
            order.Clear();
            demand.Clear();
            if (level == null) return;

            void Add(HoleSetup hole)
            {
                if (hole == null) return;
                if (!demand.ContainsKey(hole.color)) order.Add(hole.color);
                demand.TryGetValue(hole.color, out int n);
                demand[hole.color] = n + Mathf.Max(1, hole.requiredCount);
            }

            if (level.holes != null)
                foreach (var hole in level.holes) Add(hole);
            if (level.holeFactories != null)
                foreach (var f in level.holeFactories)
                    if (f != null && f.bundle != null)
                        foreach (var hole in f.bundle) Add(hole);
        }

        public static List<List<PeopleColor>> DealGroups(List<PeopleColor> order,
            Dictionary<PeopleColor, int> demand, int groupSize, int seed)
        {
            int g = Mathf.Max(1, groupSize);
            var groups = new List<List<PeopleColor>>();
            if (order == null || demand == null) return groups;

            foreach (var color in order)
            {
                demand.TryGetValue(color, out int remaining);
                while (remaining > 0)
                {
                    int size = Mathf.Min(g, remaining);
                    var group = new List<PeopleColor>(size);
                    for (int i = 0; i < size; i++) group.Add(color);
                    groups.Add(group);
                    remaining -= size;
                }
            }

            // Shuffle whole groups (never individuals) so lanes get a varied — but always complete —
            // mix of colour groups. Seeded so a level deals the same way every build / restart.
            var rng = new System.Random(seed);
            for (int i = groups.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (groups[i], groups[j]) = (groups[j], groups[i]);
            }
            return groups;
        }

        public static List<List<PeopleColor>> ResolveLaneSupply(LevelData level, int seed)
        {
            int n = level != null && level.lanes != null ? level.lanes.Count : 0;
            var result = new List<List<PeopleColor>>(n);
            for (int i = 0; i < n; i++) result.Add(level.lanes[i] != null ? level.lanes[i].characters : null);
            if (n == 0) return result;

            // Find the empty lanes (to fill) and tally what the authored lanes already supply.
            var emptyLanes = new List<int>();
            var supplied = new Dictionary<PeopleColor, int>();
            for (int i = 0; i < n; i++)
            {
                var chars = level.lanes[i] != null ? level.lanes[i].characters : null;
                if (chars == null || chars.Count == 0) { emptyLanes.Add(i); continue; }
                foreach (var c in chars)
                {
                    supplied.TryGetValue(c, out int k);
                    supplied[c] = k + 1;
                }
            }
            if (emptyLanes.Count == 0) return result; // every lane hand-authored — nothing to derive

            // Remaining demand = total hole demand minus what the authored lanes already cover.
            var order = new List<PeopleColor>();
            var demand = new Dictionary<PeopleColor, int>();
            ComputeDemand(level, order, demand);

            var remainingOrder = new List<PeopleColor>();
            var remaining = new Dictionary<PeopleColor, int>();
            foreach (var color in order)
            {
                supplied.TryGetValue(color, out int already);
                int rem = demand[color] - already;
                if (rem > 0) { remainingOrder.Add(color); remaining[color] = rem; }
            }
            if (remainingOrder.Count == 0) return result; // authored lanes already meet demand

            int groupSize = EmptyLaneGroupSize(level, emptyLanes);
            var groups = DealGroups(remainingOrder, remaining, groupSize, seed);

            // Deal into a FRESH list per empty lane (never the asset's own list — appending to that
            // would mutate the LevelData), then round-robin the dealt groups across them.
            foreach (int idx in emptyLanes) result[idx] = new List<PeopleColor>();
            for (int gi = 0; gi < groups.Count; gi++)
                result[emptyLanes[gi % emptyLanes.Count]].AddRange(groups[gi]);
            return result;
        }

        static int EmptyLaneGroupSize(LevelData level, List<int> emptyLanes)
        {
            foreach (int idx in emptyLanes)
            {
                int g = level.lanes[idx] != null ? level.lanes[idx].groupSize : 0;
                if (g > 0) return g;
            }
            return DefaultGroupSize;
        }
    }
}
