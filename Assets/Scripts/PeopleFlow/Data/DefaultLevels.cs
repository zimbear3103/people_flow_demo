using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Code-defined sample levels of increasing difficulty, so the game is fully playable from an
    /// empty scene with no hand-authored assets. The Editor menu "PeopleFlow ▸ Generate Sample
    /// Levels" writes the same data out as LevelData .asset files for designer-friendly tweaking.
    ///
    /// SOLVABILITY GUARANTEE:
    ///  • L1–L3 (no locked holes): built with supply == demand and dealt round-robin. Every colour
    ///    has an always-open hole and an exact number of slots, so any push sequence wins. Hidden
    ///    colour (L3) is visual only.
    ///  • L4–L5 (frozen / gate / lane-barrier): hand-authored with a verified solution order. Locked
    ///    colours and the barrier's blocked lane never hold colour that's needed to *reach* the
    ///    unlock condition, so there is always a path. Pushing a locked colour early (before it
    ///    unlocks) is how you can still LOSE by jamming — that's the intended tension.
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

        // ---- L1–L3: auto-dealt, supply == demand, all holes open -----------

        static LevelData Level1() => Deal(1, 70f, 14, 3.0f, 7f, 11f, new List<HoleSetup>
        {
            H(R, 3, 0.30f),
            H(B, 3, 0.70f),
        }, laneCount: 2, seed: 11);

        static LevelData Level2() => Deal(2, 65f, 14, 3.4f, 7f, 11f, new List<HoleSetup>
        {
            H(R, 3, 0.20f),
            H(G, 3, 0.50f),
            H(B, 3, 0.80f),
        }, laneCount: 3, seed: 22);

        static LevelData Level3() => Deal(3, 60f, 12, 3.7f, 7.5f, 11f, new List<HoleSetup>
        {
            H(R, 4, 0.18f),
            H(B, 4, 0.42f),
            H(G, 3, 0.66f),
            H(Y, 3, 0.90f, hidden: true),    // hidden colour: shown as "?" until a runner is close
        }, laneCount: 4, seed: 33);

        // ---- L4: frozen hole + lane barrier (hand-authored & verified) ------
        // Solution: 4 Reds (lanes 0-2) → Red done (barrier lifts) → 3 Yellows → Yellow done
        // (Green thaws) → 4 Blues → Blue done → 4 Greens → win.
        static LevelData Level4() => Make(4, 58f, 11, 3.9f, 7.5f, 11.5f,
            holes: new List<HoleSetup>
            {
                H(R, 4, 0.15f),
                H(B, 4, 0.40f),
                H(G, 4, 0.65f, mechanic: HoleMechanic.Frozen, unlockAfter: 2),
                H(Y, 3, 0.88f),
            },
            lanes: new List<LaneSetup>
            {
                L(R, R, Y, B),
                L(R, Y, B, G),
                L(R, Y, B, G),
                Barrier(L(B, G, G), afterHoles: 1),   // blocked until the first hole is done
            });

        // ---- L5: gate hole + arrow zone + hidden (hand-authored & verified) -
        // Solution: 4 Reds → 4 Blues (gate opens) → 4 Yellows → 4 Purples → 4 Greens → win.
        static LevelData Level5() => Make(5, 55f, 10, 4.2f, 8f, 12f,
            holes: new List<HoleSetup>
            {
                H(R, 4, 0.12f),
                H(B, 4, 0.32f),
                H(G, 4, 0.52f, mechanic: HoleMechanic.Gate, unlockAfter: 2),
                H(Y, 4, 0.72f),
                H(P, 4, 0.90f, hidden: true),
            },
            lanes: new List<LaneSetup>
            {
                L(R, B, Y, G),
                L(R, B, Y, G),
                L(R, B, P, G),
                L(R, B, P, G),
                L(Y, Y, P, P),
            },
            arrows: new List<ArrowSetup>
            {
                new ArrowSetup { trackPosition = 0.60f, length = 0.12f, speedMultiplier = 3f },
            });

        // ---- builders -------------------------------------------------------

        static HoleSetup H(PeopleColor c, int n, float t, bool hidden = false,
            HoleMechanic mechanic = HoleMechanic.None, int unlockAfter = 0)
            => new HoleSetup
            {
                color = c, requiredCount = n, trackPosition = t,
                hidden = hidden, mechanic = mechanic, unlockAfterHolesCompleted = unlockAfter,
            };

        static LaneSetup L(params PeopleColor[] colors)
        {
            var lane = new LaneSetup();
            lane.characters.AddRange(colors);
            return lane;
        }

        static LaneSetup Barrier(LaneSetup lane, int afterHoles)
        {
            lane.barrier = true;
            lane.unlockAfterHolesCompleted = afterHoles;
            return lane;
        }

        static LevelData New(int number, float time, int cap, float speed, float w, float h)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            data.name = $"Level_{number:00}";
            data.levelNumber = number;
            data.timeLimit = time;
            data.runwayCapacity = cap;
            data.runSpeed = speed;
            data.loopWidth = w;
            data.loopHeight = h;
            data.arrows = new List<ArrowSetup>();
            return data;
        }

        /// <summary>Hand-authored level: explicit holes + lanes (used where solvability needs care).</summary>
        static LevelData Make(int number, float time, int cap, float speed, float w, float h,
            List<HoleSetup> holes, List<LaneSetup> lanes, List<ArrowSetup> arrows = null)
        {
            var data = New(number, time, cap, speed, w, h);
            data.holes = holes;
            data.lanes = lanes;
            if (arrows != null) data.arrows = arrows;
            return data;
        }

        /// <summary>Auto-dealt level: supply == demand, colours interleaved round-robin (seeded).</summary>
        static LevelData Deal(int number, float time, int cap, float speed, float w, float h,
            List<HoleSetup> holes, int laneCount, int seed)
        {
            var data = New(number, time, cap, speed, w, h);
            data.holes = holes;

            var bag = new List<PeopleColor>();
            foreach (var hole in holes)
                for (int i = 0; i < hole.requiredCount; i++) bag.Add(hole.color);

            var rng = new System.Random(seed);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            data.lanes = new List<LaneSetup>();
            for (int i = 0; i < laneCount; i++) data.lanes.Add(new LaneSetup());
            for (int i = 0; i < bag.Count; i++)
                data.lanes[i % laneCount].characters.Add(bag[i]);

            return data;
        }
    }
}
