# AI Behavior — Glitch In The System (actual implementation)

This project does **not** use machine-learning models, LLM APIs, or Unity ML-Agents for gameplay. All “AI” is **rule-based simulation**: scripted narrative, random rolls, trust scores, and string pools. This document maps those systems so agents do not confuse them with real AI services.

**Rule-based boundaries:** No learning, no LLM inference, no adaptive difficulty beyond configured curves and RNG. “Algorithm” is in-fiction policy/engagement logic implemented in C# — not a separate AI service.

---

## Summary

| Layer | Class(es) | Role | Status |
|-------|-----------|------|--------|
| **Algorithm Director** | `AlgorithmDirector` | Decision overrides, rewrites, shadow bans, engagement nudges | **Implemented** |
| **Algorithm Manager** | `AlgorithmManager` | Trust, behaviour mode, hesitation/stress, manipulation registry | **Implemented** |
| **Algorithm Voice** | `AlgorithmVoice` | Contextual notification strings | **Implemented** |
| **Day pacing** | `DayPacing`, `AlgorithmDayHostility` | Per-day phase and hostility scaling | **Implemented** |
| **Feed simulation** | `PostManager`, `OrganicEngagementUtility` | Comments/likes after decisions | **Implemented** |
| **Procedural users/posts** | `GameDatabase`, content pools | Synthetic social graph | **Implemented** |
| **NPCs / avatars** | — | No autonomous agents in world | **Not present** |
| **External AI API** | — | — | **Not present** |

---

## The “Algorithm” as antagonist (**Implemented**)

Fictional in-universe AI that enforces “engagement” and “policy” against player judgment.

### Phases (`AlgorithmDirector.Phase`)

| Phase | Name (comments) | Typical behaviour |
|-------|-----------------|-----------------|
| 0 | Helpful | No random overrides/rewrites on Day 1 (`DayPacing` zeros phase chances); hostility multiplier still applies to any non-zero test rolls |
| 1 | Authoritative | Rewrites; content-aware overrides possible on later days |
| 2 | Manipulative | Strong overrides, shadow bans, suppress “boring” approvals |

Phase is set by:

- `DayPacing.ApplyProfile` for days 1–3  
- `GameDatabaseConfig.algorithmPhase` + inspector defaults for day 4+

### Decision override pipeline

**Entry:** `WorkDashboardController.Decide` → `AlgorithmDirector.ProcessDecision(postId, authorId, playerApproved, post)`

**Steps:**

1. **Scripted beat:** `DayPacing.TryConsumeDay3ForcedOverride` — on Day 3, post `p_d3_override`, if player declines harmless content → force approve once  
2. Compute `overrideChance` from **director phase inspector values** (days 1–3) or from **`AlgorithmManager` profiles** when `UseManagerProfiles()` is true (**day 4+ only** — `!DayPacing.IsScriptedDay(day)`)  
3. Multiply by content rules (e.g. approved misinformation → higher override chance)  
4. Multiply by `AlgorithmDayHostility.GetInterferenceMultiplier(day)` (e.g. day 1 = 0.08× even when phase override chance is 0)  
5. Roll; if override, pick `algorithmApproved` via content-aware branches  
6. Log `LogEntryType.AlgorithmOverride`, show `AlgorithmVoice.OverrideApplied` via `AlgorithmNotification`

**Trust feedback:** `AlgorithmManager.OnModerationDecision` — disagreements reduce trust; compliance adds trust.

### Post rewrite pipeline

**Entry:** `WorkDashboardController.Next` → `AlgorithmDirector.TryRewritePost(post)`

- Rolls rewrite chance (phase + day hostility)  
- Phase 1: `SoftenOrHarden` string replacements (e.g. “censored” → “moderated”)  
- Phase 2: `ManipulateText` engagement-bait replacements  
- Sets `wasRewrittenByAlgorithm`, logs rewrite, notifies UI via `AlgorithmPostAlteredNotifier`

### Shadow ban

**Entry:** After decline → `TryShadowBanOnDecline(authorUserId)`

- Phase 2+ only; chance from phase or manager profile  
- Sets `UserProfileData.isShadowBanned`; hides author’s posts from `GetFeedPosts`

### Engagement nudge

**Entry:** After approval → `TryEngagementNudge(postId)`

- Adds random likes/shares/comments via `GameDatabase.NudgeEngagement`  
- Sets `algorithmEngagementManipulated` and `EngagementTier.ManipulatedRound`  
- Feed/dashboard glitch notification

---

## AlgorithmManager — trust & behaviour (**Implemented**)

Event-driven brain (explicitly **no `Update`**).

### Trust (0–100)

- Starts from `AlgorithmTrustSettings` asset or default 55  
- `AddTrust` / `ReduceTrust` on moderation outcomes, hesitation, stress, disagreements  
- Drives `AlgorithmBehaviourState`: Passive / Assertive / Aggressive via `AlgorithmStateProfile` assets or thresholds (`passiveTrustMin` 67, `assertiveTrustMin` 34)

