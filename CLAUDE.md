# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**People Flow Demo** is a Unity 2D mobile game (v6000.4.10f1) being migrated from a legacy capybara/boss game to a crowd-runner gameplay loop (Count Masters style). The project is transitioning to a modular architecture where the new gameplay lives in the `PeopleFlow` namespace, and legacy code operates in the global namespace.

**Current state:** PeopleFlowGameController integration is scaffolded but incomplete; new crowd-runner mechanics need development.

## Architecture & Constraints

### Namespaces & Code Organization

- **Global namespace:** All legacy gameplay scripts (Boss, Capy, Bullet, BoxAmmo, etc.) — DO NOT change.
- **PeopleFlow namespace:** New crowd-runner module lives here (`Assets/Scripts/PeopleFlow/`). This is where new mechanics go.
- **Rule:** NO `_8Verse` namespace (removed entirely 2026-06-12). Host code calls INTO PeopleFlow, never the reverse.

### Integration Contract (PeopleFlow ↔ GamePlayController)

The host game state machine (GamePlayController.cs) owns level flow. PeopleFlow module reports outcomes via static events:

```csharp
public static event Action LevelFailed;        // Fire when crowd dies or player loses
public static event Action<int> LevelCompleted; // Fire with survivor count on win
```

Host calls:
- `PeopleFlowGameController.EnsureInstance().BeginLevel(level)`
- `.PauseGame()` / `.ResumeGame()` / `.QuitLevel()` (gated behind `m_usePeopleFlowPrototype = true`)

See GamePlayController.cs lines 178–179, 856 for bridge region.

### Project Conventions & Utilities

- **Private serialized fields:** Prefix with `m_` (e.g., `[SerializeField] float m_speed`).
- **Singleton base:** Use `Singleton<T>` (Assets/Scripts/Utilities/Singleton.cs). Its `OnDestroy` is `protected virtual` and nulls the cached instance — if you override it for teardown, you **must** call `base.OnDestroy()` or the singleton reference goes stale on scene reload.
- **Tweening:** No DOTween/Cinemachine installed. Use custom Tweener class (Assets/Scripts/Utilities/Tweener.cs) or code tweens inline (comments mark DOTween equivalents).
- **Input:** Input System active (activeInputHandler=2) alongside legacy input.
- **Cheat system:** USE_CHEAT define active on Android/Standalone. Callbacks already rewired to DebugWinLevel/DebugFailLevel in prototype mode.

### Project Structure

```
Assets/
├── Animations/
├── FBX/
├── Prefabs/
│   ├── Ingame/          (gameplay prefabs)
│   ├── ProjectAssets/
│   └── UI/
├── Resources/Sounds/
├── Scenes/              (Gameplay.unity, LevelDesign.unity, LoadScene.unity)
├── Scripts/
│   ├── Gameplay/        (Boss, Capy, Bullet, BoxAmmo, etc.)
│   ├── Level/           (LevelData, BossConfig, level editors)
│   ├── UI/              (Screens, Popups, Buttons)
│   ├── Utilities/       (Singleton, Tweener, GameLog, etc.)
│   └── PeopleFlow/      (↑ NEW MODULE — create if not present)
├── Sprites/
└── Settings/
```

## Development Workflow

### Headless Compile Check

```bash
& "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath F:\people_flow_demo `
  -logFile <path>
```

**Tags & Layers:** Ensure Person, PlayerAnchor, Obstacle, Enemy tags exist in TagManager before referencing them.

### App State Flow

`MainStateManager` (PersistenceSingleton) drives top-level state:

```
SplashScreen → Loading → MainMenu → Gameplay
```

Waits for `LoadingController.IsInitialized` before starting. `UIManager` listens and swaps screens accordingly.

### UI System

Base classes in `Assets/Scripts/UI/Base/`:
- `UIScreen` — full-screen views (e.g., `UIHome`, `UIInGame`); auto-hide when another screen is shown
- `UIPopup` — overlay dialogs (e.g., `UITutorial`, `UILevelFail`, `UILevelComplete`)

`UIManager` owns all screen/popup references as serialized fields.

### Player Persistence (PlayerPrefs keys)

Defined in `UserProfile.cs`:
- `UserCoin`, `UserLevel`
- `UserOnMusic`, `UserOnSFX`, `UserOnVibration`
- `UserUnlockItem`, `UserCrateItem`, `UserLongRangeItem`, `UserSortItem`, `UserGunOverloadItem`

### Key Files to Know

- **GamePlayController.cs** — Master state machine for level lifecycle; bridge to PeopleFlow at lines 178–950.
- **GameLog.cs** — Custom logging (use instead of Debug.Log for consistency).
- **Tweener.cs** — Custom animation library; study for juice without DOTween.
- **UIManager.cs** — Screen/Popup lifecycle management (Singleton).
- **UserProfile.cs** — Persistent game state (coins, level, player prefs).

### Testing & Debugging

- **USE_CHEAT enabled** on current build: Press cheat keys to win/lose levels (useful for iteration).
- **Scenes:** Use Gameplay.unity for play testing; LevelDesign.unity for level editing.
- **Console logs:** Check GameLog for structured output (filter by LogType).

## Common Gotchas

1. **Singleton.OnDestroy is `protected virtual`** — if you override it (e.g. to unsubscribe events), always call `base.OnDestroy()` first, or the instance unregister is skipped and the cached `m_instance` goes stale. (Shadowing it with a `private`/`new` method is the classic mistake — that silently drops the base teardown.)
2. **No DOTween/Cinemachine** — study Tweener.cs for animations; coded tweens are the pattern here.
3. **Global namespace collision** — if you import `_8Verse.*` by mistake, your scripts won't compile. Use PeopleFlow namespace for new code.
4. **m_usePeopleFlowPrototype flag** — many code paths still check this. New features should target the PeopleFlow path (true).
5. **Static events are decoupled** — PeopleFlow module doesn't know about GamePlayController; if you need to change the event contract, update both sides.

## Related Docs

- **Git:** See .git history for legacy removal (commit that stripped `_8Verse`).
- **Scenes:** Open Gameplay.unity to see current integration; load with Play button in UIHome.
- **User memory:** See ~/.claude/projects/f--people-flow-demo/memory/ for project context updates.
