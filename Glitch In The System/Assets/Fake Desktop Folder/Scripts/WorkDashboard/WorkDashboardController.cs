using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.Social;
using GlitchInTheSystem.UI;
using GlitchInTheSystem.Interruptions;

public sealed class WorkDashboardController : MonoBehaviour
{
    [Header("Data source")]
    [Tooltip("If true, pulls posts from GameDatabase and runs through AlgorithmDirector. Requires GameDatabase + AlgorithmDirector in scene.")]
    [SerializeField] private bool useGameDatabase = false;

    /// <summary>
    /// Intro/tutorial and boot flows can force the data source without exposing the field publicly.
    /// </summary>
    public void SetUseGameDatabase(bool useDb)
    {
        useGameDatabase = useDb;
    }

    /// <summary>Skip the automatic <see cref="StartSession"/> from the next <see cref="OnEnable"/> once (intro flow).</summary>
    public void SuppressAutoStartSessionOnNextEnable()
    {
        _suppressStartSessionOnce = true;
    }

    [Header("Top bar")]
    [SerializeField] private TMP_Text dayInfoText; // ex: "Day 1 — Posts: 0/10"

    [Header("Profile (TextMeshProUGUI)")]
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text accountAgeText;
    [SerializeField] private TMP_Text followersText;
    [SerializeField] private TMP_Text followingText;
    [SerializeField] private TMP_Text strikesText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text riskText;

    [Header("Post (TextMeshProUGUI)")]
    [SerializeField] private TMP_Text queueText;
    [SerializeField] private TMP_Text postUserText;
    [SerializeField] private TMP_Text timestampText;
    [SerializeField] private TMP_Text postText;
    [SerializeField] private TMP_Text reportReasonText;
    [SerializeField] private TMP_Text engagementRowText;

    [Header("Decision")]
    [SerializeField] private Button approveButton;
    [SerializeField] private Button declineButton;
    [Tooltip("Optional third action. If present, Flag maps to decline + an escalation reason (used by the intro/tutorial).")]
    [SerializeField] private Button flagButton;
    [SerializeField] private TMP_Text decisionResultText;

    [Header("Day transition")]
    [SerializeField] private RectTransform dayTransitionPanel;
    [SerializeField] private TMP_Text dayTransitionText;
    [SerializeField] private Button dayTransitionProceedButton;
    [SerializeField] private float dayShiftFadeSeconds = 0.55f;
    [SerializeField] private float dayShiftHoldSeconds = 2.1f;

    private const float DayTransitionWidth = 1920f;
    private const float DayTransitionHeight = 1080f;

    [Header("Decision history")]
    [Tooltip("Content transform under a ScrollRect where entries will be appended.")]
    [SerializeField] private RectTransform decisionHistoryContent;
    [SerializeField] private ScrollRect decisionHistoryScrollRect;

    [Header("Config")]
    [SerializeField] private int postsPerSession = 10;
    [SerializeField] private int dayNumber = 1;
    [Tooltip("If set, controller will bump TMP font sizes by this multiplier at runtime.")]
    [SerializeField] private float fontSizeMultiplier = 1.1f;

    private int currentIndex = 0;
    private Person currentPerson;
    private Post currentPost;

    // When using GameDatabase, we hold refs to the actual data for recording
    private UserProfileData _currentDbUser;
    private PostData _currentDbPost;
    private int _pendingCompletedDay = -1;
    private int _pendingNextDay = -1;
    private bool _dayShiftPlaying;
    private Coroutine _dayShiftRoutine;

    private readonly System.Random rng = new();
    private WorkDashboardAlgorithmUI _algorithmUi;

    /// <summary>Set by <see cref="GlitchInTheSystem.Intro.IntroManager"/> so opening the inactive window doesn't run <see cref="StartSession"/> before the tutorial queue exists.</summary>
    private bool _suppressStartSessionOnce;
    private bool _moderationLocked;

    /// <summary>Called by <see cref="GlitchInTheSystem.Interruptions.InterruptionManager"/> during error/captcha interruptions.</summary>
    public void SetModerationLocked(bool locked)
    {
        _moderationLocked = locked;
        if (approveButton != null) approveButton.interactable = !locked;
        if (declineButton != null) declineButton.interactable = !locked;
        if (flagButton != null) flagButton.interactable = !locked;
    }

    private static readonly string[] FirstNames = { "Avery", "Jordan", "Sam", "Taylor", "Riley", "Morgan", "Casey", "Quinn", "Jamie", "Dakota" };
    private static readonly string[] LastNames = { "Nguyen", "Patel", "Johnson", "Garcia", "Kim", "Brown", "Lopez", "Singh", "Chen", "Martinez" };
    private static readonly string[] Handles = { "hot_take", "newsfeed", "pixelpanda", "civic_watch", "dailybytes", "meme_station", "truthseeker", "cloudchaser", "neutral_node", "echo_room" };

    // Offline mode uses the same caption pool as GameDatabase (Step 3 — retired duplicate templates).

    private static readonly string[] ReputationLabels = { "Trusted", "Neutral", "Low Trust", "Watchlisted" };
    private static readonly string[] RiskLabels = { "Low", "Medium", "High" };