### Player telemetry

| Signal | How recorded | Effect |
|--------|----------------|--------|
| **Hesitation (primary)** | `WorkDashboardAlgorithmUI.PatienceWatch` coroutine while post displayed | After `hesitationSeconds` (from `AlgorithmTrustSettings` or default 12s), calls `NotifyPlayerHesitation()` + `AlgorithmVoice.PatienceNudge` |
| Review duration | `BeginPostReview` / `EndPostReviewInternal` | Also checks hesitation on decision end (secondary path) |
| Override disagreement | Player vs final outcome | `disagreementCount++`, trust −4, resist message pool |
| Stress | `NotifyStressLevel` | **No gameplay callers found** — API only |

**Active player-facing hesitation:** `PatienceWatch` → `AlgorithmNotification` (not a hidden background system).

### Message pools (`AlgorithmMessageCategory`)

Built-in strings in `BuildDefaultMessagePools()`:

- `OnPlayerHesitation` — cold throughput copy  
- `OnPlayerResists` — override/disagreement copy  
- `OnPlayerComplies` — alignment praise  
- `OnStressHigh` — performance warnings  

Delivery gated by `AlgorithmStateProfile.messageDeliveryChance`.

### Post manipulation registry

- `RegisterPostManipulation` called from `OnModerationDecision` when **`finalApproved`** is true (approved outcomes only)  
- `ApplyDayEscalation` — feed rank, severity, category drift, fake rewrite suffix after day 8  
- Stored in `_manipulatedPosts` dictionary for inspection/debug

---

## AlgorithmVoice — procedural copy (**Implemented**)

Static helper in `Assets/Scripts/Algorithm/AlgorithmVoice.cs` (namespace `GlitchInTheSystem.Algorithm`).

Generates **variant strings** from:

- Post `category`, text snippets, phase  
- Player approve/decline vs algorithm outcome  
- Username hooks  

Used by:

- `AlgorithmNotification.Show`  
- Override/rewrite/shadow-ban/engagement messages  
- Optional `CommentOnPost` when post displayed  
- `DecisionFeedback` when player not overridden  

**Not** generative AI — fixed templates + `System.Random` index selection.

---

## UI presence systems (**Implemented**)

### `AlgorithmNotification`

- Runtime-built terminal panel (green on dark)  
- Singleton; survives via bootstrap adoption  
- Auto-hides after duration  

### `WorkDashboardAlgorithmUI`

- **`PatienceWatch` coroutine** (main hesitation path): polls every `patienceCheckInterval` (~2s); when elapsed ≥ `hesitationSeconds`, calls `AlgorithmManager.NotifyPlayerHesitation()` and `AlgorithmNotification.Show(AlgorithmVoice.PatienceNudge(...))`  
- Subtle visual bias: panel alpha, approve button scale on certain categories (day/trust/category gated)  
- `OnPostTextAltered` triggers `AlgorithmGlitchHighlight`  

### `AlgorithmVoice.PatienceNudge`

- Template strings for slow review — used by `PatienceWatch`, separate from `BuildDefaultMessagePools` hesitation lines

### `AlgorithmGlitchHighlight`

- Brief TMP color/offset glitch on rewritten posts  

---

## Day-based hostility (**Implemented**)

### `DayPacing` (days 1–3 scripting)

- Sets `postsPerDay` and **director** override/rewrite tables per day (`UseManagerProfiles` **off** on days 1–3)  
- Day 1: phase 0, override/rewrite chances set to **0** (unless `day1EnableAlgorithmTest` on config)  
- Day 3 forced override (single consume flag `_day3ForcedOverrideConsumed`) on post `p_d3_override`  
- Persists viral outcome to PlayerPrefs  

### `AlgorithmDayHostility` (all days)

Multiplier on override/rewrite rolls (applied even when base phase chance is 0):

```
Day 1: 0.08   Day 7: 1.0   Day 14+: up to ~1.15
```

With day 1 phase chances at 0, effective interference is still ~0 unless test flags raise phase chances. Day 1 test mode uses `config.day1TestHostilityMultiplier` when `day1EnableAlgorithmTest` is enabled.

---

## Social feed “AI” — simulated audience (**Implemented**)

Not agents; **reactive content generation** when player decides.

### `PostManager.ApplyDecisionReaction`

- Picks approve vs decline branch likes (`likesApprove` / `likesDecline`)  
- Builds `commentPreview` from `approveThread` / `declineThread` or legacy string lists  
- `GenerateContextualLines` — template expansion from post text intent (kindness, caution, etc.)  
- Persona flavors (Supportive, Skeptical, …) via seeded RNG  
- `RefreshEngagementLabel` — TRENDING / LOW ENGAGEMENT thresholds  

