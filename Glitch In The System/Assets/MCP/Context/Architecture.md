# Architecture — Glitch In The System (actual implementation)

Technical map of the **current** Unity build. Status labels: **Implemented** | **Partial** | **Not present**.

---

## System overview

Single-player desktop simulation. All game state is **local, in-memory**, with small **PlayerPrefs** flags for intro completion and Day 2→3 narrative carryover. Two player-facing apps share one database:

| App (UI name) | Script / window | Purpose |
|---------------|-----------------|---------|
| **Content Moderator** | `WorkDashboardController` + `DesktopAppWindow` | Moderation queue, decisions, day transitions |
| **Social Media** (opened via “File Explorer” button) | `SocialMediaFeedController` + `SimpleAppWindow` | Public feed of approved/published posts |

**Implemented:** Core loop above.  
**Partial:** Evidence / whistleblower (`LogEntry` list exists; no dedicated viewer UI found).  
**Not present:** Networking, server backend, cloud save, real OS shell integration.

**Scene authority:** Player-facing behavior is defined by **C# plus serialized scene data** (`.unity` Inspector references). When they disagree, trust the scene YAML for wiring (buttons, flags, `startClosed`).

---

## Scene architecture

### Build order (`EditorBuildSettings`)

```
[0] LoginScene     → MenuManager (fake auth, load IntroScene)
[1] IntroScene     → IntroManager + full desktop + tutorial
[2] GameplayScene  → main session (no IntroManager on scene)
```

Legacy scenes (not in build): `FakeDesktopScene.unity`, `WorkDashboard.unity`.

### Scene contents (verified via scene YAML)

**LoginScene**

- `MenuManager` (`GlitchInTheSystem.Intro`)
- `GlobalClickAudio`, Input System UI module
- Flow: boot fade → login → welcome → `SceneManager.LoadScene("IntroScene")`

**IntroScene** (duplicate desktop stack — tutorial + optional full session here)

- `GameBootstrap` on `Algorithm` object (`forceStartAtDayOneOnBoot: 1`, `GameDatabaseConfig` asset assigned)
- `IntroManager` (`alwaysPlayIntro: 1` in current YAML — full tutorial every launch unless Escape skip or flag changed)
- `WorkDashboardController` (`useGameDatabase: 1`); `IntroManager.workDashboard` may be unassigned — falls back to `FindFirstObjectByType`
- `SocialMediaFeedController`, fake desktop (`FakeDesktop`), taskbar, start menu
- `loadGameplaySceneAfterIntro: 1`, `gameplaySceneName: GameplayScene`
- **No `InterruptionManager`** in this scene
- Tutorial uses `GameDatabase.InitializeIntroTutorialSession()`

**GameplayScene** (main loop after first intro completion)

- Parallel desktop hierarchy to IntroScene (without `IntroManager`)
- `GameBootstrap` (`forceStartAtDayOneOnBoot: 1`, `AlgorithmTrustSettings` asset assigned) — does not recreate DDOL `GameDatabase` if Intro already ran
- `InterruptionManager` only here (popups + captcha, `minimumDayToStart: 2`)
- Apps start **closed** (`startClosed: 1`) — **no moderation session on scene load**

### Scene transition diagram

```
LoginScene
   │ MenuManager.Login → LoadScene("IntroScene")
   ▼
IntroScene
   │ [if alwaysPlayIntro] full tutorial: boot → welcome → 5 posts → DAY 1 card
   │ CompleteIntro → LoadScene("GameplayScene")   ← does NOT call StartDay1Session when loadGameplaySceneAfterIntro is true
   ▼
GameplayScene
   │ Apps closed (startClosed: 1) — player must open Content Moderator
   ▼
WorkDashboardController.OnEnable → StartSession()
   │ (may InitializeSession, resume, or use existing queue — see StartSession logic)
```

**Intro skip paths (verify flags in scene):**

| Condition | Behavior |
|-----------|----------|
| `alwaysPlayIntro: 1` (current IntroScene default) | Full tutorial every launch; `GITMS_IntroSeen` does **not** auto-skip |
| `alwaysPlayIntro: 0` + `GITMS_IntroSeen` | `TryStartOrSkip` → `StartDay1Session()` in **IntroScene** only — **no** `LoadScene(GameplayScene)` |
| Escape during intro | `SkipIntroAndStartDay1` → `CompleteIntro` → loads Gameplay if `loadGameplaySceneAfterIntro` |

