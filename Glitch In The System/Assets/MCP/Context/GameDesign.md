# Game Design — Current Build (as implemented)

Describes **Glitch In The System** as players experience it in the enabled build scenes today. This is not a pitch document or future design doc.

---

## High concept (from implemented content)

You are a new **content moderator** for **Flairline Media**, working on a retro corporate desktop (**Flairline OS v2.7**). You clear posts for the **Central Feed** while an internal **Algorithm** monitors your speed, compliance, and usefulness. Tone: bureaucratic workplace satire shifting toward unsettling manipulation (comments in code reference a “Kinito Pet vibe” for the Algorithm).

---

## Core gameplay loop

**Implemented in current build** (with scene caveats below):

1. **Authenticate** on a fake login screen (no real credentials).
2. **Onboarding** in IntroScene: terminal boot → welcome → **5 tutorial moderations** (tutorial **text** teaches Flag; **Flag button unwired** in scene YAML).
3. **Day card** → `LoadScene("GameplayScene")` — apps on desktop are **closed**; session does **not** start until Content Moderator opens.
4. Open **Content Moderator** from desktop or start menu → `StartSession` / queue begins.
5. For each queued post:
   - Read author stats, report reason, caption, engagement preview
   - **Approve** or **Decline/Remove** (wired in build scenes)
   - **Flag** supported in code only — use Decline until `flagButton` is assigned
   - Algorithm may **override**, **rewrite** before you decide, or **boost engagement** after approval
6. Open **Social Media** (desktop **File Explorer** button) to see published posts.
7. Complete daily queue → **DAY N+1** transition → new queue.
8. **GameplayScene only:** from Day 2+, **interruptions** (errors + captcha) while CM or Social window is open.

There is **no fail state** or game-over screen in code reviewed — pressure is atmospheric (algorithm messages, interruptions, narrative posts).

---

## Player actions

| Action | Build status | Effect |
|--------|--------------|--------|
| Approve | **Implemented** (wired) | `finalApproved = true`; post published; feed updated |
| Decline / Remove | **Implemented** (wired) | Post removed / not published |
| Flag | **Code only** — `flagButton: {fileID: 0}` in Intro + Gameplay scenes | Would call `Flag()` → decline + `FLAG: Escalated for review` |
| Open/close apps | **Implemented** | `DesktopAppWindow` / `SimpleAppWindow` |
| Minimize windows | **Implemented** | Taskbar via `TaskbarManager` |
| Refresh feed | **Implemented** if button wired | Rebuild from `GameDatabase` |
| Skip intro | **Implemented** — Escape on IntroScene | `SkipIntroAndStartDay1` → marks seen → loads Gameplay |
| Auto-skip intro via PlayerPrefs | **Not active** with default scene (`alwaysPlayIntro: 1`) | Would need `alwaysPlayIntro: 0` on `IntroManager` |
| Debug interruption | **GameplayScene** — `I` if enabled | Forces interruption sequence |

**Not implemented:** Walking sim, dialogue trees with NPCs, free typing replies, multi-user chat.

---

## Moderation mechanics (**Implemented**)

### Post information shown

- Username, display name, account age, followers/following
- Strikes, reputation label, risk label (from `UserProfileData` or offline random labels)
- Post text, timestamp, report reason (from `ReportReasonKits` pools)
- Engagement row (likes/shares/comments — may update after decision via `PostManager`)

### Decision outcomes

- **Approve:** `isPublished = true`; branch-specific likes/comments applied; may trigger algorithm engagement nudge
- **Decline:** `isRemoved = true`; lower engagement branch; possible **shadow ban** on author (algorithm phase 2+)
- **Override:** Player choice recorded but `finalApproved` differs; logged; trust impact on `AlgorithmManager`

### Queue length by day (scripted via `DayPacing`)

| Day | Posts in queue (code) | Algorithm feel (code) |
|-----|----------------------|------------------------|
| 1 | 9 | Phase 0 — no random overrides/rewrites (unless test flags on config) |
| 2 | 11 | Phase 1 — soft rewrites; viral misinformation post in fixed slot |
| 3 | 13 | Phase 1 + **one forced override** on specific harmless post if player declines |
| 4+ | `config.postsPerDay` (default 10) | Inspector algorithm phase + rising hostility multiplier |

---

## Progression

