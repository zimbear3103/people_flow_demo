# People Flow — color-matching loop puzzle

A complete, immediately-playable hyper-casual puzzle game (Unity 6 / URP / new Input System).
Tap-and-hold lanes to push colored people onto a closed loop runway; they run around and dive
into matching-color holes. Fill every hole before the runway **jams** or **time runs out**.

Everything is built **procedurally from primitives + code-made materials** — there are no
copyrighted assets and (almost) nothing to wire by hand.

---

## 0. TL;DR — play it in 60 seconds

1. In Unity, create a new scene and name it **`Game`** (File ▸ New Scene ▸ save as `Game`).
2. Create an empty GameObject (`GameObject ▸ Create Empty`), name it **`GameBootstrap`**.
3. Add the **`GameBootstrap`** component to it (Add Component ▸ search "Game Bootstrap").
4. On the component, set **Force Level Index = 0** (so it ignores the menu and just loads level 1).
5. Press **Play**. The camera, light, managers, UI, runway, holes, lanes and characters are all
   created at runtime. **Tap-and-hold the gray pads at the bottom** to push people onto the loop.

That's the whole game running. The rest of this doc adds the main menu, the Android build, the
designer-friendly level assets, and how to extend it.

> If you see **magenta** objects, your project has no active URP asset — see §7 "URP".

---

## 1. Architecture at a glance

Systems are decoupled through **C# events on a central referee** (`GameManager`). Nobody holds a
web of references to everybody.

```
GameBootstrap ──builds──▶ camera / light / managers / UI
        │
        └─▶ LevelManager.Build(LevelData)
                ├─ RunwayTrack   (oval polyline, arc-length lookup, capacity, JAM detection)
                ├─ Hole × N      (color, requiredCount, reservation, specials)
                ├─ Lane × M      (queue, tap-hold release, barrier)
                └─ binds InputManager / Timer / UIManager, then GameManager.BeginLevel()

Hole.Commit() ─────▶ GameManager.ReportHoleCompleted()  ─┐
RunwayTrack (full + ▶ GameManager.ReportRunwayJam()      ├─▶ decides Win / Lose
   no viable move)                                       │   and raises events:
Timer hits 0  ─────▶ GameManager.ReportTimeOut()        ─┘   OnHoleProgress / OnLevelWin /
                                                              OnLevelLose / OnStateChanged
UIManager & AudioManager just SUBSCRIBE to those events.
```

**Why it can't be cheesed:** pushing is blocked when the runway is at capacity, and you only lose
by *RunwayFull* on a genuine **deadlock** (runway full **and** no runner has any matching, unlocked,
not-yet-full hole left). A momentarily full bar that can still drain is not a loss.

**Why fills never race:** each incoming runner **reserves** a hole slot (`TryReserve`) before it
hops in, so two runners can never both claim the last slot of a 3/3 hole.

**Why nothing is skipped at speed:** runners detect a hole by **crossing** its normalized track
position between frames (`PassedForward`, wrap-aware at t=0/1), not by a proximity sphere.

---

## 2. Script index (all in `namespace PeopleFlow`, under `Assets/Scripts/PeopleFlow/`)

| Folder | Script | Role |
|---|---|---|
| Core | `GameEnums.cs` | `GameState`, `LoseReason`, `PeopleColor`, `HoleMechanic` |
| Core | `GameSession.cs` | static carrier for selected level + scene names |
| Core | `GameManager.cs` | state machine + win/lose referee + events + navigation |
| Core | `Timer.cs` | level countdown → `ReportTimeOut` |
| Core | `InputManager.cs` | tap-and-hold per lane (Pointer raycast) |
| Core | `LevelManager.cs` | builds runway/holes/lanes from `LevelData`, wires everything |
| Data | `ColorPalette.cs` | `PeopleColor` → pastel `Color` |
| Data | `LevelData.cs` | ScriptableObject + `LaneSetup`/`HoleSetup`/`ArrowSetup` |
| Data | `DefaultLevels.cs` | 5 code-defined, guaranteed-solvable sample levels |
| Gameplay | `RunwayTrack.cs` | loop geometry, capacity, fill %, jam detection |
| Gameplay | `Hole.cs` | color/required/filled, reservation, specials, visuals |
| Gameplay | `People.cs` | runs the loop, reserves + hops into holes |
| Gameplay | `Lane.cs` | character queue, hold-release timing, barrier, preview |
| Build | `MaterialLibrary.cs` | runtime URP material cache (per color + named) |
| Build | `Prim.cs` | primitive + particle-burst factory |
| Build | `GameBootstrap.cs` | **one-component scene builder / entry point** |
| UI | `UIManager.cs` | auto-built HUD + Win/Lose/Pause popups |
| UI | `MainMenuController.cs` | auto-built menu, level select, audio toggles |
| Audio | `AudioManager.cs` | SFX/music (optional clips) |
| Util | `SaveManager.cs` | PlayerPrefs: cleared levels + settings |
| Util | `Haptics.cs` | light mobile vibration |
| Util | `TweenUtil.cs` | coroutine easings, pop & arc-hop |
| Util | `PFLog.cs` | tagged logging |
| Editor | `SampleLevelGenerator.cs` | menu **PeopleFlow ▸ Generate Sample Levels** |