    private void Reset()
    {
        AutoBindByName();
    }

    private void Awake()
    {
        AutoBindByName();
        EnsureWindowFocusHandler();
        _algorithmUi = GetComponent<WorkDashboardAlgorithmUI>();
        if (_algorithmUi == null)
            _algorithmUi = gameObject.AddComponent<WorkDashboardAlgorithmUI>();

        AlgorithmPostAlteredNotifier.PostAltered += OnAlgorithmPostAltered;

        WireButtonsIfPresent();
        LogMissingBindingsOnce();
    }

    private void OnDestroy()
    {
        AlgorithmPostAlteredNotifier.PostAltered -= OnAlgorithmPostAltered;
    }

    private void OnAlgorithmPostAltered(PostData post, bool rewrite)
    {
        if (post == null || _currentDbPost == null || post.id != _currentDbPost.id) return;

        _algorithmUi?.OnPostTextAltered(postText, rewrite);
        currentPost = MapPost(post, _currentDbUser);
        Render();
    }

    private void OnEnable()
    {
        if (_suppressStartSessionOnce)
        {
            _suppressStartSessionOnce = false;
            return;
        }

        StartSession();
    }

    /// <summary>
    /// Inspector / Button hook: randomize a new person + post without recording a decision.
    /// </summary>
    public void RandomizeNow()
    {
        if (!EnsureReady()) return;
        currentPerson = RandomPerson();
        currentPost = RandomPost(currentPerson);
        Render();
    }

    /// <summary>
    /// Inspector / Button hook.
    /// </summary>
    public void Approve() => Decide(playerApproved: true, playerReason: null);

    /// <summary>
    /// Inspector / Button hook.
    /// </summary>
    public void Decline() => Decide(playerApproved: false, playerReason: null);

    /// <summary>
    /// Optional action: treat as a removal + escalation note.
    /// This keeps gameplay simple while still teaching a "flag/escalate" concept in the intro.
    /// </summary>
    public void Flag() => Decide(playerApproved: false, playerReason: "FLAG: Escalated for review");

    /// <summary>
    /// Call this when you open/show the dashboard panel. Resumes from last state if session in progress.
    /// </summary>
    public void StartSession()
    {
        AutoBindByName();
        WireButtonsIfPresent();
        HideDayTransitionPanel();

        if (useGameDatabase && GameDatabase.Instance != null)
        {
            var db = GameDatabase.Instance;
            dayNumber = db.Config != null ? db.Config.currentDay : 1;
            int qCount = db.ModerationQueueCount;
            postsPerSession = qCount > 0 ? qCount : (db.Config != null ? db.Config.postsPerDay : 10);

            // Finished all items in this loaded queue → day shift (skip during intro tutorial — IntroManager owns that handoff).
            if (!db.IsIntroTutorialSession
                && db.Posts.Count > 0
                && qCount > 0
                && db.GetDecisionsCount() >= qCount)
            {
                int nextDay = dayNumber + 1;
                _pendingCompletedDay = dayNumber;
                _pendingNextDay = nextDay;
                BeginDayShiftTransition(_pendingCompletedDay, _pendingNextDay);
                return;
            }

            // Re-opening after ≥1 decisions (true resume). Not fired on first post at 0/N — that wrongly showed “Session resumed”.
            if (db.HasResumeableModerationSession())
            {
                currentIndex = db.GetDecisionsCount();
                if (decisionResultText != null) decisionResultText.text = "—";
                ApplyFontMultiplier();
                if (!EnsureReady()) return;
                UpdateTopBar();
                AlgorithmNotification.Instance?.Show(AlgorithmVoice.SessionResumed(currentIndex, postsPerSession));
                Next();
                NotifyInterruptionWorkSessionStarted();
                return;
            }

            // Queue already populated (tutorial, feed seeded early, etc.) but no decisions yet → use it once, do not wipe.
            if (db.HasActiveModerationQueue() && db.Decisions.Count == 0)
            {
                currentIndex = 0;
                if (decisionResultText != null) decisionResultText.text = "—";
                ApplyFontMultiplier();
                if (!EnsureReady()) return;
                UpdateTopBar();
                AlgorithmNotification.Instance?.Show(AlgorithmVoice.QueueLoaded(postsPerSession, dayNumber));
                Next();
                NotifyInterruptionWorkSessionStarted();
                return;
            }

            db.InitializeSession();
            postsPerSession = db.Config != null ? db.Config.postsPerDay : postsPerSession;
            dayNumber = db.Config != null ? db.Config.currentDay : dayNumber;
            currentIndex = 0;
            AlgorithmNotification.Instance?.Show(AlgorithmVoice.QueueLoaded(postsPerSession, dayNumber));
        }
        else
        {
            // Non-DB: resume if we're in the middle (currentIndex and currentPerson/currentPost persist when panel is hidden)
            if (currentIndex > 0 && currentIndex < postsPerSession && currentPerson.Username != null)
            {
                if (decisionResultText != null) decisionResultText.text = "—";
                ApplyFontMultiplier();
                if (!EnsureReady()) return;
                UpdateTopBar();
                Render();
                NotifyInterruptionWorkSessionStarted();
                return;
            }

            currentIndex = 0;
        }

        if (decisionResultText != null) decisionResultText.text = "—";
        ApplyFontMultiplier();
        if (!EnsureReady()) return;
        UpdateTopBar();
        Next();

        NotifyInterruptionWorkSessionStarted();
    }

