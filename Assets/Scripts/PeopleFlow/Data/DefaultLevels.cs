using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    public static class DefaultLevels
    {
        public const int Count = 5;

        public const int GroupSize = SupplyDealer.DefaultGroupSize;

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
            H(R, GroupSize, 0.30f),
            H(B, GroupSize, 0.70f),
        }, laneCount: 2, seed: 11), TrackShape.Rectangle, cornerRadius: 2f);

        // Level 2 showcases the hole FACTORY: instead of three separate holes around the loop, one
        // factory produces a bundle of R→G→B in turn. Fill the red hole and it disappears, the green
        // hole takes its place, then blue. Supply is still dealt to match the bundle (one full group of each).
        static LevelData Level2() => Deal(2, 65f, 14, 3.4f, 7f, 11f,
            holes: new List<HoleSetup>(),
            laneCount: 3, seed: 22,
            factories: new List<HoleFactorySetup>
            {
                Factory(0.5f, H(R, GroupSize, 0f), H(G, GroupSize, 0f), H(B, GroupSize, 0f)),
            });

        // Sharp-cornered rectangle loop ("kinky" corners — cornerRadius 0).
        static LevelData Level3() => Shape(Deal(3, 60f, 12, 3.7f, 7.5f, 11f, new List<HoleSetup>
        {
            H(R, GroupSize * 2, 0.18f),          // two full groups
            H(B, GroupSize * 2, 0.42f),
            H(G, GroupSize, 0.66f),
            H(Y, GroupSize, 0.90f, hidden: true),// hidden colour: shown as "?" until a runner is close
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

        static LevelData Make(int number, float time, int cap, float speed, float w, float h,
            List<HoleSetup> holes, List<LaneSetup> lanes, List<ArrowSetup> arrows = null)
        {
            var data = New(number, time, cap, speed, w, h);
            data.holes = holes;
            data.lanes = lanes;
            if (arrows != null) data.arrows = arrows;
            return data;
        }

        static LevelData Deal(int number, float time, int cap, float speed, float w, float h,
            List<HoleSetup> holes, int laneCount, int seed, List<HoleFactorySetup> factories = null)
        {
            var data = New(number, time, cap, speed, w, h);
            data.holes = holes;
            if (factories != null) data.holeFactories = factories;

            // Supply == demand: total per-colour hole demand, broken into whole single-colour groups
            // of GroupSize and shuffled (seeded), then round-robined onto the lanes. This is the same
            // dealing LevelManager runs at build time to fill empty lanes — done here at authoring
            // time so the generated sample assets ship with their queues already populated.
            var order = new List<PeopleColor>();
            var demand = new Dictionary<PeopleColor, int>();
            SupplyDealer.ComputeDemand(data, order, demand);
            var groups = SupplyDealer.DealGroups(order, demand, GroupSize, seed);

            data.lanes = new List<LaneSetup>();
            for (int i = 0; i < laneCount; i++) data.lanes.Add(new LaneSetup { groupSize = GroupSize });
            for (int i = 0; i < groups.Count; i++)
                data.lanes[i % laneCount].characters.AddRange(groups[i]);

            return data;
        }
    }
}