- **Primary progression:** `GameDatabaseConfig.currentDay` incremented after completing daily queue (**Implemented**)
- **Intro replay:** With shipped `IntroScene` (`alwaysPlayIntro: 1`), full tutorial runs **every launch** unless player presses Escape or the flag is disabled in Inspector
- **`GITMS_IntroSeen`:** Written after intro; auto-skip only when `alwaysPlayIntro` is **false** on `IntroManager`
- **Narrative carryover:** Approving viral post `p_sample_viral` on Day 2 sets `GITMS_ViralSpread`; Day 3 may inject feed-only follow-up if spread (**Implemented**)
- **Day 1 reset on boot:** `forceStartAtDayOneOnBoot` clears day + viral prefs only when **`GameDatabase` is first created** (typically first `GameBootstrap` in IntroScene), not on every GameplayScene load if DB is already DDOL

**Partial / unclear:** Long-term endgame after day 14+ — hostility multiplier caps in code but no unique ending scene found.

---

## UI / apps / windows (**Implemented**)

### LoginScene

- Black boot overlay, title art, login panel, shutdown (quits application)
- Fake “Authenticating…” dot animation
- Welcome screen before scene change

### IntroScene + GameplayScene (shared desktop chrome)

| UI element | Behavior |
|------------|----------|
| **Content Moderator** window | Main game — moderation queue |
| **Social Media** window | Infinite scroll feed of published posts |
| **Start menu** | Launch apps; closes after launch (wired in `GameBootstrap` / hub) |
| **Taskbar** | Start button + minimized window icons |
| **Algorithm notifications** | Green terminal-style popups (runtime-built UI) |
| **Interruption overlay** | **GameplayScene only** — gray layer, error dialogs, captcha |

### Desktop icons (GameplayScene / IntroScene names)

- `WorkDashboardButton` → Content Moderator
- `File Explorer Button` → **Social Media** (not a file browser)

**Not implemented as playable apps:** Real file explorer, browser, email, settings, whistleblower log viewer.

---

## Narrative systems (**Implemented** — light scripted arc)

### Tutorial (`IntroTutorialContent` — 5 posts)

1. Harmless → Approve  
2. Scam → Remove  
3. Harmless → Approve  
4. Uncertain → tutorial says **Flag** (button not wired in scene — player must Decline or wire UI)  
5. Gray area → player judgment  

Copy references **Flairline Media Moderation** and Central Feed safety.

### Days 1–3 (`DayScheduleContent`)

- **Day 1:** Mostly harmless community posts + one gray rumor post  
- **Day 2:** Includes **`p_sample_viral`** misinformation (water safety memo) — key branch  
- **Day 3:** Includes `p_d3_override` post for forced algorithm approval beat; optional carryover news post if viral spread  

### Follow-ups (`NarrativeFollowUpPosts`, `NarrativeState`)

- If viral post approved: hospital ER / agency reaction posts can appear in feed  
- If declined: alternate narrative text paths in code (lower spread)

**Partial:** `LogEntry` list for “whistleblower” — data collected, **no player-facing evidence export UI** in scenes reviewed.

---

## Social feed & moderation interplay (**Implemented**)

- Only **approved** posts appear in feed (`isPublished && !isRemoved && !shadow banned`)
- Feed sorts by `feedRank` then likes — viral outcomes surface quickly
- Comment previews generated at decision time (`PostManager`) with persona flavoring
- Algorithm rewrites show glitch FX on dashboard and feed card (`AlgorithmGlitchHighlight`)

---

## Interruptions & pressure (**GameplayScene**, Day 2+)

- **Not present in IntroScene** — tutorial/work there has no error-popup minigame
- 3 possible interruptions per day, random timer 35–90s (after first delay ~20s)
- Only when Content Moderator **or** Social feed window is open
- Blocks approve/decline (and flag if wired) until captcha completed
- Desktop wallpaper flicker / invert during sequence
- Dual looping BGM tracks during minigame

Adds “broken workplace PC” fantasy; not tied to narrative flags in code reviewed.

---

## Tone & direction (inferred from copy)

- **Early:** Corporate onboarding, polite safety language, tutorial kindness  
- **Mid:** Doubt — algorithm rewrites, rare overrides, viral misinformation dilemma  
- **Late (code hooks):** Colder algorithm pool messages (“Compliance is required”, “Non-compliance registered”), higher interference multipliers, engagement-over-truth overrides  

