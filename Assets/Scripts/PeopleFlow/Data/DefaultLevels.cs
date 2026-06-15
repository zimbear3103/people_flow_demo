using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Code-defined sample levels of increasing difficulty, so the game is fully playable from an
    /// empty scene with no hand-authored assets. The Editor menu "PeopleFlow ▸ Generate Sample
    /// Levels" writes the same data out as LevelData .asset files for designer-friendly tweaking.
    ///
    /// Difficulty curve: L1 2 colours / roomy → L5 5 colours, tight capacity, gate + arrow.
    /// Every level is built so total colour supply ≥ demand (guaranteed solvable); colours that
    /// belong to a locked hole are pushed to the BACK of their lanes so you can always make
    /// progress elsewhere first.
    /// </summary>
    public static class DefaultLevels
    {
        public const int Count = 5;

        // Short aliases keep the level tables readable.
        const PeopleColor R = PeopleColor.Red;
        const PeopleColor B = PeopleColor.Blue;
        const PeopleColor G = PeopleColor.Green;
        const PeopleColor Y = PeopleColor.Yellow;
        const PeopleColor P = PeopleColor.Purple;

        public static LevelData Get(int index)
        {
            switch (Mathf.Clamp(index, 0, Count - 1))
            {
                case 0: return Level1();
                case 1: return Level2();
                case 2: return Level3();
                case 3: return Level4();
                default: return Level5();
            }
        }

        // ---- levels ---------------------------------------------------------

        static LevelData Level1() => Build(1, 70f, 14, 3.0f, 7f, 11f, new List<HoleSetup>
        {
            H(R, 3, 0.30f),
            H(B, 3, 0.70f),
        }, laneCount: 2, seed: 11);

        static LevelData Level2() => Build(2, 65f, 14, 3.4f, 7f, 11f, new List<HoleSetup>
        {
            H(R, 3, 0.20f),
            H(G, 3, 0.50f),
            H(B, 3, 0.80f),
        }, laneCount: 3, seed: 22, decoys: new[] { R, G, B });

        static LevelData Level3() => Build(3, 60f, 12, 3.7f, 7.5f, 11f, new List<HoleSetup>
        {
            H(R, 4, 0.18f),
            H(B, 4, 0.42f),
            H(G, 3, 0.66f),
            H(Y, 3, 0.90f, hidden: true),    // hidden colour: shown as "?" until a runner is close
        }, laneCount: 4, seed: 33, decoys: new[] { R, B });

        static LevelData Level4()
        {
            var data = Build(4, 58f, 11, 3.9f, 7.5f, 11.5f, new List<HoleSetup>
            {
                H(R, 4, 0.15f),
                H(B, 4, 0.40f),
                H(G, 4, 0.65f, mechanic: HoleMechanic.Frozen, unlockAfter: 2), // thaws after 2 holes
                H(Y, 3, 0.88f),
            }, laneCount: 4, seed: 44, decoys: new[] { Y, R }, locked: new[] { G });

            // Lane barrier: the last lane is blocked until the first hole is completed.
            var last = data.lanes[data.lanes.Count - 1];
            last.barrier = true;
            last.unlockAfterHolesCompleted = 1;
            return data;
        }

        static LevelData Level5()
        {
            var data = Build(5, 55f, 10, 4.2f, 8f, 12f, new List<HoleSetup>
            {
                H(R, 4, 0.12f),
                H(B, 4, 0.32f),
                H(G, 4, 0.52f, mechanic: HoleMechanic.Gate, unlockAfter: 2), // gate opens after 2 holes
                H(Y, 4, 0.72f),
                H(P, 4, 0.90f, hidden: true),
            }, laneCount: 5, seed: 55, decoys: new[] { R, B, G }, locked: new[] { G });

            // Arrow / speed zone: runners sprint through the back stretch.
            data.arrows.Add(new ArrowSetup { trackPosition = 0.60f, length = 0.12f, speedMultiplier = 3f });
            return data;
        }

        // ---- builder --------------------------------------------------------

        static HoleSetup H(PeopleColor c, int n, float t, bool hidden = false,
            HoleMechanic mechanic = HoleMechanic.None, int unlockAfter = 0)
            => new HoleSetup
            {
                color = c,
                requiredCount = n,
                trackPosition = t,
                hidden = hidden,
                mechanic = mechanic,
                unlockAfterHolesCompleted = unlockAfter,
            };

        /// <summary>
        /// Builds a guaranteed-solvable level. Supply = exactly what the holes need, plus optional
        /// decoys. Colours are dealt round-robin so they interleave across lanes; any colour in
        /// <paramref name="locked"/> is dealt last so it sits at the back of its lanes.
        /// </summary>
        static LevelData Build(int number, float time, int capacity, float speed, float w, float h,
            List<HoleSetup> holes, int laneCount, int seed,
            PeopleColor[] decoys = null, PeopleColor[] locked = null)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            data.name = $"Level_{number:00}";
            data.levelNumber = number;
            data.timeLimit = time;
            data.runwayCapacity = capacity;
            data.runSpeed = speed;
            data.loopWidth = w;
            data.loopHeight = h;
            data.holes = holes;
            data.arrows = new List<ArrowSetup>();

            var lockedSet = new HashSet<PeopleColor>(locked ?? new PeopleColor[0]);

            var free = new List<PeopleColor>();
            var back = new List<PeopleColor>();
            void Add(PeopleColor c) { (lockedSet.Contains(c) ? back : free).Add(c); }

            foreach (var hole in holes)
                for (int i = 0; i < hole.requiredCount; i++) Add(hole.color);
            if (decoys != null) foreach (var c in decoys) Add(c);

            var rng = new System.Random(seed);
            Shuffle(free, rng);
            Shuffle(back, rng);

            data.lanes = new List<LaneSetup>();
            for (int i = 0; i < laneCount; i++) data.lanes.Add(new LaneSetup());

            int slot = 0;
            foreach (var c in free) data.lanes[slot++ % laneCount].characters.Add(c);
            foreach (var c in back) data.lanes[slot++ % laneCount].characters.Add(c);

            return data;
        }

        static void Shuffle(List<PeopleColor> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