    private void BeginDayShiftTransition(int completedDay, int nextDay)
    {
        if (_dayShiftPlaying) return;

        _pendingCompletedDay = completedDay;
        _pendingNextDay = nextDay;

        if (_dayShiftRoutine != null)
            StopCoroutine(_dayShiftRoutine);

        _dayShiftRoutine = StartCoroutine(PlayDayShiftTransition(completedDay, nextDay));
    }

    private IEnumerator PlayDayShiftTransition(int completedDay, int nextDay)
    {
        _dayShiftPlaying = true;
        EnsureDayTransitionPanel();
        if (dayTransitionPanel == null)
        {
            _dayShiftPlaying = false;
            ProceedToNextDay();
            yield break;
        }

        ApplyDayTransitionOverlayLayout();
        ConfigureDayShiftOverlayCinematic(true);
        ScreenFadeUtility.ApplyFullBleed(dayTransitionPanel, dayTransitionPanel.GetComponent<Image>());

        if (dayTransitionText != null)
        {
            dayTransitionText.fontSize = 36;
            dayTransitionText.alignment = TextAlignmentOptions.Center;
            dayTransitionText.text =
                $"<size=58><b>DAY {nextDay}</b></size>\n\n<size=24><alpha=#BB>Day {completedDay} complete.";
        }

        CanvasGroup group = ScreenFadeUtility.EnsureCanvasGroup(dayTransitionPanel.gameObject);
        group.alpha = 0f;
        dayTransitionPanel.gameObject.SetActive(true);
        dayTransitionPanel.SetAsLastSibling();

        yield return ScreenFadeUtility.Fade(group, 1f, dayShiftFadeSeconds);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, dayShiftHoldSeconds));
        yield return ScreenFadeUtility.Fade(group, 0f, dayShiftFadeSeconds);

        dayTransitionPanel.gameObject.SetActive(false);
        ConfigureDayShiftOverlayCinematic(false);
        _dayShiftPlaying = false;
        _dayShiftRoutine = null;

        ProceedToNextDay();
    }

    private void ConfigureDayShiftOverlayCinematic(bool cinematic)
    {
        if (dayTransitionPanel == null) return;

        var card = dayTransitionPanel.Find("Card");
        if (card != null)
            card.gameObject.SetActive(!cinematic);

        if (dayTransitionProceedButton != null)
            dayTransitionProceedButton.gameObject.SetActive(!cinematic);

        if (dayTransitionText != null && cinematic)
        {
            var rt = dayTransitionText.rectTransform;
            rt.SetParent(dayTransitionPanel, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(32f, 32f);
            rt.offsetMax = new Vector2(-32f, -32f);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    private void HideDayTransitionPanel()
    {
        if (_dayShiftPlaying)
            return;

        if (_dayShiftRoutine != null)
        {
            StopCoroutine(_dayShiftRoutine);
            _dayShiftRoutine = null;
        }

        if (dayTransitionPanel != null)
            dayTransitionPanel.gameObject.SetActive(false);
    }

    /// <summary>Stops an in-flight day-shift cinematic (e.g. when intro takes over after the tutorial queue).</summary>
    public void CancelDayShiftTransition()
    {
        if (_dayShiftRoutine != null)
        {
            StopCoroutine(_dayShiftRoutine);
            _dayShiftRoutine = null;
        }

        _dayShiftPlaying = false;
        _pendingCompletedDay = -1;
        _pendingNextDay = -1;

        if (dayTransitionPanel != null)
            dayTransitionPanel.gameObject.SetActive(false);

        ConfigureDayShiftOverlayCinematic(false);
    }

    private void TryBeginDayShiftAfterQueueComplete()
    {
        if (!useGameDatabase || GameDatabase.Instance == null || _dayShiftPlaying) return;

        var db = GameDatabase.Instance;
        if (db.IsIntroTutorialSession) return;
        int qCount = db.ModerationQueueCount;
        if (qCount <= 0 || db.GetDecisionsCount() < qCount) return;

        int completed = dayNumber;
        int next = dayNumber + 1;
        BeginDayShiftTransition(completed, next);
    }

    private void ProceedToNextDay()
    {
        if (_dayShiftPlaying) return;

        if (!useGameDatabase || GameDatabase.Instance == null)
        {
            HideDayTransitionPanel();
            return;
        }

        if (GameManager.Instance != null) GameManager.Instance.AdvanceToNextDay();
        else if (GameDatabase.Instance.Config != null) GameDatabase.Instance.Config.currentDay += 1;

        GameDatabase.Instance.InitializeSession();
        postsPerSession = GameDatabase.Instance.Config != null ? GameDatabase.Instance.Config.postsPerDay : postsPerSession;
        dayNumber = GameDatabase.Instance.Config != null ? GameDatabase.Instance.Config.currentDay : dayNumber;
        currentIndex = 0;

        if (decisionResultText != null) decisionResultText.text = "—";
        ApplyFontMultiplier();
        if (!EnsureReady()) return;
        UpdateTopBar();
        HideDayTransitionPanel();

        if (_pendingCompletedDay > 0 && _pendingNextDay > 0)
            AlgorithmNotification.Instance?.Show($"> Day {_pendingCompletedDay} ended. Loading Day {_pendingNextDay}...");
        _pendingCompletedDay = -1;
        _pendingNextDay = -1;

        AlgorithmNotification.Instance?.Show(AlgorithmVoice.QueueLoaded(postsPerSession, dayNumber));
        Next();

        NotifyInterruptionWorkSessionStarted();
        NotifyInterruptionDayAdvanced();
    }

    private static void NotifyInterruptionWorkSessionStarted()
    {
        var manager = FindFirstObjectByType<InterruptionManager>();
        manager?.OnWorkDashboardOpened();
    }

    private static void NotifyInterruptionDayAdvanced()
    {
        var manager = FindFirstObjectByType<InterruptionManager>();
        manager?.OnNarrativeDayAdvanced();
    }

    private void ApplyFontMultiplier()
    {
        if (fontSizeMultiplier <= 0.01f || Math.Abs(fontSizeMultiplier - 1f) < 0.001f) return;

        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.fontSize = Mathf.RoundToInt(tmp.fontSize * fontSizeMultiplier);
    }

    private void Decide(bool playerApproved, string playerReason)
    {
        if (_moderationLocked) return;
        if (!EnsureReady()) return;
        if (currentIndex >= postsPerSession) return;

        _algorithmUi?.OnPostClosed();

        bool finalApproved = playerApproved;
        bool overridden = false;
        string overrideReason = null;

        if (useGameDatabase && _currentDbPost != null && _currentDbUser != null && GameDatabase.Instance != null)
        {
            if (AlgorithmDirector.Instance != null)
            {
                var result = AlgorithmDirector.Instance.ProcessDecision(_currentDbPost.id, _currentDbUser.id, playerApproved, _currentDbPost);
                finalApproved = result.approved;
                overridden = result.overridden;
                overrideReason = result.reason;

                // If the algorithm didn't override, preserve playerReason (e.g. FLAG).
                string recordReason = overridden ? overrideReason : playerReason;
                GameDatabase.Instance.RecordDecision(_currentDbPost.id, _currentDbUser.id, finalApproved, playerApproved, overridden, recordReason);

                AlgorithmManager.Instance?.OnModerationDecision(
                    _currentDbPost.id, playerApproved, finalApproved, overridden);

                if (finalApproved)
                    AlgorithmDirector.Instance.TryEngagementNudge(_currentDbPost.id);
                else
                    AlgorithmDirector.Instance.TryShadowBanOnDecline(_currentDbUser.id);

                // Algorithm responds to your decision (content-aware: approved misinformation, declined real info, etc.)
                if (!overridden && AlgorithmNotification.Instance != null)
                {
                    var feedback = AlgorithmVoice.DecisionFeedback(_currentDbPost, playerApproved, AlgorithmDirector.Instance.Phase, _currentDbUser.username);
                    if (feedback != null)
                        AlgorithmNotification.Instance.Show(feedback, 3f);
                }
            }
            else
            {
                GameDatabase.Instance.RecordDecision(_currentDbPost.id, _currentDbUser.id, playerApproved, playerApproved, false, playerReason);
            }
        }

        string lastAuthor = ResolveLastModeratedAuthorHandle();
        string outcomeLine = BuildOutcomeLabelForCurrentPost(finalApproved, overridden, playerReason);

        AppendHistoryEntry(finalApproved, currentPerson, currentPost, overridden);
        currentIndex++;

        if (useGameDatabase && GameDatabase.Instance != null)
            GameDatabase.Instance.AdvanceQueue();

        UpdateTopBar();
        Next();
        ApplyDecisionResultBanner(lastAuthor, outcomeLine);
    }

    /// <summary>Author for the post that was just decided (before queue advance).</summary>
    private string ResolveLastModeratedAuthorHandle()
    {
        if (!string.IsNullOrEmpty(currentPerson.Username))
            return currentPerson.Username;
        if (!string.IsNullOrEmpty(currentPost.AuthorUsername))
            return currentPost.AuthorUsername;
        if (_currentDbUser != null && !string.IsNullOrEmpty(_currentDbUser.username))
            return _currentDbUser.username;
        return "user";
    }

    /// <summary>Short outcome line matching approve / decline / flag / algorithm override semantics.</summary>
    private string BuildOutcomeLabelForCurrentPost(bool finalApproved, bool overridden, string playerReason)
    {
        if (useGameDatabase && _currentDbPost != null)
        {
            bool flagged = !overridden
                           && !finalApproved
                           && !string.IsNullOrEmpty(playerReason)
                           && playerReason.StartsWith(ModerationDecisionFeedback.FlagReasonPrefix, StringComparison.Ordinal);
            if (flagged)
                return "Flagged";
            return ModerationDecisionFeedback.GetDashboardLine(finalApproved, overridden, _currentDbPost);
        }

        return overridden
            ? $"{(finalApproved ? "Approved" : "Declined")} (overridden)"
            : (finalApproved ? "Approved" : "Declined");
    }

    /// <summary>
    /// After <see cref="Next"/> loads the following post, clarify that the banner refers to the <b>previous</b> decision
    /// and that the next item is already on screen (or that the session queue is finished).
    /// </summary>
    private void ApplyDecisionResultBanner(string lastAuthor, string outcomeLine)
    {
        if (decisionResultText == null) return;

        string user = string.IsNullOrEmpty(lastAuthor) ? "user" : lastAuthor;
        string last = $"Last: @{user} — {outcomeLine}";

        if (currentIndex >= postsPerSession)
        {
            decisionResultText.text = $"{last}.\nAll queued posts reviewed for this session.";
            return;
        }

        decisionResultText.text = $"{last}.\nNext post ready ({currentIndex + 1}/{postsPerSession}).";
    }

    private void Next()
    {
        if (currentIndex >= postsPerSession)
        {
            if (queueText != null) queueText.text = $"Queue: {postsPerSession} / {postsPerSession}";
            UpdateTopBar(final: true);
            TryBeginDayShiftAfterQueueComplete();
            return;
        }

        if (useGameDatabase && GameDatabase.Instance != null)
        {
            var (user, post) = GameDatabase.Instance.GetNextModerationItem();
            if (user != null && post != null)
            {
                _currentDbUser = user;
                _currentDbPost = post;
                AlgorithmManager.Instance?.BeginPostReview(post.id);
                if (AlgorithmDirector.Instance != null)
                    AlgorithmDirector.Instance.TryRewritePost(post);

                currentPerson = MapUser(user);
                currentPost = MapPost(post, user);
                _algorithmUi?.OnPostDisplayed(post);

                // Algorithm reacts to post content (contextual, content-aware)
                if (AlgorithmDirector.Instance != null && AlgorithmNotification.Instance != null)
                {
                    var comment = AlgorithmVoice.CommentOnPost(post, AlgorithmDirector.Instance.Phase, user.username);
                    if (comment != null)
                        AlgorithmNotification.Instance.Show(comment, 3.5f);
                }
            }
            else
            {
                currentPerson = RandomPerson();
                currentPost = RandomPost(currentPerson);
                _currentDbUser = null;
                _currentDbPost = null;
            }
        }
        else
        {
            currentPerson = RandomPerson();
            currentPost = RandomPost(currentPerson);
            _currentDbUser = null;
            _currentDbPost = null;
        }

        Render();
    }

    private void Render()
    {
        // Left panel (person)
        Set(usernameText, $"@{currentPerson.Username}");
        Set(displayNameText, currentPerson.DisplayName);
        Set(accountAgeText, $"Account age: {currentPerson.AccountAgeYears}y");

        Set(followersText, $"Followers: {currentPerson.Followers:N0}");
        Set(followingText, $"Following: {currentPerson.Following:N0}");
        Set(strikesText, $"Strikes: {currentPerson.Strikes}");
        Set(reputationText, $"Reputation: {currentPerson.Reputation}");
        Set(riskText, $"Risk: {currentPerson.Risk}");

        // Right panel (post)
        Set(queueText, $"Queue: {currentIndex + 1} / {postsPerSession}");
        Set(postUserText, $"@{currentPost.AuthorUsername}");
        Set(timestampText, currentPost.TimestampLabel);
        Set(postText, BuildPostBodyForDisplay(currentPost));
        Set(reportReasonText, BuildReportLine(currentPost));
        Set(engagementRowText, $"Likes {currentPost.Likes:N0}  •  Shares {currentPost.Shares:N0}  •  Comments {currentPost.Comments:N0}");
    }

    private static string BuildPostBodyForDisplay(Post post)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(post.CategoryHint))
            sb.AppendLine(post.CategoryHint);
        sb.Append(SocialMediaFeedPresentation.SanitizeForTMP(post.Text ?? string.Empty));
        if (!string.IsNullOrWhiteSpace(post.ImageDescription))
        {
            sb.Append("\n\n[Attached image: ")
                .Append(SocialMediaFeedPresentation.SanitizeForTMP(post.ImageDescription))
                .Append(']');
        }

        if (post.HasAttachedComments)
            sb.Append("\n\n— Comments attached to this report (see thread in feed if approved) —");
        return sb.ToString().Trim();
    }

    private static string BuildReportLine(Post post)
    {
        if (string.IsNullOrWhiteSpace(post.ReportReason)) return string.Empty;
        return $"Report: {SocialMediaFeedPresentation.SanitizeForTMP(post.ReportReason)}";
    }

    private void UpdateTopBar(bool final = false)
    {
        if (dayInfoText == null) return;
        int completed = Mathf.Clamp(currentIndex, 0, postsPerSession);
        dayInfoText.text = final
            ? $"Day {dayNumber} — Posts: {postsPerSession}/{postsPerSession}"
            : $"Day {dayNumber} — Posts: {completed}/{postsPerSession}";
    }

    private void AppendHistoryEntry(bool approved, Person person, Post post, bool overridden = false)
    {
        if (decisionHistoryContent == null) return;

        var go = new GameObject($"Decision_{DateTime.Now:HHmmssfff}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(decisionHistoryContent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
        string suffix = overridden ? " (OVERRIDDEN)" : "";
        tmp.text = $"{(approved ? "APPROVED" : "DECLINED")}{suffix}  •  @{person.Username}  •  {TrimOneLine(post.Text, 60)}";
        tmp.color = approved ? new Color(0.65f, 1f, 0.72f, 1f) : new Color(1f, 0.70f, 0.70f, 1f);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 0);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 26;

        // Auto-scroll to bottom.
        if (decisionHistoryScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            decisionHistoryScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private static string TrimOneLine(string s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    private static void Set(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }

    private static Person MapUser(UserProfileData u)
    {
        if (u == null) return default;
        return new Person
        {
            Username = u.username,
            DisplayName = u.displayName,
            AccountAgeYears = u.accountAgeYears,
            Followers = u.followers,
            Following = u.following,
            Strikes = u.strikes,
            Reputation = u.reputation,
            Risk = u.risk
        };
    }

    private static Post MapPost(PostData p, UserProfileData author)
    {
        if (p == null) return default;
        string categoryHint = p.category switch
        {
            PostCategory.GrayArea => "[Gray area — judgment call]",
            PostCategory.Misinformation => "[Flagged: possible misinformation]",
            PostCategory.Narrative => "[Narrative / speculation]",
            PostCategory.Violation => "[Likely policy violation]",
            PostCategory.AlgorithmManipulation => "[Meta / platform critique]",
            _ => string.Empty
        };

        return new Post
        {
            AuthorUsername = author?.username ?? p.authorUserId,
            TimestampLabel = p.timestampLabel,
            Text = p.text,
            Likes = p.likes,
            Shares = p.shares,
            Comments = p.comments,
            ReportReason = p.reportReason,
            ImageDescription = p.imageDescription,
            CategoryHint = categoryHint,
            HasAttachedComments = p.presentationFormat == PostPresentationFormat.TextWithAttachedComments
        };
    }

    private Person RandomPerson()
    {
        string first = FirstNames[rng.Next(FirstNames.Length)];
        string last = LastNames[rng.Next(LastNames.Length)];
        string handle = Handles[rng.Next(Handles.Length)];

        int suffix = rng.Next(10, 999);
        string username = $"{handle}{suffix}";

        int followers = WeightedInt(0, 250_000, 6); // skew lower
        int following = WeightedInt(0, 5_000, 5);
        int strikes = rng.Next(0, 4);

        string rep = ReputationLabels[rng.Next(ReputationLabels.Length)];
        string risk = RiskLabels[rng.Next(RiskLabels.Length)];

        // Correlate a little: more strikes => higher risk, lower reputation.
        if (strikes >= 2) risk = "High";
        if (strikes == 0 && followers > 50_000) rep = "Trusted";
        if (strikes >= 3) rep = "Watchlisted";

        return new Person
        {
            Username = username,
            DisplayName = $"{first} {last}",
            AccountAgeYears = rng.Next(0, 11),
            Followers = followers,
            Following = following,
            Strikes = strikes,
            Reputation = rep,
            Risk = risk
        };
    }

    private Post RandomPost(Person person)
    {
        var entries = ModerationContentPools.AllQueueEntries;
        var entry = entries[rng.Next(entries.Count)];
        var author = new UserProfileData
        {
            id = "offline_author",
            username = person.Username,
            displayName = person.DisplayName,
            accountAgeYears = person.AccountAgeYears,
            followers = person.Followers,
            following = person.Following,
            strikes = person.Strikes,
            reputation = person.Reputation,
            risk = person.Risk
        };
        var postData = ModerationContentPools.BuildPostFromEntry(entry, author, $"offline_{rng.Next(100000)}", rng);
        return MapPost(postData, author);
    }

    private int WeightedInt(int min, int max, int power)
    {
        // Returns value skewed toward min using power curve.
        double u = rng.NextDouble();
        for (int i = 1; i < power; i++) u *= rng.NextDouble();
        return min + (int)Math.Round(u * (max - min));
    }

    private void AutoBindByName()
    {
        // Finds common names created by the builder; if you rename objects, you can set fields in the Inspector.
        dayInfoText ??= FindTMP("DayInfo");

        usernameText ??= FindTMP("UsernameText");
        displayNameText ??= FindTMP("DisplayNameText");
        accountAgeText ??= FindTMP("AccountAgeText");
        followersText ??= FindTMP("FollowersText");
        followingText ??= FindTMP("FollowingText");
        strikesText ??= FindTMP("StrikesText");
        reputationText ??= FindTMP("ReputationText");
        riskText ??= FindTMP("RiskText");

        queueText ??= FindTMP("QueueText");
        postUserText ??= FindTMP("PostUserText");
        timestampText ??= FindTMP("TimestampText");
        postText ??= FindTMP("PostText");
        reportReasonText ??= FindTMP("ReportReasonText");
        engagementRowText ??= FindTMP("EngagementRow");

        approveButton ??= FindButton("ApproveButton");
        declineButton ??= FindButton("DeclineButton");
        flagButton ??= FindButton("FlagButton");
        decisionResultText ??= FindTMP("DecisionResultText");
        dayTransitionText ??= FindTMP("DayTransitionText");

        decisionHistoryContent ??= FindRect("FloatingPanel/Body/RightPanel/DecisionHistory/Scroll/Viewport/Content");
        decisionHistoryContent ??= FindRect("Body/RightPanel/DecisionHistory/Scroll/Viewport/Content");
        decisionHistoryContent ??= FindRect("DecisionHistory/Scroll/Viewport/Content");
        decisionHistoryContent ??= FindDecisionHistoryContent();

        decisionHistoryScrollRect ??= FindScrollRect("Scroll");
        decisionHistoryScrollRect ??= FindScrollRect("DecisionHistory");

        dayTransitionPanel ??= FindRect("FloatingPanel/DayTransitionPanel");
        dayTransitionPanel ??= FindRect("DayTransitionPanel");
        dayTransitionProceedButton ??= FindButton("DayTransitionProceedButton");
    }

    private TMP_Text FindTMP(string name)
    {
        foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.name == name) return t;
        return null;
    }

    private Button FindButton(string name)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
        return null;
    }

    private RectTransform FindRect(string path)
    {
        var t = transform.Find(path);
        return t as RectTransform;
    }

    private ScrollRect FindScrollRect(string name)
    {
        foreach (var s in GetComponentsInChildren<ScrollRect>(true))
            if (s.name == name) return s;
        return null;
    }

    private RectTransform FindDecisionHistoryContent()
    {
        foreach (var rt in GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name != "Content") continue;
            var p = rt.parent;
            if (p == null || p.name != "Viewport") continue;
            var gp = p.parent;
            if (gp == null || gp.name != "Scroll") continue;
            var ggp = gp.parent;
            if (ggp == null || ggp.name != "DecisionHistory") continue;
            return rt;
        }
        return null;
    }

    private bool EnsureReady()
    {
        // Only require what's needed for "randomize + approve/decline" loop.
        if (dayInfoText == null) return false;
        if (usernameText == null || displayNameText == null || accountAgeText == null) return false;
        if (followersText == null || followingText == null || strikesText == null || reputationText == null || riskText == null) return false;
        if (queueText == null || postUserText == null || timestampText == null || postText == null || engagementRowText == null) return false;
        return true;
    }

    private void WireButtonsIfPresent()
    {
        if (approveButton != null)
        {
            approveButton.onClick.RemoveAllListeners();
            approveButton.onClick.AddListener(Approve);
        }

        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(Decline);
        }

        if (flagButton != null)
        {
            flagButton.onClick.RemoveAllListeners();
            flagButton.onClick.AddListener(Flag);
        }

        if (dayTransitionProceedButton != null)
        {
            dayTransitionProceedButton.onClick.RemoveAllListeners();
            dayTransitionProceedButton.onClick.AddListener(ProceedToNextDay);
        }
    }

    private void LogMissingBindingsOnce()
    {
        // This is intentionally noisy only when something is missing.
        var missing = new List<string>();

        if (usernameText == null) missing.Add(nameof(usernameText));
        if (displayNameText == null) missing.Add(nameof(displayNameText));
        if (accountAgeText == null) missing.Add(nameof(accountAgeText));
        if (followersText == null) missing.Add(nameof(followersText));
        if (followingText == null) missing.Add(nameof(followingText));
        if (strikesText == null) missing.Add(nameof(strikesText));
        if (reputationText == null) missing.Add(nameof(reputationText));
        if (riskText == null) missing.Add(nameof(riskText));

        if (queueText == null) missing.Add(nameof(queueText));
        if (postUserText == null) missing.Add(nameof(postUserText));
        if (timestampText == null) missing.Add(nameof(timestampText));
        if (postText == null) missing.Add(nameof(postText));
        if (engagementRowText == null) missing.Add(nameof(engagementRowText));

        if (approveButton == null) missing.Add(nameof(approveButton));
        if (declineButton == null) missing.Add(nameof(declineButton));
        // decisionHistoryContent is optional; AppendHistoryEntry safely no-ops when missing.

        if (missing.Count > 0)
            Debug.LogWarning($"{nameof(WorkDashboardController)} is missing bindings: {string.Join(", ", missing)}. Either keep the default UI object names (from the builder) or assign these fields in the Inspector on {gameObject.name}.", this);
    }

    private void EnsureWindowFocusHandler()
    {
        var root = transform as RectTransform;
        var panel = transform.Find("FloatingPanel") as RectTransform;
        if (panel == null || root == null) return;

        var img = panel.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        var focus = panel.GetComponent<WindowFocusOnClick>();
        if (focus == null) focus = panel.gameObject.AddComponent<WindowFocusOnClick>();
        focus.SetTarget(root);
    }

    private RectTransform ResolveDayTransitionOverlayRoot()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas.transform as RectTransform;

        var sceneCanvas = FindFirstObjectByType<Canvas>();
        return sceneCanvas != null ? sceneCanvas.transform as RectTransform : transform.root as RectTransform;
    }

    private void ApplyDayTransitionOverlayLayout()
    {
        if (dayTransitionPanel == null) return;

        var root = ResolveDayTransitionOverlayRoot();
        if (root == null) return;

        if (dayTransitionPanel.parent != root)
            dayTransitionPanel.SetParent(root, false);

        var overlayImg = dayTransitionPanel.GetComponent<Image>();
        ScreenFadeUtility.ApplyReferenceResolution(
            dayTransitionPanel,
            DayTransitionWidth,
            DayTransitionHeight,
            overlayImg);
        dayTransitionPanel.SetAsLastSibling();
    }

    private void EnsureDayTransitionPanel()
    {
        AutoBindByName();
        if (dayTransitionPanel != null && dayTransitionText != null && dayTransitionProceedButton != null)
        {
            ApplyDayTransitionOverlayLayout();
            return;
        }

        var overlayRoot = ResolveDayTransitionOverlayRoot();
        if (overlayRoot == null) return;

        if (dayTransitionPanel == null)
        {
            // Full-HD overlay on the canvas root (not clipped to the dashboard window).
            var overlayGo = new GameObject("DayTransitionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayGo.transform.SetParent(overlayRoot, false);
            dayTransitionPanel = overlayGo.transform as RectTransform;
            ScreenFadeUtility.ApplyReferenceResolution(
                dayTransitionPanel,
                DayTransitionWidth,
                DayTransitionHeight,
                overlayGo.GetComponent<Image>());

            var overlayImg = overlayGo.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.45f);
            overlayImg.raycastTarget = true;

            // Center card (desktop modal look).
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            cardGo.transform.SetParent(dayTransitionPanel, false);
            var cardRt = cardGo.transform as RectTransform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 300f);

            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.10f, 0.14f, 0.985f);
            cardImg.raycastTarget = true;

            var outline = cardGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.34f, 0.72f, 1f, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Header strip to visually match desktop app chrome.
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerGo.transform.SetParent(cardRt, false);
            var hrt = headerGo.transform as RectTransform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(0f, 42f);
            hrt.anchoredPosition = Vector2.zero;
            var himg = headerGo.GetComponent<Image>();
            himg.color = new Color(0.12f, 0.16f, 0.22f, 1f);

            var headerLabel = new GameObject("HeaderLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            headerLabel.transform.SetParent(headerGo.transform, false);
            var hlrt = headerLabel.transform as RectTransform;
            hlrt.anchorMin = Vector2.zero;
            hlrt.anchorMax = Vector2.one;
            hlrt.offsetMin = new Vector2(16f, 0f);
            hlrt.offsetMax = new Vector2(-16f, 0f);
            var htmp = headerLabel.GetComponent<TextMeshProUGUI>();
            htmp.text = "WORK DASHBOARD — DAY TRANSITION";
            htmp.fontSize = 16;
            htmp.alignment = TextAlignmentOptions.MidlineLeft;
            htmp.color = new Color(0.80f, 0.90f, 1f, 1f);
            htmp.raycastTarget = false;
        }

        var card = dayTransitionPanel != null ? dayTransitionPanel.Find("Card") as RectTransform : null;
        if (card == null) card = dayTransitionPanel; // fallback

        if (dayTransitionText == null)
        {
            var textGo = new GameObject("DayTransitionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(card, false);
            var rt = textGo.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0.28f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(26f, 12f);
            rt.offsetMax = new Vector2(-26f, -54f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "Day ended.";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.93f, 0.95f, 0.99f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            dayTransitionText = tmp;
        }

        if (dayTransitionProceedButton == null)
        {
            var buttonGo = new GameObject("DayTransitionProceedButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(card, false);
            var brt = buttonGo.transform as RectTransform;
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 24f);
            brt.sizeDelta = new Vector2(220f, 50f);

            var bImg = buttonGo.GetComponent<Image>();
            bImg.color = new Color(0.20f, 0.58f, 0.30f, 1f);

            dayTransitionProceedButton = buttonGo.GetComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var lrt = labelGo.transform as RectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var ltmp = labelGo.GetComponent<TextMeshProUGUI>();
            ltmp.text = "Proceed";
            ltmp.fontSize = 22;
            ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.color = Color.white;
            ltmp.raycastTarget = false;
        }

        ApplyDayTransitionOverlayLayout();
        dayTransitionPanel.SetAsLastSibling();
        WireButtonsIfPresent();
    }

    [Serializable]
    private struct Person
    {
        public string Username;
        public string DisplayName;
        public int AccountAgeYears;
        public int Followers;
        public int Following;
        public int Strikes;
        public string Reputation;
        public string Risk;
    }

    [Serializable]
    private struct Post
    {
        public string AuthorUsername;
        public string TimestampLabel;
        public string Text;
        public int Likes;
        public int Shares;
        public int Comments;
        public string ReportReason;
        public string ImageDescription;
        public string CategoryHint;
        public bool HasAttachedComments;
    }
}