**Interruptions:** Only after reaching **GameplayScene** and day ≥ 2 with CM or Social window open.

---

## Runtime bootstrap & persistence

### `GameBootstrap` (scene component)

On `Awake`, ensures (via `RuntimePersistency.Adopt`):

| Object | Component |
|--------|-----------|
| GameDatabase | Data + session init |
| AlgorithmManager | Trust, behaviour, manipulation registry |
| AlgorithmDirector | Phase-based overrides/rewrites |
| AlgorithmNotification | CMD-style toasts |
| GameManager | Day number API |
| AlgorithmGlitchHighlight | Text glitch FX |
| StartMenuController | Added to same GO if missing |

Also calls `DesktopLauncherHub.EnsureInitialized()`.

**`forceStartAtDayOneOnBoot`:** When `GameDatabase.Instance == null`, sets `config.currentDay = 1` and clears `GITMS_ViralSpread`. Runs on **first** bootstrap (usually IntroScene). A second `GameBootstrap` in GameplayScene with an existing DDOL database **does not** apply this block again.

### `RuntimePersistency`

- Root: `GlitchRuntimeSystems` (`DontDestroyOnLoad`)
- Survives scene loads (Intro → Gameplay) for database/algorithm singletons

### Save / load

| Mechanism | Keys / data | Status |
|-----------|-------------|--------|
| PlayerPrefs | `GITMS_IntroSeen`, `GITMS_ViralSpread` | **Implemented** |
| In-memory | `GameDatabase` lists, `NarrativeState` per session | **Implemented** |
| Disk save game | — | **Not present** |

---

## Core data model

### `GameDatabase` (**Implemented** — central store)

In-memory lists:

- `_users`, `_posts`, `_moderationQueue`, `_decisions`, `_logs`
- `NarrativeState` for viral arc flags
- Event: `DecisionRecorded`

Key methods:

- `InitializeSession()` — full day reset + content generation
- `InitializeIntroTutorialSession()` — 5-post tutorial queue
- `GetNextModerationItem()` / `AdvanceQueue()` / `RecordDecision()`
- `GetFeedPosts()`, `RewritePost`, `ShadowBanUser`, `NudgeEngagement`

### `GameDatabaseConfig` (ScriptableObject)

- Asset: `Assets/Fake Desktop Folder/GameData/GameDatabaseConfig.asset` (referenced by Intro + Gameplay `GameBootstrap`)
- `currentDay`, `postsPerDay`, `algorithmPhase`
- Optional `moderationContentLibrary` (field exists; **not assigned** on current asset — procedural fill uses pools)
- Serialized category counts (`harmlessCount`, etc.) — **not read** by `GenerateUsersAndPosts` today
- Day 1 test toggles for algorithm (`day1EnableAlgorithmTest`, etc.)

### Models (`Assets/.../GameData/Models/`)

- `PostData`, `UserProfileData`, `CommentData`, `ModerationDecision`, `LogEntry`, `PostCommentLine`, enums for categories/format

---

## Moderation pipeline (**Implemented**)

```
WorkDashboardController.StartSession
    → GameDatabase.InitializeSession (or resume / tutorial queue)
WorkDashboardController.Next
    → GetNextModerationItem
    → AlgorithmManager.BeginPostReview
    → AlgorithmDirector.TryRewritePost
    → Render UI + AlgorithmVoice comments
WorkDashboardController.Decide
    → AlgorithmDirector.ProcessDecision (may override)
    → GameDatabase.RecordDecision
        → PostManager.ApplyDecisionReaction
        → NarrativeFollowUpPosts (viral arc)
    → AlgorithmManager.OnModerationDecision
    → TryEngagementNudge / TryShadowBanOnDecline
    → AdvanceQueue, Next
Queue complete → day transition coroutine → GameManager.AdvanceToNextDay → InitializeSession
```

**Partial:** `useGameDatabase = false` still generates random local personas/posts (not used in current scenes).

---

## Algorithm stack (**Implemented** — rule-based, not ML)

Two cooperating layers:

### `AlgorithmDirector` (phase 0–2)

