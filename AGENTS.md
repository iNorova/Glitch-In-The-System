# AGENTS.md

This repository is a Unity game project. The actual Unity project folder is:

`Glitch In The System/`

Use this file as the starting brief for AI agents and contributors, especially if you are new to Unity and want to contribute through code, content systems, tools, and validation before touching fragile scene layout.

## Project Status

- Repository: `iNorova/Glitch-In-The-System`
- Unity version: `6000.3.11f1`
- Enabled build scenes:
  - `Assets/Scenes/LoginScene.unity`
  - `Assets/Scenes/IntroScene.unity`
  - `Assets/Scenes/GameplayScene.unity`
- Main gameplay scene: `GameplayScene`
- The project currently has no README, so this file doubles as a contributor orientation guide.

## What The Game Is About

**Glitch In The System** is a fake-desktop social-media moderation game.

The player works inside a retro/glitchy operating-system interface for a platform called **Flairline Media Moderation**. The core job is to review reported social-media posts and decide whether to:

- `Approve` posts so they go live in the public feed.
- `Decline` / remove posts so they stay off the feed.
- `Flag` uncertain posts for escalation.

The twist is that the platform's **Algorithm** is not just a tool. It becomes an active presence that watches the player, comments on decisions, rewrites posts, boosts engagement, overrides moderation choices, and eventually interrupts the workflow with error popups and captcha-like minigames.

The theme is moderation under pressure: misinformation, engagement incentives, vague reports, false reports, platform opacity, automated decision-making, and how a system can slowly train a human operator to comply.

## Current Game Flow

1. `LoginScene`
   - Fake login screen.
   - Plays startup/login/shutdown UI transitions.
   - Loads `IntroScene`.

2. `IntroScene`
   - Boot/welcome onboarding.
   - Introduces the moderation dashboard with a short tutorial queue.
   - Teaches approve, remove, and flag behavior.
   - Loads `GameplayScene`.

3. `GameplayScene`
   - Fake desktop with launchable app windows.
   - Main apps:
     - Content Moderator dashboard.
     - Social Media feed.
   - Day-based moderation queues.
   - Algorithm messages and interference.
   - Random interruptions from Day 2 onward.

## Important Code Areas

Most gameplay code lives under:

`Glitch In The System/Assets/Fake Desktop Folder/Scripts/`

Key folders:

- `GameData/`
  - Runtime database, generated posts, authored content, narrative state, pacing, and models.
  - Best first stop if you are new to Unity because most changes here are plain C# data and rules.

- `WorkDashboard/`
  - Main moderation UI controller.
  - Shows account data, post data, reports, decisions, history, and day transitions.

- `SocialMedia/`
  - Public feed rendering.
  - Shows posts that were approved, boosted, rewritten, or affected by decisions.

- `Algorithm/`
  - Algorithm behavior, trust, overrides, rewrites, engagement boosts, shadow bans, and notification copy.

- `Interruptions/`
  - Error popups, loading spinner, captcha minigame, work locking, and interruption timing.

- `Intro/`
  - Login/menu and onboarding/tutorial flow.

- `Desktop/`
  - Fake desktop windows, start menu, app launching, taskbar, window focus, and layering.

Also note:

- `Glitch In The System/Assets/Scripts/Algorithm/AlgorithmVoice.cs`
  - Central copy generator for the Algorithm's voice and reactions.

## Key Gameplay Systems

### GameDatabase

`GameData/GameDatabase.cs`

This is the runtime source of truth for:

- users
- posts
- moderation queue
- decisions
- logs
- narrative flags

Most systems read from or write to this object.

### Day Pacing

`GameData/DayPacing.cs`

Days 1-3 are scripted:

- Day 1: gentle baseline, mostly harmless posts, no algorithm interference.
- Day 2: introduces doubt and the viral misinformation story.
- Day 3: introduces more direct algorithm manipulation and a forced override beat.

Day 4+ uses broader procedural/content-pool behavior and trust-based algorithm tuning.

### Scripted Day Content

`GameData/DayScheduleContent.cs`

Defines fixed queues for Days 1-3.

Important current story beat:

- Day 2 includes a viral misinformation post about a fake city water memo.
- If it goes live, Day 3 can show consequences in the social feed: ER visits, public panic, and accountability posts.

### Algorithm Director

`Algorithm/AlgorithmDirector.cs`

Handles direct algorithm actions:

- overriding decisions
- rewriting post text
- shadow banning users
- boosting engagement
- logging algorithm actions

### Algorithm Manager

`Algorithm/AlgorithmManager.cs`

