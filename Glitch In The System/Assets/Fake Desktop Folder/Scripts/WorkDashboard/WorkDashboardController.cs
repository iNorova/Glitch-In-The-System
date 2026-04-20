using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.UI;

public sealed class WorkDashboardController : MonoBehaviour
{
    [Header("Data source")]
    [Tooltip("If true, pulls posts from GameDatabase and runs through AlgorithmDirector. Requires GameDatabase + AlgorithmDirector in scene.")]
    [SerializeField] private bool useGameDatabase = false;

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
    [SerializeField] private TMP_Text engagementRowText;

    [Header("Decision")]
    [SerializeField] private Button approveButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private TMP_Text decisionResultText;

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

    private readonly System.Random rng = new();

    private static readonly string[] FirstNames = { "Avery", "Jordan", "Sam", "Taylor", "Riley", "Morgan", "Casey", "Quinn", "Jamie", "Dakota" };
    private static readonly string[] LastNames = { "Nguyen", "Patel", "Johnson", "Garcia", "Kim", "Brown", "Lopez", "Singh", "Chen", "Martinez" };
    private static readonly string[] Handles = { "hot_take", "newsfeed", "pixelpanda", "civic_watch", "dailybytes", "meme_station", "truthseeker", "cloudchaser", "neutral_node", "echo_room" };

    private static readonly string[] PostTemplates =
    {
        "Just saw the leaked docs. They're hiding what really happened in the election. Share before it's gone.",
        "The mainstream media won't report this. Politician caught on tape admitting the truth. RT to spread.",
        "BREAKING: Politician declared dead but I saw them at a private event yesterday. Cover-up?",
        "Just heard the celebrity passed away. No official confirmation yet but sources say it's true. Rest in peace.",
        "The hospital is hiding the real death toll. I know someone who works there. It's way worse.",
        "The cure they don't want you to know about. Big Pharma has been hiding this for decades.",
        "My aunt took this and her cancer went away. Doctors said it was impossible. Do your research.",
        "Vaccine injury cover-up. Thousands of reports being deleted. I have the screenshots.",
        "That 'leaked' video is a deepfake. I work in AI, I can tell. Don't fall for it.",
        "Fake charity alert. That GoFundMe is a scam. The family confirmed they didn't set it up.",
        "If you know, you know. The 'official story' doesn't add up.",
        "Reminder: be kind. People are going through a lot right now.",
        "This is obviously satire but people are taking it seriously.",
        "Algorithm is boosting the wrong posts again. Engagement over truth, as usual."
    };

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