- Inspector-tuned override/rewrite/shadow-ban curves per phase
- **Days 1–3:** `DayPacing.ApplyProfile` sets phase and override/rewrite tables; `UseManagerProfiles()` is **false** (`DayPacing.IsScriptedDay`) — director inspector values drive rolls, not `AlgorithmManager` profile chances
- **Day 4+:** `UseManagerProfiles()` true when `AlgorithmManager` exists — override/rewrite/shadow chances come from manager profiles; phase from `config.algorithmPhase` + `RestoreDefaultInterferenceFromInspector`
- `AlgorithmDayHostility` multiplier still applies on all days (e.g. day 1 = 0.08× on rolls even when phase chances are zeroed)
- Content-aware override multipliers in `ProcessDecision`
- `TryRewritePost`, `TryShadowBanOnDecline`, `TryEngagementNudge`
- Writes `LogEntry` records

### `AlgorithmManager` (trust 0–100, behaviour states)

- Passive / Assertive / Aggressive via `AlgorithmStateProfile` or trust thresholds
- Tracks review time, disagreements, stress
- Message pools (`AlgorithmMessageCategory`) → `AlgorithmNotification`
- `RegisterPostManipulation` / day escalation on approved posts
- **No Update loop**

### Supporting

- `AlgorithmVoice` — procedural message strings from post context
- `AlgorithmDayHostility` — day-based multiplier on interference rolls (day 4+)
- `AlgorithmPostAlteredNotifier` — static event for feed/dashboard glitch UI
- `WorkDashboardAlgorithmUI` — dimming, approve nudge, patience coroutines

---

## Social feed (**Implemented**)

```
SocialMediaFeedController
    → FeedManager.GetPublishedPostsForFeed(GameDatabase)
    → Clone postDesignTemplate per post
    → SocialMediaFeedCardBinder.Apply
    → Refresh on DecisionRecorded / PostAltered
```

- **`autoInitializeSessionIfEmpty`** (default true in GameplayScene): if feed opens while `GameDatabase.Posts` is empty, calls `InitializeSession()` — can start a real day queue before the player opens Content Moderator (verify ordering when changing boot flow).

Editor affordances: `SocialMediaFeedFreeformLayout`, `SocialMediaFeedEditorPost`, decor image watcher.

---

## Desktop / window shell (**Implemented**)

| Component | Role |
|-----------|------|
| `DesktopAppLocator` | Resolve CM / Social windows by `WindowId` or name |
| `DesktopLauncherHub` | Wires `WorkDashboardButton`, `File Explorer Button` → apps |
| `DesktopAppWindow` / `SimpleAppWindow` | Open/close/minimize; CM uses `DesktopAppWindow` |
| `DesktopHierarchy` | Activate window roots when opening |
| `DesktopUiStackOrder` | Focus / interruption z-order |
| `MinimizableWindow` | Minimize to `TaskbarManager` |
| `WindowAnimator` | Open/close animation |
| `DesktopWindowLayer` | Z-order / fullscreen under canvas |
| `StartMenuController` | Start menu toggle |
| `DesktopUiStackOrder` | Focus stacking |
| `DragPanel`, `WindowFocusOnClick` | Window chrome interaction |
| `DesktopTutorialScope` | Intro-only: hide social app |

`DesktopLaunchBootstrap` (`AfterSceneLoad`): on scenes whose name contains `"Gameplay"`, calls `DesktopLauncherHub.EnsureInitialized()`.

---

## Interruptions (**Implemented** — GameplayScene only, Day 2+)

`InterruptionManager` (component on **GameplayScene** only):

- Uses **`Update()`** for day checks, eligibility, and random trigger timing (not event-driven)
- Triggers when narrative day ≥ `minimumDayToStart` (2), up to `interruptionsPerDay` (3)
- Eligible when **Content Moderator or Social feed** window is active (`IsWorkDashboardOpen` || `IsSocialFeedOpen`) — field name `requireWorkDashboardOpen` is legacy; both apps qualify
- Sequence: loading spinner → error popups → captcha (`CaptchaMinigame` / `MinigameManager`)
- Locks moderation via `WorkDashboardController.SetModerationLocked`
- `InterruptionInputBlocker` on overlay; `InterruptionDesktopBackground` for wallpaper invert
- Optional runtime wiring via `InterruptionSceneBootstrap` (not required when manager is scene-placed)

Debug: `KeyCode.I` when `allowDebugTriggerKey`.

---

## Content generation