Tracks the broader algorithm state:

- trust score
- behavior state: passive, assertive, aggressive
- player hesitation
- disagreement count
- stress signals
- day-based manipulation escalation

### Work Dashboard

`WorkDashboard/WorkDashboardController.cs`

This is the main moderation loop:

- pulls the next post
- displays user/profile/report data
- records approve/decline/flag decisions
- calls algorithm systems
- advances the queue
- transitions to the next day

### Social Feed

`SocialMedia/SocialMediaFeedController.cs`

Shows the visible public feed. Approved posts appear here, plus ambient filler posts. It also shows effects like rewritten content and engagement changes.

### Interruptions

`Interruptions/InterruptionManager.cs`

From Day 2 onward, the game can interrupt work sessions with:

- loading spinner
- desktop background flicker
- critical error popups
- captcha minigame
- locked moderation buttons until interruption is resolved

## Fast Contribution Areas

You do not need to be a Unity expert to contribute meaningfully. The safest path is to work on data, logic, tools, validation, and content systems before touching scene layout.

### Good First Contributions

1. Add more authored moderation content.
   - Files:
     - `GameData/ModerationContentPools.cs`
     - `GameData/ModerationContentPoolsExtended.cs`
   - Add realistic posts across:
     - harmless
     - violation
     - misinformation
     - gray area
     - narrative
     - algorithm manipulation

2. Add better report reasons.
   - File: `GameData/ReportReasonKits.cs`
   - Reports can be genuine, vague, bad-faith, opinion-based, or false.
   - This is a strong fit because it is mostly structured content and branching logic.

3. Expand comment/reaction threads.
   - Files:
     - `GameData/DayScheduleContentThreads.cs`
     - `GameData/PostManager.cs`
   - Make approve vs decline consequences feel more different.

4. Improve day-by-day narrative pacing.
   - Files:
     - `GameData/DayScheduleContent.cs`
     - `GameData/DayPacing.cs`
     - `GameData/NarrativeFollowUpPosts.cs`
   - Add more scripted story beats after Day 3.

5. Improve Algorithm voice lines.
   - File: `Assets/Scripts/Algorithm/AlgorithmVoice.cs`
   - Add more contextual responses for specific categories, trust states, and days.

### Systems And Tooling That Would Help The Game

These are high leverage because they make the game easier to author, verify, and expand:

- Content validation tool
  - Editor menu that scans all moderation entries for missing report reasons, empty comment branches, duplicate IDs, invalid categories, or impossible engagement values.

- JSON or CSV content import
  - Let writers add post pools from a spreadsheet or JSON file.
  - Convert imported content into ScriptableObject assets or generated C# data.

- End-of-day report screen
  - Summarize decisions:
    - approved count
    - declined count
    - flags
    - overrides
    - misinformation approved
    - harmless posts wrongly removed
    - algorithm trust movement

- Scoring/evaluation model
  - Add expected moderation outcomes per post.
  - Compare player choice vs. expected policy choice.
  - Make algorithm overrides and gray areas affect score differently.

- Save/load progression
  - Current logic uses runtime state and some `PlayerPrefs`.
  - A small save system could persist day, decisions, narrative flags, trust, and unlocked events.

- Debug/dev dashboard
  - In-editor or in-game panel to jump to Day 1/2/3/4, force interruptions, reset PlayerPrefs, spawn viral outcome, or inspect Algorithm trust.

- EditMode tests for pure logic
  - Useful targets:
    - `DayPacing`
    - `FeedManager`
    - `OrganicEngagementUtility`
    - `ReportReasonKits`
    - `ModerationDecisionFeedback`

## Suggested First Implementation Plan

If you are downloading Unity now, start with this sequence:

1. Open the Unity project folder:
   - `Glitch In The System/`

2. Install/use Unity:
   - `6000.3.11f1`
   - If Unity Hub suggests a different Unity 6 patch, prefer the exact version when possible.

3. Open `LoginScene` and press Play.
   - Confirm the login flow reaches intro/tutorial/gameplay.

4. Then inspect `GameplayScene`.
   - Find the Content Moderator app.
   - Find the Social Media app.
   - Watch how decisions affect the feed.

5. Make a small code-only contribution first.
   - Best starting task: add 10-20 new moderation entries to `ModerationContentPoolsExtended.cs`.
   - Then verify they can appear in Day 4+ procedural queues.

6. After that, implement a validation/editor tool.
   - This gives immediate value to the team and does not require deep scene/UI knowledge.

## AI-Assisted Workflow For This Repo

When asking an AI agent to work here, be specific about the target area:

- "Add 20 new post entries to `ModerationContentPoolsExtended.cs`."
- "Add validation for duplicate moderation IDs."
- "Create an end-of-day report data model but do not build UI yet."
- "Add EditMode tests for `FeedManager` sorting."
- "Add Day 4 scripted content using the existing Day 1-3 pattern."

Ask the AI to avoid scene edits unless explicitly requested. Unity scene YAML is fragile and easy to break outside the editor.

Preferred AI task shape:

1. Read the relevant scripts.
2. Explain the existing pattern.
3. Make a small scoped change.
4. Run static checks if possible.
5. Tell you exactly what to test in Unity.

## Installed / Recommended Skills

These global skills were installed from `https://skills.sh/` for this project:

- `unity-developer`
  - Source: `rmyndharis/antigravity-skills@unity-developer`
  - Use for Unity architecture, C# gameplay scripts, Unity Test Framework strategy, editor tooling, performance, URP, scene-safe implementation plans, and build/debug guidance.
  - Good prompt: "Use `unity-developer` and add an editor validation tool for moderation content."

- `game-designer`
  - Source: `dylantarre/animation-principles@game-designer`
  - Use for game feel, UI feedback, animation timing, glitch feedback, button responses, error-popup tension, and making moderation actions feel satisfying and readable.
  - Good prompt: "Use `game-designer` to improve the feel of approve/decline/flag feedback without changing game balance."

Already available skills that are also useful here:

- `doc-coauthoring`
  - Use for README, GDD, contribution docs, specs, and feature proposals.

- `humanizer`
  - Use for making in-game copy, report text, and Algorithm voice lines feel less generic.

- `deep-research` / `research-orchestrator`
  - Use for researching content moderation patterns, misinformation mechanics, and comparable games.

- `visual-design-foundations`
  - Use for evaluating fake-desktop UI hierarchy, color, typography, and readability.

Skills intentionally not installed yet:

- `ityes22/game-design-document@game-design-document`
  - Potentially useful for a full GDD, but weaker trust signals than the Unity skill. Install only if the team wants a publisher-style game design document workflow.

- Unity test-runner / compile-fixer community skills
  - Install later if needed after Unity is available locally. Current install counts and trust signals were low.

## Code Style Notes

- Follow existing C# style.
- Keep gameplay logic in the existing folders rather than creating new top-level systems.
- Prefer small, explicit data classes and static helpers where the project already uses them.
- Keep UI scene wiring in Unity when possible.
- Avoid broad refactors until the game flow is stable.
- Do not rename serialized fields casually. Unity scenes and assets may reference them.
- Do not move scripts between folders casually. Unity `.meta` GUIDs matter.

## Unity Safety Rules

Do not commit generated local folders such as:

- `Library/`
- `Temp/`
- `Obj/`
- `Logs/`
- `UserSettings/`

Be careful with:

- `.unity` scene files
- `.prefab` files
- `.asset` files
- `.meta` files

If a `.meta` file changes because you intentionally added/moved/deleted an asset, keep it with the asset change. If it changes unexpectedly, inspect before committing.

## What To Test Manually In Unity

After gameplay changes, test:

1. Login scene loads intro.
2. Intro tutorial can complete.
3. Gameplay scene starts Day 1.
4. Content Moderator shows a queue item.
5. Approve publishes the post to the Social Media feed.
6. Decline keeps the post out of the feed.
7. Day 2 viral post creates the correct consequence path.
8. Day 3 forced override can trigger.
9. Interruption sequence does not permanently lock moderation.
10. Closing/opening fake desktop windows does not lose the active session.

## High-Value Next Tasks

Recommended next contribution order:

1. Add content pool entries.
2. Add a content validation editor menu.
3. Add end-of-day summary data.
4. Add tests for feed ordering and day pacing.
5. Add Day 4 narrative beats.
6. Add a simple save/load model.
7. Only then start deeper UI scene work.

## Mental Model

If you want a code-first mental model, treat the game like a stateful app with a UI on top:

- `GameDatabase` is the runtime database.
- `PostData`, `UserProfileData`, and `ModerationDecision` are domain models.
- `DayScheduleContent` and `ModerationContentPools` are seed/content data.
- `WorkDashboardController` is the main command handler.
- `SocialMediaFeedController` is a read model / projection of approved content.
- `AlgorithmDirector` is an automated policy/interference service.
- `InterruptionManager` is a scheduler plus modal lock system.

That framing makes the project much less intimidating. You can contribute real gameplay value by improving the data and rules before touching complex Unity UI.