### `OrganicEngagementUtility`

- Coherent tier rolls (`EngagementTier`: Normal, Heated, Ignored, Viral, …)  
- Caps shares/comments relative to likes  

### `FeedManager`

- Ordering only — no autonomous posting except narrative injections from `GameDatabase` / `NarrativeFollowUpPosts`

---

## Content generation rules (**Implemented**)

### Scripted days 1–3

`DayScheduleContent` — fixed post IDs and copy; threads via `DayScheduleContentThreads`; report reasons via `ReportReasonKits`.

### Procedural days 4+

1. `GenerateUsersAndPosts` — **not** `DayScheduleContent`; uses `ModerationSamplePosts.Build` for initial slots, then `ModerationContentPools` / extended pools / optional library entries  
2. Viral template post is authored for **Day 2** via `DayScheduleContent` + `CreateViralMisinformationPost`, not re-injected daily on day 4+  
3. `ModerationContentValidator` (editor) can validate pool entries  

Categories and severity influence algorithm override weighting, not separate AI.

---

## Moderation “AI” scoring

**Player-facing stats** (strikes, reputation, risk on `UserProfileData`) are **display data** for judgment — no automated score that fires the player in code reviewed.

**Algorithm trust** is internal to AlgorithmManager — affects behaviour profiles and message rate, not directly shown as a meter on dashboard (unless bound in scene — not verified in code).

---

## Interruptions (related pressure system)

`InterruptionManager` is **not** narrative AI — timed workplace hazards (**GameplayScene only**):

- **`Update()`** loop for scheduling (not event-driven)  
- Spawns `ErrorPopup` clusters  
- `CaptchaMinigame` validation  
- Locks `WorkDashboardController`  

Uses random timers and day counters — no learning.

---

## State management

| State | Location | Lifetime |
|-------|----------|----------|
| Users/posts/queue/decisions | `GameDatabase` | Session; reset on `InitializeSession` |
| Narrative flags | `NarrativeState` | Session |
| Algorithm trust/manipulation | `AlgorithmManager` | DDOL; partial reset per day/session |
| Algorithm phase | `AlgorithmDirector` | Component; reconfigured by `DayPacing` |
| Intro seen / viral spread | PlayerPrefs | Cross-session |
| Current day | `GameDatabaseConfig.currentDay` | Config asset + runtime; reset only when `forceStartAtDayOneOnBoot` runs on **first** `GameDatabase` create |

---

## Planned vs implemented (honest)

### Implemented

- Full rule-based Algorithm antagonist with escalating interference  
- Trust/disagreement/hesitation loops  
- Rewrite + override + shadow ban + engagement boost  
- CMD narrative voice  
- Simulated comment/likes branches  
- Day 1–3 scripted manipulation beats + viral arc  
- Feed sync on algorithm edits  

### Partial / foundation only

- **Whistleblower logs:** `LogEntry` + `GameDatabase.Logs` — **no UI** to browse/export in gameplay scenes  
- **Stress system:** `NotifyStressLevel` API — **no clear producer** in gameplay loop found  
- **AlgorithmManager vs Director:** On days 1–3, director + `DayPacing` control chances; from day 4+, `UseManagerProfiles()` switches override/rewrite/shadow to manager profiles — tune both when changing late-game feel  
- **Evidence/meta ending:** Logs described as “evidence” in comments only  

### Not implemented

- LLM / ChatGPT / Unity Sentis inference  
- ML-Agents trained moderators  
- Simulated users posting on timers in feed  
- Personalization from player history beyond PlayerPrefs viral flag  
- AI difficulty settings menu  

---

## Data flow diagram (Algorithm path)

```
Player clicks Approve/Decline (Flag if wired in scene)
        │
        ▼
AlgorithmDirector.ProcessDecision ──► override? ──► AlgorithmNotification
        │                                      └──► GameDatabase.AddLog
        ▼
GameDatabase.RecordDecision
        │
        ├──► PostManager (comments/likes)
        ├──► NarrativeFollowUpPosts (if viral id)
        └──► DecisionRecorded event ──► SocialMediaFeedController.Refresh
        │
        ▼
AlgorithmManager.OnModerationDecision
        │
        ├──► trust / disagreement
        └──► RegisterPostManipulation (if approved)

Parallel at post display:
Next() ──► TryRewritePost ──► PostAltered event ──► Feed + Dashboard glitch
```

---

## For AI coding assistants

When asked to “add AI” to this project, clarify with the user:

- They almost certainly mean **Algorithm fiction** (rules, phases, copy pools), not external ML.  
- Extend `AlgorithmDirector` / `AlgorithmManager` / `DayPacing` / content pools — do not add OpenAI calls unless explicitly requested and product-approved.  
- Keep event-driven design for `AlgorithmManager`; use coroutines only in UI layer (`WorkDashboardAlgorithmUI`).  