| Source | When | Status |
|--------|------|--------|
| `IntroTutorialContent` | Intro tutorial | **Implemented** (5 posts) |
| `DayScheduleContent` | Days 1–3 queues | **Implemented** (fixed order) |
| `ModerationSamplePosts` | Day 2 viral via `DayScheduleContent`; day 4+ uses `Build()` for first queue slots | **Implemented** |
| `ModerationContentPools` / `ModerationContentPoolsExtended` | Day 4+ remainder fill | **Implemented** |
| `ModerationContentLibrary` asset | Extra authored entries | **Partial** (optional SO) |
| `NarrativeFollowUpPosts` | After viral decision | **Implemented** |
| `DayPacing` | Posts/day, algorithm profile, PlayerPrefs carryover | **Implemented** |

---

## Event systems (actual)

| Mechanism | Producers | Consumers |
|-----------|-----------|-----------|
| C# event `GameDatabase.DecisionRecorded` | `RecordDecision` | `SocialMediaFeedController`, `IntroManager` |
| `AlgorithmPostAlteredNotifier.PostAltered` | Rewrites, nudges | Feed controller, Work Dashboard |
| `AlgorithmManager.BehaviourStateChanged` | Trust changes | **No subscribers** in codebase today |
| `InterruptionManager.Update` | Timer / eligibility | Starts interruption sequences |
| Unity UI `Button.onClick` | Scenes, `DesktopUIButtonWiring` | Launchers, decisions |
| Coroutines | Intro, interruptions, fades, `WorkDashboardAlgorithmUI` | Various |

No global event bus / ScriptableObject event channels. **Not** a fully event-driven architecture — scoped to Algorithm manager + C# events above.

---

## Prefab & scene relationships

- Primary wiring is **scene-based** (`GameplayScene`, `IntroScene`), not prefab-first
- `ErrorPopup` can be loaded as default prefab by `InterruptionManager` if unassigned
- Social post visual = scene template `EditorFeedPost_Template` cloned at runtime
- `GameDatabaseConfig` and `AlgorithmTrustSettings` referenced as assets on `GameBootstrap`

---

## Editor / tooling (non-runtime)

Under `Assets/Editor/`: `WorkDashboardBuilder`, `SocialMediaAppBuilder`, `CaptchaMinigamePanelBuilder`, `ModerationContentValidatorMenu`, `CreateGameDatabaseConfig`, etc. Used to construct or validate content — not loaded in player build logic.

---

## Backend / API integrations

**Not present.** No `UnityWebRequest`, HTTP clients, or backend URLs in gameplay scripts.

---

## Implementation status summary

### Fully implemented

- Login + intro tutorial + gameplay scene pipeline
- Shared `GameDatabase` moderation + feed
- Day 1–3 scripted pacing + viral narrative branch (PlayerPrefs carryover)
- Algorithm overrides, rewrites, shadow bans, engagement nudges
- Fake desktop windows, taskbar minimize, start menu
- Interruption minigame (GameplayScene, day 2+)
- Decision history UI on dashboard
- Algorithm CMD notifications + glitch highlights

### Partial

- Whistleblower / evidence: logs stored, **no dedicated UI**
- “File Explorer” branding vs social app behavior
- Day 4+ narrative: procedural posts + algorithm hostility scaling; less hand-authored story than days 1–3
- `GameDatabaseConfig` header counts (`harmlessCount`, etc.) — not all wired as sole generator vs pools
- Offline moderation mode on `WorkDashboardController` (code exists, scenes use DB)

### Not implemented (in codebase)

- Multiplayer / netcode
- Persistent save slots
- Real file explorer, email, browser, or other desktop apps beyond CM + Social
- External LLM / cloud moderation API
- Achievement / meta progression beyond day counter

---

## Text architecture diagram (runtime)

```
                    ┌─────────────────────┐
                    │   GameBootstrap     │
                    └──────────┬──────────┘
                               │
         ┌─────────────────────┼─────────────────────┐
         ▼                     ▼                     ▼
  ┌──────────────┐    ┌─────────────────┐   ┌──────────────────┐
  │ GameDatabase │◄───│ AlgorithmDirector│   │ AlgorithmManager │
  │  + Config    │    └────────┬────────┘   └────────┬─────────┘
  └──────┬───────┘             │                      │
         │                     └──────────┬───────────┘
         │                                ▼
         │                    ┌───────────────────────┐
         ├───────────────────►│ AlgorithmNotification │
         │                    └───────────────────────┘
         │
    ┌────┴────┐
    ▼         ▼
WorkDashboard  SocialMediaFeedController
Controller          │
    │               └── FeedManager
    │
    └── WorkDashboardAlgorithmUI

InterruptionManager ──locks──► WorkDashboardController
              └──► SocialMediaFeedController (eligibility)
```
