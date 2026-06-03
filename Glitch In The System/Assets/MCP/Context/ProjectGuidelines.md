# Project Guidelines — Glitch In The System

Evidence-based guide for AI assistants working in this Unity 6 project (`6000.3.11f1`). Describes **what exists today**, not roadmap fiction.

## Project overview

**Glitch In The System** is a single-player, offline **fake-OS desktop sim** about working as a content moderator for **Flairline Media**. Core loop: review a queue of social posts (approve / remove / flag), see outcomes on a **Central Feed** social app, and survive escalating **Algorithm** interference plus **work-session interruptions** (error popups + captcha minigame).

- **Engine:** Unity `6000.3.11f1`, URP (`com.unity.render-pipelines.universal`)
- **UI:** uGUI + TextMesh Pro
- **Input:** New Input System (`com.unity.inputsystem`) in login/intro scenes; legacy `KeyCode` still used in some interruption debug paths
- **MCP:** `com.anklebreaker.unity-mcp` (AB Unity MCP plugin)
- **Networking:** None implemented (no multiplayer, no HTTP game API)

## Build & scene flow (authoritative)

`EditorBuildSettings` order:

1. `Assets/Scenes/LoginScene.unity` — fake login (`MenuManager`)
2. `Assets/Scenes/IntroScene.unity` — onboarding + tutorial (`IntroManager`)
3. `Assets/Scenes/GameplayScene.unity` — main loop (fake desktop, moderation, feed, interruptions)

**Disabled in build:** `Assets/Fake Desktop Folder/FakeDesktopScene.unity`, `Assets/Workdashboard Folder/WorkDashboard.unity` (legacy / editor tooling scenes).

**Player flow (first launch, default scene flags):** LoginScene → IntroScene (full tutorial when `alwaysPlayIntro` is true) → `LoadScene("GameplayScene")` → player opens **Content Moderator** (`startClosed: 1`) → `OnEnable` → `StartSession`. **Verify in scene:** `IntroScene` currently has `alwaysPlayIntro: 1`, so `GITMS_IntroSeen` does not auto-skip the tutorial unless that flag is turned off.

## Folder organization (game code)

| Path | Role |
|------|------|
| `Assets/Fake Desktop Folder/Scripts/` | **Primary gameplay code** |
| `Assets/Fake Desktop Folder/Scripts/GameData/` | `GameDatabase`, pacing, content pools, models |
| `Assets/Fake Desktop Folder/Scripts/Algorithm/` | Algorithm director, trust, manipulation |
| `Assets/Fake Desktop Folder/Scripts/SocialMedia/` | Feed layout, card binding, `FeedManager` |
| `Assets/Fake Desktop Folder/Scripts/WorkDashboard/` | Moderation UI controller + algorithm presence |
| `Assets/Fake Desktop Folder/Scripts/Desktop/` | Windows, taskbar, launchers, start menu |
| `Assets/Fake Desktop Folder/Scripts/Interruptions/` | Captcha / error popup sequences |
| `Assets/Fake Desktop Folder/Scripts/Intro/` | Login + intro flows |
| `Assets/Fake Desktop Folder/Scripts/UI/` | Notifications, fades, audio, drag |
| `Assets/Scripts/Algorithm/` | `AlgorithmVoice` (copy helper; outside Fake Desktop Folder) |
| `Assets/Fake Desktop Folder/GameData/` | `GameDatabaseConfig.asset` (serialized config referenced by scenes) |
| `Assets/Scenes/` | Login, Intro, Gameplay |
| `Assets/Editor/` | Scene builders, validators (not runtime) |
| `Assets/MCP/Context/` | AI context docs (this folder) |
| `Assets/TextMesh Pro/Examples & Extras/` | **Third-party samples — do not treat as game systems** |

## Coding conventions observed

### Namespaces (mixed — match nearby files)

- **Namespaced:** `GlitchInTheSystem.GameData`, `.Algorithm`, `.Social`, `.UI`, `.Intro`, `.Interruptions`
- **Global namespace (legacy / scene-wired):** `WorkDashboardController`, `SocialMediaFeedController`, `TaskbarManager`, `DesktopAppLauncher`, `DesktopLauncherHub`, most `Desktop/*` window scripts

When adding code, prefer `GlitchInTheSystem.*` namespaces for new types; if editing an existing global class, stay global unless doing a deliberate refactor.

### Patterns

- **Singletons** via static `Instance` on: `GameDatabase`, `GameManager`, `AlgorithmDirector`, `AlgorithmManager`, `AlgorithmNotification`, `TaskbarManager` (scene-scoped)
- **Persistent root:** `RuntimePersistency` → `GlitchRuntimeSystems` (`DontDestroyOnLoad`); children adopted via `RuntimePersistency.Adopt`
- **Bootstrap:** `GameBootstrap` spawns DDOL systems in `Awake` if missing
- **Event-driven Algorithm subsystem:** `AlgorithmManager` has **no `Update`**; uses public methods + coroutines in `WorkDashboardAlgorithmUI`. This does **not** apply project-wide (`InterruptionManager` uses `Update()` for timers)
- **Data authority:** `GameDatabase` is the single source of truth for users, posts, queue, decisions, logs
- **Inspector wiring:** Heavy use of `[SerializeField]`, `AutoBindByName()`, and scene object names (`WorkDashboardButton`, `File Explorer Button`)

