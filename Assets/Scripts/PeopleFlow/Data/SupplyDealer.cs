using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Derives the people supply that fills the lanes from the level's <em>hole demand</em>, so a
    /// designer only places holes + lanes and the waiting queues are generated to match
    /// (supply == demand). Example: a factory bundling Red×12 and Blue×12 yields 12 red + 12 blue
    /// previews dealt across the lanes — no hand-authored queues to keep in sync with the holes.
    ///
    /// "Demand" is the total runners each colour needs: the sum of every hole's
    /// <see cref="HoleSetup.requiredCount"/>, across both standalone <see cref="LevelData.holes"/> and
    /// every hole in every <see cref="LevelData.holeFactories"/> bundle. The demand is broken into
    /// whole single-colour groups of <c>groupSize</c> (a trailing remainder forms one smaller group so
    /// supply still equals demand exactly), the groups are shuffled deterministically by <c>seed</c>,
    /// and dealt round-robin across the target lanes.
    ///
    /// Used at authoring time by <see cref="DefaultLevels"/> (so the code samples ship pre-filled) and
    /// at build time by <see cref="LevelManager"/> (to fill any lane the designer left empty).
    /// </summary>
    public static class SupplyDealer
    {
        /// <summary>Minions per full single-colour group when none is otherwise specified.</summary>
        public const int DefaultGroupSize = 3;

        /// <summary>
        /// Total runners needed per colour (standalone holes + every factory-bundle hole), written
        /// into <paramref name="order"/> (colours in first-seen order, so a later seeded shuffle is
        /// deterministic) and <paramref name="demand"/> (colour → count). Both are cleared first.
        /// </summary>
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

        /// <summary>
        /// Break a per-colour demand into whole single-colour groups of at most
        /// <paramref name="groupSize"/> (a leftover forms one smaller group so the total still equals
        /// demand), in <paramref name="order"/> sequence, then shuffle the whole groups
        /// deterministically by <paramref name="seed"/>. Individuals are never split out of a group, so
        /// every dealt group stays single-colour and full.
        /// </summary>
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

        /// <summary>
        /// Resolve the waiting-queue colours for every lane in <paramref name="level"/>: a lane the
        /// designer already authored keeps its list; a lane left empty is filled by dealing the
        /// <em>remaining</em> hole demand (total demand minus what the authored lanes already supply)
        /// as shuffled single-colour groups, round-robined across just the empty lanes — so total
        /// supply still equals demand. Returned per-lane lists line up 1:1 with
        /// <c>level.lanes</c>; entries may be null/empty when there is nothing to deal. Does not mutate
        /// <paramref name="level"/> (authored lanes are returned by reference for read-only use).
        /// </summary>
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

        /// <summary>Group size to deal with: the first empty lane's <see cref="LaneSetup.groupSize"/>
        /// (so released groups match how that lane releases), falling back to
        /// <see cref="DefaultGroupSize"/>.</summary>
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
