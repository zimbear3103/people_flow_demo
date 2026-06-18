using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Code-defined sample levels of increasing difficulty, so the game is fully playable from an
    /// empty scene with no hand-authored assets. The Editor menu "PeopleFlow ▸ Generate Sample
    /// Levels" writes the same data out as LevelData .asset files for designer-friendly tweaking.
    ///
    /// FULL-GROUP DESIGN:
    ///  • Every hole's required count is a multiple of <see cref="GroupSize"/>, and supply is dealt as
    ///    whole single-colour groups of that size. So the waiting line never holds a partial group —
    ///    each tap releases exactly one full colour group, and a full group exactly fills one hole's
    ///    worth of slots. No leftover minions, no half-filled holes.
    ///
    /// SOLVABILITY GUARANTEE:
    ///  • L1–L3 (no locked holes): built with supply == demand, dealt as whole colour groups. Every
    ///    colour has an always-open hole and an exact number of full groups, so any push sequence
    ///    wins. Hidden colour (L3) is visual only.
    ///  • L4–L5 (frozen / gate / lane-barrier): hand-authored with a verified solution order, each
    ///    lane a single full colour group. Locked colours and the barrier's blocked lane never hold
    ///    colour that's needed to *reach* the unlock condition, so there is always a path. Pushing a
    ///    locked colour early (before it unlocks) is how you can still LOSE by jamming — intended tension.
    /// </summary>
    public static class DefaultLevels
    {
        public const int Count = 5;

        /// <summary>Minions per full single-colour group. Hole counts are multiples of this and supply
        /// is dealt in blocks of it, so every released group is full. Mirrors the Lane's release size
        /// (pushed into each lane via <see cref="LaneSetup.groupSize"/>).</summary>
        public const int GroupSize = 3;

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

        // Rounded-rectangle loop (showcases the non-oval shapes; any push order still wins).
        static LevelData Level1() => Shape(Deal(1, 70f, 14, 3.0f, 7f, 11f, new List<HoleSetup>
        {
            H(R, 3, 0.30f),
            H(B, 3, 0.70f),
        }, laneCount: 2, seed: 11), TrackShape.Rectangle, cornerRadius: 2f);

        // Level 2 showcases the hole FACTORY: instead of three separate holes around the loop, one
        // factory produces a bundle of R→G→B in turn. Fill the red hole and it disappears, the green
        // hole takes its place, then blue. Supply is still dealt to match the bundle (3 of each).
        static LevelData Level2() => Deal(2, 65f, 14, 3.4f, 7f, 11f,
            holes: new List<HoleSetup>(),
            laneCount: 3, seed: 22,
            factories: new List<HoleFactorySetup>
            {
                Factory(0.5f, H(R, 3, 0f), H(G, 3, 0f), H(B, 3, 0f)),
            });

        // Sharp-cornered rectangle loop ("kinky" corners — cornerRadius 0).
        static LevelData Level3() => Shape(Deal(3, 60f, 12, 3.7f, 7.5f, 11f, new List<HoleSetup>
        {
            H(R, 6, 0.18f),                  // 6 = two full groups
            H(B, 6, 0.42f),
            H(G, 3, 0.66f),
            H(Y, 3, 0.90f, hidden: true),    // hidden colour: shown as "?" until a runner is close
        }, laneCount: 4, seed: 33), TrackShape.Rectangle);

        // ---- L4: frozen hole + lane barrier (hand-authored & verified) ------
        // Every lane is one full colour group. Solution: Reds (lane 0) → Red done (barrier lifts) →
        // Yellows (lane 1) → Yellow done (Green thaws after 2) → Blues (lane 2) → Blue done →
        // Greens (lane 3) → win.
        static LevelData Level4() => Make(4, 58f, 11, 3.9f, 7.5f, 11.5f,
            holes: new List<HoleSetup>
            {
                H(R, 3, 0.15f),
                H(B, 3, 0.40f),
                H(G, 3, 0.65f, mechanic: HoleMechanic.Frozen, unlockAfter: 2),
                H(Y, 3, 0.88f),
            },
            lanes: new List<LaneSetup>
            {
                L(R, R, R),
                L(Y, Y, Y),
                L(B, B, B),
                Barrier(L(G, G, G), afterHoles: 1),   // blocked until the first hole is done
            });

        // ---- L5: gate hole + arrow zone + hidden (hand-authored & verified) -
        // Every lane is one full colour group. Solution: Reds → Blues (2 done → gate Green unlocks) →
        // Yellows → Purples → Greens → win.
        static LevelData Level5() => Make(5, 55f, 10, 4.2f, 8f, 12f,
            holes: new List<HoleSetup>
            {
                H(R, 3, 0.12f),
                H(B, 3, 0.32f),
                H(G, 3, 0.52f, mechanic: HoleMechanic.Gate, unlockAfter: 2),
                H(Y, 3, 0.72f),
                H(P, 3, 0.90f, hidden: true),
            },
            lanes: new List<LaneSetup>
            {
                L(R, R, R),
                L(B, B, B),
                L(Y, Y, Y),
                L(P, P, P),
                L(G, G, G),
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

        /// <summary>A hole factory at <paramref name="t"/> producing <paramref name="bundle"/> in order
        /// (each bundle entry's own trackPosition is ignored — the factory position is used).</summary>
        static HoleFactorySetup Factory(float t, params HoleSetup[] bundle)
        {
            var f = new HoleFactorySetup { trackPosition = t };
            f.bundle.AddRange(bundle);
            return f;
        }

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

        /// <summary>Override a level's loop shape (and optional corner rounding for Rectangle/Square).
        /// Returns the same data so it can wrap a Deal(...) / Make(...) call inline.</summary>
        static LevelData Shape(LevelData data, TrackShape shape, float cornerRadius = 0f)
        {
            data.trackShape = shape;
            data.cornerRadius = cornerRadius;
            return data;
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

        /// <summary>Auto-dealt level: supply == demand, dealt as whole single-colour groups of
        /// <see cref="GroupSize"/> (seeded). Demand counts both standalone holes and every hole across
        /// the factory bundles. Whole groups are round-robined onto lanes, so each lane holds only
        /// complete colour groups.</summary>
        static LevelData Deal(int number, float time, int cap, float speed, float w, float h,
            List<HoleSetup> holes, int laneCount, int seed, List<HoleFactorySetup> factories = null)
        {
            var data = New(number, time, cap, speed, w, h);
            data.holes = holes;
            if (factories != null) data.holeFactories = factories;

            // Demand per colour = slots across all holes (standalone + every factory-bundle hole),
            // tracked in first-seen order so the seeded shuffle below is deterministic.
            var order = new List<PeopleColor>();
            var demand = new Dictionary<PeopleColor, int>();
            void AddDemand(HoleSetup hole)
            {
                if (!demand.ContainsKey(hole.color)) order.Add(hole.color);
                demand.TryGetValue(hole.color, out int n);
                demand[hole.color] = n + Mathf.Max(1, hole.requiredCount);
            }
            foreach (var hole in holes) AddDemand(hole);
            if (factories != null)
                foreach (var f in factories)
                    foreach (var hole in f.bundle) AddDemand(hole);

            // Break each colour's supply into whole groups of GroupSize so every waiting group is
            // full. (If a colour's demand isn't a multiple of GroupSize, the leftover forms one
            // smaller group so supply still exactly equals demand — solvability is preserved.)
            var groups = new List<List<PeopleColor>>();
            foreach (var color in order)
            {
                int remaining = demand[color];
                while (remaining > 0)
                {
                    int size = Mathf.Min(GroupSize, remaining);
                    var g = new List<PeopleColor>(size);
                    for (int i = 0; i < size; i++) g.Add(color);
                    groups.Add(g);
                    remaining -= size;
                }
            }

            // Shuffle whole groups (never individuals) so lanes get a varied — but always complete —
            // mix of colour groups.
            var rng = new System.Random(seed);
            for (int i = groups.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (groups[i], groups[j]) = (groups[j], groups[i]);
            }

            data.lanes = new List<LaneSetup>();
            for (int i = 0; i < laneCount; i++) data.lanes.Add(new LaneSetup { groupSize = GroupSize });
            for (int i = 0; i < groups.Count; i++)
                data.lanes[i % laneCount].characters.AddRange(groups[i]);

            return data;
        }
    }
}