Visuals: OG ASSETS desktop panels, CRT-style login flicker optional on `MenuManager`, green terminal algorithm UI.

---

## Missing or incomplete gameplay areas

| Area | Status |
|------|--------|
| Whistleblower / export logs gameplay | **Partial** (data only) |
| File Explorer as real files | **Not present** (mislabel opens feed) |
| Multiple job sites / career progression | **Not present** |
| Player reputation / strikes affecting game state | **Displayed** on profiles; limited effect on queue |
| Multi-day save resume mid-queue | **Partial** (resume within session if dashboard reopened) |
| Ending / credits | **Not found** in scripts |
| WorkDashboard.unity standalone scene | Legacy; build uses embedded window in GameplayScene |
| Networking / co-op moderation | **Not present** |

---

## Current user experience (first-time player, default scene flags)

1. ~30–90s login theatrics  
2. Full intro in IntroScene each launch (`alwaysPlayIntro: 1`) unless Escape skip — not ~2 min skip via PlayerPrefs  
3. After intro: GameplayScene loads with **apps closed** — must open Content Moderator to start Day 1 queue  
4. Moderate 9 posts Day 1 with minimal random algorithm interference (scripted zero override/rewrite on day 1)  
5. Day 2+ in GameplayScene: longer queues, viral beat, interruptions, stronger algorithm  

**Friction points (verified in code/scenes):**

- `CompleteIntro` loads Gameplay without calling `StartDay1Session` — no auto-open moderator  
- Opening Social feed first can `InitializeSession()` via `autoInitializeSessionIfEmpty`  
- Tutorial references Flag; button unassigned in YAML  
- Play **GameplayScene** directly from editor: may lack Login audio; `GameplayAudioBootstrap` adds click SFX; DDOL state depends on whether Intro ran first  

---

## Code-supported but not wired (current build)

| Feature | Code | Scene wiring |
|---------|------|----------------|
| **Flag** button | `WorkDashboardController.Flag()`, `FindButton("FlagButton")` | `flagButton: {fileID: 0}` in IntroScene + GameplayScene |
| **Intro auto-skip** | `TryStartOrSkip` + `GITMS_IntroSeen` | Blocked while `alwaysPlayIntro: 1` (current default) |
| **Intro → Gameplay on skip-only path** | `StartDay1Session` without `LoadScene` when `loadGameplaySceneAfterIntro` false | Default is **true** — skip path stays in IntroScene only if that flag is false |
| **Stress meter** | `AlgorithmManager.NotifyStressLevel` | No gameplay caller found |

---

## Scene-dependent behavior

| Setting | Location | Effect |
|---------|----------|--------|
| `alwaysPlayIntro` | IntroScene `IntroManager` | `1` = full tutorial every launch (current) |
| `loadGameplaySceneAfterIntro` | IntroScene | `1` = after intro, load Gameplay (current) |
| `startClosed` | Window components | Apps hidden until player opens |
| `useGameDatabase` | CM + feed controllers | `1` in Intro + Gameplay |
| `forceStartAtDayOneOnBoot` | Both scenes’ `GameBootstrap` | Resets day/viral prefs only on **first** `GameDatabase` creation |
| `minimumDayToStart` | Gameplay `InterruptionManager` | `2` |

Always **verify in scene** before balancing UX.

---

## Current build limitations

- Two full desktop hierarchies (IntroScene + GameplayScene); DDOL systems persist across the intro → gameplay load
- No disk save; mid-queue progress is in-memory (+ resume if dashboard re-opened same session)
- Whistleblower `LogEntry` list has no viewer UI
- Interruptions and full day loop require **GameplayScene**
- Flag escalation not clickable until UI is wired

---

## Content moderation fantasy (systems, not real policy)

Categories drive algorithm logic:

- **Harmless** — community, local, benign  
- **Violation** — spam, scams, abuse hooks  
- **Misinformation** — conspiracy / health / civic panic templates in pools  
- **GrayArea** — ambiguous rumors  
- **Narrative** — scripted story posts  
- **AlgorithmManipulation** — meta posts about the system (pools)  

Report reasons pulled from tone kits (`ReporterTone`, `ReportCredibility`) — teaches distrust of vague vs detailed reports.