### Content & data

- Posts are `PostData` with categories: `Harmless`, `Violation`, `Misinformation`, `GrayArea`, `Narrative`, `AlgorithmManipulation`
- Days **1–3:** fixed queues in `DayScheduleContent` (no shuffle)
- Day **4+:** procedural mix via `ModerationSamplePosts`, `ModerationContentPools`, optional `ModerationContentLibrary` asset
- Narrative arc IDs in `NarrativeIds` (viral misinformation `p_sample_viral`)

## Architecture constraints (do not break casually)

1. **`GameDatabase.InitializeSession()`** resets queue, applies `DayPacing`, resets algorithm day/session state — call path matters for day transitions.
2. **`WorkDashboardController.Decide()`** pipeline: `AlgorithmDirector.ProcessDecision` → `RecordDecision` → `AlgorithmManager.OnModerationDecision` → engagement nudge / shadow ban → UI history.
3. **`IntroManager`** must initialize tutorial via `InitializeIntroTutorialSession()` **before** opening Work Dashboard; uses `SuppressAutoStartSessionOnNextEnable()` to avoid double `StartSession`.
4. **`DesktopTutorialScope`** blocks Social app during intro; `File Explorer Button` actually opens **Social Media** (`DesktopLauncherHub.OpenSocialMedia`).
5. **`GameBootstrap.forceStartAtDayOneOnBoot`** resets `currentDay` and `GITMS_ViralSpread` **only when `GameDatabase.Instance == null`** (first `GameBootstrap` that creates the DB — typically IntroScene). If Intro already created a DDOL `GameDatabase`, GameplayScene’s `GameBootstrap` does **not** re-run that reset.
6. **Feed refresh** listens to `GameDatabase.DecisionRecorded` and `AlgorithmPostAlteredNotifier` — breaking events desyncs feed UI.
7. **`SocialMediaFeedController.autoInitializeSessionIfEmpty`** — opening the feed with an empty DB can call `InitializeSession()` before the moderator; mind tutorial/day ordering.

## System dependencies (high level)

```
LoginScene (MenuManager)
    → IntroScene (IntroManager + GameBootstrap + WorkDashboard + Social feed)
        → GameplayScene (GameBootstrap + desktop + WorkDashboard + Social + InterruptionManager)

GameBootstrap
    → GameDatabase, GameManager, AlgorithmDirector, AlgorithmManager, AlgorithmNotification, AlgorithmGlitchHighlight, StartMenuController

WorkDashboardController (useGameDatabase=true in scenes)
    ↔ GameDatabase, AlgorithmDirector, AlgorithmManager, AlgorithmNotification
    → SocialMediaFeedController (via published posts)
    ← InterruptionManager (locks moderation)

SocialMediaFeedController
    ↔ GameDatabase, FeedManager, SocialMediaFeedCardBinder
```

## UI consistency rules

- Fake desktop resolution assumptions: **1920×1080** overlays (day transition, interruptions)
- Terminal-green **Algorithm** messages via `AlgorithmNotification` (`> ` prefix style in copy)
- Work Dashboard: TMP labels, Approve / Decline; **Flag** exists in code (`WorkDashboardController.Flag`) but **`flagButton` is unwired** in current Intro/Gameplay scene YAML — verify after UI edits
- Social feed: clones `EditorFeedPost_Template` at runtime; cards named `FeedCard_{postId}`
- Window chrome: `MinimizableWindow` + `WindowAnimator` + `TaskbarManager` for minimized apps
- **Naming quirk:** UI label **File Explorer** launches **Social Media** app — do not “fix” without design intent

## Desktop / shell helpers (runtime)

| Component | Role |
|-----------|------|
| `DesktopAppLocator` | Finds windows by `MinimizableWindow.WindowId` or name substring |
| `DesktopHierarchy` | Ensures window roots active when opening |
| `DesktopUiStackOrder` | Z-order / interruption blocking for app shells |
| `InterruptionInputBlocker` | Blocks pointer input on interruption overlay |
| `GameplayAudioBootstrap` | Spawns `GlobalClickAudio` when entering **Gameplay** without LoginScene |

## Serialized scene authority

Treat **`.unity` YAML / Inspector references** as equal authority to C#:

- Button bindings (`flagButton`, `workDashboard` on `IntroManager`, etc.)
- `alwaysPlayIntro`, `loadGameplaySceneAfterIntro`, `forceStartAtDayOneOnBoot`
- `startClosed` on window components

Grep scenes under `Assets/Scenes/` before claiming player-facing behavior.

## Refactoring cautions