        WireButtonsIfPresent();
        LogMissingBindingsOnce();
    }

    private void OnEnable()
    {
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
    public void Approve() => Decide(true);

    /// <summary>
    /// Inspector / Button hook.
    /// </summary>
    public void Decline() => Decide(false);

    /// <summary>
    /// Call this when you open/show the dashboard panel. Resumes from last state if session in progress.
    /// </summary>
    public void StartSession()
    {
        AutoBindByName();
        WireButtonsIfPresent();

        if (useGameDatabase && GameDatabase.Instance != null)
        {
            postsPerSession = GameDatabase.Instance.Config != null ? GameDatabase.Instance.Config.postsPerDay : 10;
            dayNumber = GameDatabase.Instance.Config != null ? GameDatabase.Instance.Config.currentDay : 1;

            if (GameDatabase.Instance.HasSessionInProgress())
            {
                // Resuming — don't reset, restore state and show current item
                currentIndex = GameDatabase.Instance.GetDecisionsCount();
                if (decisionResultText != null) decisionResultText.text = "—";
                ApplyFontMultiplier();
                if (!EnsureReady()) return;
                UpdateTopBar();
                AlgorithmNotification.Instance?.Show(AlgorithmVoice.SessionResumed(currentIndex, postsPerSession));
                Next();
                return;
            }

            // Fresh session
            GameDatabase.Instance.InitializeSession();
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
                return;
            }

            currentIndex = 0;
        }

        if (decisionResultText != null) decisionResultText.text = "—";
        ApplyFontMultiplier();
        if (!EnsureReady()) return;
        UpdateTopBar();
        Next();
    }

    private void ApplyFontMultiplier()
    {
        if (fontSizeMultiplier <= 0.01f || Math.Abs(fontSizeMultiplier - 1f) < 0.001f) return;

        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.fontSize = Mathf.RoundToInt(tmp.fontSize * fontSizeMultiplier);
    }

    private void Decide(bool playerApproved)
    {
        if (!EnsureReady()) return;
        if (currentIndex >= postsPerSession) return;

        bool finalApproved = playerApproved;
        bool overridden = false;
        string overrideReason = null;

        if (useGameDatabase && _currentDbPost != null && _currentDbUser != null && AlgorithmDirector.Instance != null)
        {
            var result = AlgorithmDirector.Instance.ProcessDecision(_currentDbPost.id, _currentDbUser.id, playerApproved, _currentDbPost);
            finalApproved = result.approved;
            overridden = result.overridden;
            overrideReason = result.reason;

            GameDatabase.Instance.RecordDecision(_currentDbPost.id, _currentDbUser.id, finalApproved, playerApproved, overridden, overrideReason);

            if (finalApproved)
                AlgorithmDirector.Instance.TryEngagementNudge(_currentDbPost.id);
            else
                AlgorithmDirector.Instance.TryShadowBanOnDecline(_currentDbUser.id);

            // Algorithm responds to your decision (content-aware: approved misinformation, declined real info, etc.)
            if (!overridden && AlgorithmNotification.Instance != null)
            {
                var feedback = AlgorithmVoice.DecisionFeedback(_currentDbPost, playerApproved, AlgorithmDirector.Instance.Phase, _currentDbUser.username);
                if (feedback != null) AlgorithmNotification.Instance.Show(feedback, 3f);
            }
        }

        if (decisionResultText != null)
        {
            if (useGameDatabase && _currentDbPost != null)
                decisionResultText.text = ModerationDecisionFeedback.GetDashboardLine(finalApproved, overridden, _currentDbPost);
            else
                decisionResultText.text = overridden ? $"{(finalApproved ? "Approved" : "Declined")} (overridden)" : (finalApproved ? "Approved" : "Declined");
        }

        AppendHistoryEntry(finalApproved, currentPerson, currentPost, overridden);
        currentIndex++;

        if (useGameDatabase && GameDatabase.Instance != null)
            GameDatabase.Instance.AdvanceQueue();

        UpdateTopBar();
        Next();
    }

    private void Next()
    {
        if (currentIndex >= postsPerSession)
        {
            if (queueText != null) queueText.text = $"Queue: {postsPerSession} / {postsPerSession}";
            UpdateTopBar(final: true);
            return;
        }

        if (useGameDatabase && GameDatabase.Instance != null)
        {
            var (user, post) = GameDatabase.Instance.GetNextModerationItem();
            if (user != null && post != null)
            {
                _currentDbUser = user;
                _currentDbPost = post;
                if (AlgorithmDirector.Instance != null)
                    AlgorithmDirector.Instance.TryRewritePost(post);

                currentPerson = MapUser(user);
                currentPost = MapPost(post, user);

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
        Set(postText, currentPost.Text);
        Set(engagementRowText, $"Likes {currentPost.Likes:N0}  •  Shares {currentPost.Shares:N0}  •  Comments {currentPost.Comments:N0}");
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
        return new Post
        {
            AuthorUsername = author?.username ?? p.authorUserId,
            TimestampLabel = p.timestampLabel,
            Text = p.text,
            Likes = p.likes,
            Shares = p.shares,
            Comments = p.comments
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
        string template = PostTemplates[rng.Next(PostTemplates.Length)];
        int likes = WeightedInt(0, 40_000, 5);
        int shares = WeightedInt(0, 10_000, 5);
        int comments = WeightedInt(0, 5_000, 5);

        return new Post
        {
            AuthorUsername = person.Username,
            TimestampLabel = $"{rng.Next(1, 23)}h",
            Text = template,
            Likes = likes,
            Shares = shares,
            Comments = comments
        };
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
        engagementRowText ??= FindTMP("EngagementRow");

        approveButton ??= FindButton("ApproveButton");
        declineButton ??= FindButton("DeclineButton");
        decisionResultText ??= FindTMP("DecisionResultText");

        decisionHistoryContent ??= FindRect("FloatingPanel/Body/RightPanel/DecisionHistory/Scroll/Viewport/Content");
        decisionHistoryContent ??= FindRect("Body/RightPanel/DecisionHistory/Scroll/Viewport/Content");
        decisionHistoryContent ??= FindRect("DecisionHistory/Scroll/Viewport/Content");
        decisionHistoryContent ??= FindDecisionHistoryContent();

        decisionHistoryScrollRect ??= FindScrollRect("Scroll");
        decisionHistoryScrollRect ??= FindScrollRect("DecisionHistory");
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
    }
}