---

## 3. Scenes & the explicit hierarchy

You only strictly need the two scenes below; the bootstrap builds their contents. The hierarchy it
produces (and which you *could* author by hand instead) maps to §3 of the brief like this:

```
Game scene
├─ GameBootstrap            (GameBootstrap.cs)            ← the only object you place
├─ Main Camera              (angled ~52°, perspective)    ← auto
├─ Directional Light                                       ← auto
├─ GameManager / AudioManager / UIManager / InputManager / Timer / LevelManager  ← auto (one each)
├─ EventSystem              (InputSystemUIInputModule)     ← auto
├─ PF_Canvas                (HUD + popups)                 ← auto
├─ RunwayTrack              (+ Ground, TrackLine, EntryMarker)
├─ Holes / Hole_Red …       (Hole.cs each)
├─ Lanes / Lane_0 …         (Lane.cs each, child "Pad" has the BoxCollider)
└─ CharactersRoot / Runner_*  (People.cs each)
```

**Create the two scenes:**

- `MainMenu` scene → empty GameObject + **`MainMenuController`** component.
- `Game` scene → empty GameObject + **`GameBootstrap`** component.

Both scene **names must be exactly** `MainMenu` and `Game` (they're referenced by
`GameSession.MenuScene/GameScene`). Add them to **File ▸ Build Profiles / Build Settings ▸ Scene
List** with **MainMenu first (index 0)** and **Game second (index 1)**.

---

## 4. "Prefabs" (built at runtime; here's the blueprint)

This architecture **builds its objects in code** (`Hole.BuildVisuals`, `Lane.BuildVisuals`,
`People.Spawn`), so you don't need prefab assets to play. If you'd rather author
real prefabs later, here is the equivalent component spec for each:

- **Character** — root (empty) with `People`; children: Capsule "Body",
  Sphere "Head"; no collider. Color material applied at spawn.
- **Hole** — root (empty) with `Hole`; children: flat Cylinder "Ring" (colored rim), Cylinder
  "Inner" (dark), N Sphere "Pip" (progress), optional "Ice"/"Gate" overlay, "Burst" ParticleSystem.
- **Lane** — root (empty) with `Lane`; child Cube "Pad" **with BoxCollider** (this is what the
  pointer raycast hits), preview Cubes, optional "Barrier" cube.
- **Runway** — `RunwayTrack` on an empty at the origin; the LevelManager adds Ground (Plane),
  a looped `LineRenderer` "TrackLine", and an "EntryMarker".

> To switch from procedural to authored prefabs you'd remove the `BuildVisuals`/`Spawn` body
> construction and `Instantiate` your prefabs instead — the logic components are already separated
> from their visuals for exactly this.

---

## 5. Level data & the 5 sample levels

`LevelData` is a ScriptableObject (`Assets ▸ Create ▸ PeopleFlow ▸ Level`). Fields:

```
levelNumber, timeLimit, runwayCapacity, runSpeed, loopWidth, loopHeight,
lanes  : List<LaneSetup>  { characters[], barrier, unlockAfterHolesCompleted }
holes  : List<HoleSetup>  { color, requiredCount, trackPosition(0..1), hidden, mechanic, unlockAfterHolesCompleted }
arrows : List<ArrowSetup> { trackPosition, length, speedMultiplier }
```