- Do not rename scene objects used by `AutoBindByName()` / `Find("...")` without updating all binders (`WorkDashboardController`, `DesktopLauncherHub`, `DesktopTutorialScope`).
- Preserve **`MinimizableWindow.WindowId`** values (`ContentModerator`, `SocialMedia`) — `DesktopAppLocator` depends on them.
- Prefer extending **`DayScheduleContent`** / **`ModerationContentPools`** over new manager singletons.
- `WorkDashboardController` still supports **`useGameDatabase = false`** offline random mode — scenes currently use `true`; removing offline path is a product decision.
- `AlgorithmDirector` and `AlgorithmManager` both influence interference; `AlgorithmDirector` uses phase + day pacing; `AlgorithmManager` adds trust/behaviour profiles — keep responsibilities aligned with comments in code.
- Editor scripts (`WorkDashboardBuilder`, `SocialMediaAppBuilder`, etc.) recreate UI — runtime code may assume hierarchy paths.

## Performance considerations

- Feed rebuild uses `FeedManager.BuildSignature` to skip full UI rebuild when unchanged
- Social feed can refresh on scroll / timer (`autoRefreshSeconds`); prefer signature-based updates when touching feed code
- Interruption sequences spawn multiple `ErrorPopup` instances with separation checks — avoid per-frame allocation in hot paths
- Procedural content generation runs at **session start**, not per frame

## Rules for modifying gameplay systems

| Change type | Touch |
|-------------|--------|
| New post types / categories | `PostData`, pools, `PostManager`, possibly `AlgorithmDirector` content rules |
| New day scripting | `DayScheduleContent`, `DayPacing`, `GameDatabase.GenerateUsersAndPosts` |
| Algorithm behaviour | `AlgorithmDirector`, `AlgorithmManager`, `AlgorithmTrustSettings` asset, `DayPacing` |
| Moderation UI | `WorkDashboardController`, `WorkDashboardAlgorithmUI`, scene hierarchy |
| Feed presentation | `SocialMediaFeedController`, `SocialMediaFeedCardBinder`, template prefab/scene object |
| Onboarding | `IntroManager`, `IntroTutorialContent`, `MenuManager` |
| Desktop apps | `DesktopAppWindow` / `SimpleAppWindow`, `DesktopLauncherHub`, scene wiring |

## Naming conventions (observed)

| Element | Convention | Examples |
|---------|------------|----------|
| Classes | PascalCase | `GameDatabase`, `AlgorithmDirector` |
| Private fields | `_camelCase` in newer code | `_currentDbPost`, `_moderationLocked` |
| PlayerPrefs keys | `GITMS_*` | `GITMS_IntroSeen`, `GITMS_ViralSpread` |
| Post IDs | prefixes | `p_d1_01`, `intro_p_01`, `p_sample_viral` |
| Window IDs | PascalCase strings | `ContentModerator`, `SocialMedia` |
| Events | PascalCase | `DecisionRecorded`, `PostAltered` |

---

## What future AI assistants SHOULD do

- Read `GameDatabase`, `WorkDashboardController.Decide`, and `DayPacing` before changing moderation or narrative pacing.
- **Verify scene Inspector values** (`IntroScene.unity`, `GameplayScene.unity`) before gameplay/UX assumptions — especially intro flags and button references.
- Keep all post/user mutations going through `GameDatabase` when `useGameDatabase` is true.
- Preserve intro/tutorial ordering: tutorial queue init → open dashboard → `StartSession`.
- Match existing namespace and binding patterns in the file you edit.
- Run / respect `ModerationContentValidator` editor tooling when changing content pools.
- **After UI edits:** confirm Approve / Decline / Flag button references in **both** Intro and Gameplay scenes.
- Preserve prefab and serialized references; do not “fix” wiring by bypassing scene objects without checking YAML.
- Label incomplete systems as partial if extending whistleblower logs, File Explorer fiction, or day 4+ narrative.
- Prefer **extending** existing pools and controllers over replacing working systems.

## What future AI assistants SHOULD NOT do

- Invent multiplayer, cloud saves, real social APIs, or ML moderation — **not in repo**.
- Assume `File Explorer` is a file browser — it opens the social feed.
- Assume **PlayerPrefs intro skip** works without checking `IntroManager.alwaysPlayIntro` on the scene.
- Assume **`forceStartAtDayOneOnBoot`** resets day/viral prefs on every Gameplay load — it only runs when creating the first `GameDatabase`.
- Assume **Flag** is clickable because `Flag()` exists in code — verify `flagButton` is assigned in scene YAML.
- Add `Update()` loops to `AlgorithmManager` (explicitly event-driven).
- Break `RuntimePersistency` / duplicate DDOL singletons without handling `Instance` guards.
- Treat TMP example scenes or `_Recovery` scenes as canonical gameplay.
- Commit secrets or change `manifest.json` Unity packages without user request.
- Perform major refactors of `AlgorithmDirector` / `AlgorithmManager` split without user approval.