**The game ships with 5 code-defined levels** (`DefaultLevels.cs`) so it runs with zero authored
assets, and **every one is provably solvable** (see the solvability guarantee in that file):

| # | Colors | Holes | Capacity | Time | Speed | Special | How built |
|---|---|---|---|---|---|---|---|
| 1 | 2 | Red×3, Blue×3 | 14 | 70s | 3.0 | — (tutorial) | auto-dealt, supply==demand |
| 2 | 3 | R/G/B ×3 | 14 | 65s | 3.4 | — | auto-dealt, supply==demand |
| 3 | 4 | R×4 B×4 G×3 Y×3 | 12 | 60s | 3.7 | **Hidden** color (Yellow) | auto-dealt, supply==demand |
| 4 | 4 | R×4 B×4 G×4 Y×3 | 11 | 58s | 3.9 | **Frozen** hole + **Lane barrier** | hand-authored, verified order |
| 5 | 5 | R/B/G/Y/P ×4 | 10 | 55s | 4.2 | **Gate** hole + **Arrow** zone + Hidden | hand-authored, verified order |

L1–L3 have an always-open hole for every color and exactly enough characters, so any push order
wins (lose only by timeout — good for learning). L4–L5 add locked mechanics: pushing a frozen/
gated color *before it unlocks* is how you jam and lose, so timing becomes the puzzle.

**To edit levels in the Inspector:** run the menu **`PeopleFlow ▸ Generate Sample Levels`**. It
writes `Assets/PeopleFlow/Levels/Level_01..05.asset`. Drag one onto a `GameBootstrap`'s **Override
Level** field to play it, or edit the assets and call your own loader.

**Pick a level for testing:** on `GameBootstrap`, set **Force Level Index** (0–4), or assign
**Override Level**. Leaving Force Level Index at **−1** uses `GameSession.CurrentLevelIndex` (what
the menu picked).

---

## 6. Controls & rules

- **Tap & hold** a lane pad → pushes the front character of that lane onto the runway. **Keep
  holding** to push more (one every `releaseInterval` ≈ 0.22s). Release to stop.
- Pushing is **blocked** while the runway is full or a runner is still sitting on the entry.
- A running character automatically **dives into the first matching, open, unlocked hole** it passes.
- **Win:** all holes filled. **Lose:** runway deadlocks (full, nobody can move) or the clock hits 0.
- Mouse = the pointer on PC; touch = the pointer on device (same code path).

---

## 7. Player Settings & build (Android primary, iOS, Editor)

**URP (do this first):** Project Settings ▸ **Graphics** → a *Render Pipeline Asset* must be
assigned (and a matching one under Quality). This is a URP project (com.unity.render-pipelines.
universal 17.4 is installed); if no asset is assigned, the runtime `URP/Lit` materials show as
magenta. Use an existing `UniversalRenderPipelineAsset` in the project or create one
(`Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)`).

**Input:** Project Settings ▸ Player ▸ **Active Input Handling = Both** (already set). The code
prefers the new Input System's `Pointer`, with a legacy fallback compiled behind `#if`.

**Player ▸ Resolution & Presentation:** Default Orientation = **Portrait** (and disable auto-rotate
to landscape). The bootstrap also forces `Screen.orientation = Portrait` at runtime.

**Player ▸ Other Settings (Android):**
- Package name e.g. `com.yourstudio.peopleflow`
- Scripting Backend **IL2CPP**, Target Architectures **ARM64** (required for Play Store)
- Minimum API Level 23+ (24+ recommended)
- Color Space **Linear** (URP best practice)

**Build:** Build Profiles ▸ Android (or iOS) ▸ ensure `MainMenu` (0) and `Game` (1) are in the
scene list ▸ Build. It also runs in the **Editor** via Play.

**Audio (optional):** all clips on `AudioManager` are `[SerializeField]` and default to none (the
game runs silent-but-fine). To add sound, put a `AudioManager` GameObject in the scene yourself
with your royalty-free clips assigned — the bootstrap will use the existing one instead of creating
a bare one. Same trick works for a pre-configured `GameManager`/`UIManager`/etc.

---

## 8. Special mechanics (§7 of the brief) — what's implemented & how to toggle

All are data-driven via `LevelData`, so you enable them per level with no code changes.

| Mechanic | Status | Script(s) | Toggle in LevelData |
|---|---|---|---|
| **Hidden color** | ✅ | `Hole`, `People` | `HoleSetup.hidden = true` (shows "?" until a runner passes near and `RevealIfHidden` fires) |
| **Frozen hole** | ✅ | `Hole` (+ `GameManager.OnHoleProgress`) | `HoleSetup.mechanic = Frozen`, `unlockAfterHolesCompleted = N` (ice dome until N other holes done) |
| **Hole gate** | ✅ | `Hole` | `HoleSetup.mechanic = Gate`, `unlockAfterHolesCompleted = N` (barred until N done) |
| **Lane barrier** | ✅ | `Lane` | `LaneSetup.barrier = true`, `unlockAfterHolesCompleted = N` |
| **Arrow / speed zone** | ✅ (bonus) | `RunwayTrack`, `People` | add an `ArrowSetup { trackPosition, length, speedMultiplier }` |

The "locked" mechanics share one signal — `GameManager.OnHoleProgress(completed, total)` — so a hole
or lane unlocks the moment enough *other* holes are finished.

---

## 9. Assumptions made (hyper-casual conventions)

1. **Single-pointer input.** You hold one lane at a time (no multi-touch). This also reads as a
   deliberate "make one fast decision at a time" constraint. Multi-touch is a noted next step.
2. **Lose-by-full = true deadlock**, not "bar hit 100%", so the game feels fair (see §1).
3. **Procedural everything** (primitives + code UI + `LegacyRuntime.ttf`) so there is literally
   nothing to import — no TMP Essentials, no art, no prefabs, no manual canvas layout.
4. **Holes are placed by a normalized `trackPosition` (0..1)** around an auto-generated oval rather
   than by hand in the scene — far easier to design and reason about.
5. **Win = holes filled, not lanes emptied.** Levels are built supply==demand (open levels) or
   hand-authored with a verified solution order (locked levels), so they're always winnable. A
   "surplus decoy color with no hole" (extra wrong-color tension) is a deliberate *next step*
   rather than shipped, because it can make a naive shuffle unsolvable — see §10.
6. Managers are **per-scene singletons** (no `DontDestroyOnLoad`); each level is a fresh scene load.

---

## 10. Next steps (remaining §7 mechanics + polish)

The open, data-driven architecture makes these incremental:

- **Key & lock** — add `keyColor` to `HoleSetup`; a runner carrying a matching "key" (a flag set
  when it passes a key pickup placed like a hole) satisfies `CanAccept`. Touches `Hole` + a new
  `KeyPickup` + `People`.
- **Moving obstacle on the runway** — a component that occupies a moving `trackPosition`; in
  `People.Update`, clamp forward progress when the next obstacle is too close
  (timing gates). Touches `RunwayTrack` (obstacle list) + the character.
- **Connected people** — spawn a short chain that shares one color and must enter the same hole
  together; reserve `chainLength` slots at once in `Hole.TryReserve`. Touches `Lane` (spawn) +
  `Hole`.
- **Two-way arrows / direction flips** — generalize `SpeedMultiplierAt` to also return a direction
  sign and let the character move backward through a zone.
- **Surplus / trap colors** (extra "push the wrong color = jam" tension) — add decoy characters of a
  color whose hole is already satisfied, but place them strictly at the **back** of lanes (after the
  required ones) so they remain avoidable and the level stays solvable. Touches `DefaultLevels` /
  your `LevelData` authoring only.
- **Polish:** assign real SFX/music on `AudioManager`; swap primitive bodies for low-poly prefabs
  (logic is already split from visuals); add star scoring on the win popup; persist per-level stars
  in `SaveManager`; add a level-select scroll for many levels.

---

## 11. Compile/verify note

These scripts were authored to compile cleanly into the default `Assembly-CSharp` and were
cross-checked by an automated multi-agent review (compile, cross-file contract, gameplay logic).
For a definitive check, open the project in Unity (it auto-compiles) and watch the Console, or run a
headless compile:

```bash
& "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" `
  -batchmode -nographics -quit -projectPath F:\people_flow_demo -logFile -
```

(Close the Editor first — it holds the project lock.)
