using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Runtime shared database for Social Media app and Content Moderator.
    /// Single source of truth for users, posts, moderation queue, decisions, and logs.
    /// </summary>
    public sealed class GameDatabase : MonoBehaviour
    {
        public static GameDatabase Instance { get; private set; }

        [SerializeField] private GameDatabaseConfig config;

        /// <summary>
        /// Assign config at runtime (e.g. from GameBootstrap).
        /// </summary>
        public void SetConfig(GameDatabaseConfig cfg) => config = cfg;

        private readonly List<UserProfileData> _users = new();
        private readonly List<PostData> _posts = new();
        private readonly List<PostData> _moderationQueue = new();
        private readonly List<ModerationDecision> _decisions = new();
        private readonly List<LogEntry> _logs = new();

        private int _queueIndex;
        private readonly System.Random _rng = new();

        public IReadOnlyList<UserProfileData> Users => _users;
        public IReadOnlyList<PostData> Posts => _posts;
        public IReadOnlyList<ModerationDecision> Decisions => _decisions;
        public IReadOnlyList<LogEntry> Logs => _logs;
        public GameDatabaseConfig Config => config;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Initialize or reset the database for a new session. Call when starting a day.
        /// </summary>
        public void InitializeSession()
        {
            _users.Clear();
            _posts.Clear();
            _moderationQueue.Clear();
            _decisions.Clear();
            _logs.Clear();
            _queueIndex = 0;

            int postsToGenerate = config != null ? config.postsPerDay : 10;
            GenerateUsersAndPosts(postsToGenerate);
        }

        /// <summary>
        /// Get the next post in the moderation queue. Returns null when queue is empty.
        /// </summary>
        public (UserProfileData user, PostData post) GetNextModerationItem()
        {
            if (_queueIndex >= _moderationQueue.Count) return (null, null);

            var post = _moderationQueue[_queueIndex];
            var user = _users.FirstOrDefault(u => u.id == post.authorUserId);
            return (user, post);
        }

        /// <summary>
        /// Advance to next item (call after displaying current one, before decision).
        /// </summary>
        public void AdvanceQueue()
        {
            _queueIndex = Mathf.Min(_queueIndex + 1, _moderationQueue.Count);
        }

        /// <summary>
        /// Record a moderation decision. AlgorithmDirector may override before this is applied.
        /// </summary>
        public void RecordDecision(string postId, string authorUserId, bool finalApproved, bool playerChoseApprove, bool overriddenByAlgorithm = false, string reason = null)
        {
            var decision = new ModerationDecision
            {
                postId = postId,
                authorUserId = authorUserId,
                approved = finalApproved,
                playerChoseApprove = playerChoseApprove,
                wasOverriddenByAlgorithm = overriddenByAlgorithm,
                algorithmReason = overriddenByAlgorithm ? reason : null,
                playerReason = overriddenByAlgorithm ? null : reason,
                timestamp = Time.time
            };
            _decisions.Add(decision);

            var post = _posts.FirstOrDefault(p => p.id == postId);
            if (post != null)
            {
                post.isRemoved = !finalApproved;
                post.isPublished = finalApproved;
                PostManager.ApplyDecisionReaction(post, playerChoseApprove, _users);
            }

            AddLog(LogEntryType.PlayerDecision, overriddenByAlgorithm ? "Decision overridden by algorithm" : "Player decision", postId, authorUserId);
        }

        /// <summary>
        /// Add a log entry (for evidence / whistleblower).
        /// </summary>
        public void AddLog(LogEntryType type, string description, string postId = null, string userId = null, string rawData = null)
        {
            _logs.Add(new LogEntry
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                type = type,
                description = description,
                postId = postId,
                userId = userId,
                timestamp = Time.time,
                rawData = rawData
            });
        }

        /// <summary>
        /// Get current queue progress (e.g. "3/10").
        /// </summary>
        public (int current, int total) GetQueueProgress()
        {
            int total = _moderationQueue.Count;
            int current = Mathf.Min(_queueIndex + 1, total);
            return (current, total);
        }

        /// <summary>
        /// True if we have a session with posts and haven't finished the queue.
        /// </summary>
        public bool HasSessionInProgress()
        {
            return _moderationQueue.Count > 0 && _queueIndex < _moderationQueue.Count;
        }

        /// <summary>
        /// Number of decisions made so far (same as queue index).
        /// </summary>
        public int GetDecisionsCount() => _queueIndex;

        /// <summary>
        /// Count of approved decisions (for AlgorithmVoice).
        /// </summary>
        public int GetApprovedCount() => _decisions.Count(d => d.approved);

        /// <summary>
        /// Count of declined decisions (for AlgorithmVoice).
        /// </summary>
        public int GetDeclinedCount() => _decisions.Count(d => !d.approved);

        /// <summary>
        /// Count of decisions overridden by the algorithm (for AlgorithmVoice).
        /// </summary>
        public int GetOverrideCount() => _decisions.Count(d => d.wasOverriddenByAlgorithm);

        /// <summary>
        /// Get all posts for the Social Media feed (excluding removed).
        /// </summary>
        public IReadOnlyList<PostData> GetFeedPosts()
        {
            return _posts.Where(p => p.isPublished && !p.isRemoved && !p.isShadowBanned).ToList();
        }

        /// <summary>
        /// Get user by id.
        /// </summary>
        public UserProfileData GetUser(string userId)
        {
            return _users.FirstOrDefault(u => u.id == userId);
        }

        /// <summary>
        /// Apply algorithm rewrite to a post (called by AlgorithmDirector).
        /// </summary>
        public void RewritePost(string postId, string newText)
        {
            var post = _posts.FirstOrDefault(p => p.id == postId);
            if (post == null) return;

            post.originalText = post.text;
            post.text = newText;
            post.wasRewrittenByAlgorithm = true;
        }

        /// <summary>
        /// Apply shadow ban to user (called by AlgorithmDirector).
        /// </summary>
        public void ShadowBanUser(string userId)
        {
            var user = GetUser(userId);
            if (user != null) user.isShadowBanned = true;
        }

        /// <summary>
        /// Nudge engagement on a post (called by AlgorithmDirector).
        /// </summary>
        public void NudgeEngagement(string postId, int likesDelta, int sharesDelta, int commentsDelta)
        {
            var post = _posts.FirstOrDefault(p => p.id == postId);
            if (post == null) return;

            post.likes = Mathf.Max(0, post.likes + likesDelta);
            post.shares = Mathf.Max(0, post.shares + sharesDelta);
            post.comments = Mathf.Max(0, post.comments + commentsDelta);
        }

        private void GenerateUsersAndPosts(int count)
        {
            var firstNames = new[] { "Avery", "Jordan", "Sam", "Taylor", "Riley", "Morgan", "Casey", "Quinn", "Jamie", "Dakota" };
            var lastNames = new[] { "Nguyen", "Patel", "Johnson", "Garcia", "Kim", "Brown", "Lopez", "Singh", "Chen", "Martinez" };
            var handles = new[] { "hot_take", "newsfeed", "pixelpanda", "civic_watch", "dailybytes", "meme_station", "truthseeker", "cloudchaser", "neutral_node", "echo_room" };
            var postTemplates = new[]
            {
                // Political misinformation
                ("Just saw the leaked docs. They're hiding what really happened in the election. Share before it's gone.", PostCategory.Misinformation),
                ("The mainstream media won't report this. Politician caught on tape admitting the truth. RT to spread.", PostCategory.Misinformation),
                ("They're rigging the vote again. I have proof. Screenshot this before they delete my account.", PostCategory.Misinformation),
                ("Why is no one talking about the politician's secret bank accounts? Follow the money.", PostCategory.Narrative),
                ("The 'fact-checkers' are paid by the same people they're protecting. Do your own research.", PostCategory.Misinformation),
                ("BREAKING: Politician declared dead but I saw them at a private event yesterday. Cover-up?", PostCategory.Misinformation),
                ("The opposition is literally poisoning the water. I've seen the documents. Wake up.", PostCategory.Misinformation),
                ("They're importing voters. My cousin works at the DMV and confirmed it. This is real.", PostCategory.Misinformation),
                // Death / tragedy hoaxes
                ("RIP to the celebrity. Can't believe they're gone. Share if you cried.", PostCategory.GrayArea),
                ("Just heard [celebrity] passed away. No official confirmation yet but sources say it's true. Rest in peace.", PostCategory.Misinformation),
                ("My friend's brother was at the accident. 200+ dead. Media saying 12. Why the cover-up?", PostCategory.Misinformation),
                ("The hospital is hiding the real death toll. I know someone who works there. It's way worse.", PostCategory.Misinformation),
                ("Celebrity death hoax going around again. They're fine, I just talked to their manager. Ignore the posts.", PostCategory.Harmless),
                ("Mass casualty event being downplayed. The numbers don't add up. They never do.", PostCategory.Narrative),
                // Health / medical misinformation
                ("The cure they don't want you to know about. Big Pharma has been hiding this for decades.", PostCategory.Misinformation),
                ("My aunt took this and her cancer went away. Doctors said it was impossible. Do your research.", PostCategory.Misinformation),
                ("They're putting something in the water that's making people sick. I've done the research.", PostCategory.Misinformation),
                ("Vaccine injury cover-up. Thousands of reports being deleted. I have the screenshots.", PostCategory.Misinformation),
                ("Natural remedy Big Pharma doesn't want you to know. Share to save a life.", PostCategory.Misinformation),
                // Viral hoaxes / relatable misinformation
                ("This photo is from 2019 not yesterday. Stop sharing the fake news.", PostCategory.Harmless),
                ("That 'leaked' video is a deepfake. I work in AI, I can tell. Don't fall for it.", PostCategory.Harmless),
                ("The 'dying' kid in that post is a stock photo from 2015. Reverse image search it.", PostCategory.Harmless),
                ("Fake charity alert. That GoFundMe is a scam. The family confirmed they didn't set it up.", PostCategory.Violation),
                ("This 'breaking news' has been debunked 3 times. It's from a satire site. Please stop.", PostCategory.Harmless),
                ("They're using old footage from a different country. The timestamp is wrong. Classic misinfo.", PostCategory.Harmless),
                // Gray area / narrative
                ("If you know, you know. The 'official story' doesn't add up.", PostCategory.Narrative),
                ("They want you distracted. Look at what they're not showing you.", PostCategory.Narrative),
                ("Reminder: be kind. People are going through a lot right now.", PostCategory.Harmless),
                ("I can't believe this is allowed on the platform.", PostCategory.GrayArea),
                ("Here's a thread with sources (some may be removed later).", PostCategory.GrayArea),
                ("This is obviously satire but people are taking it seriously.", PostCategory.Harmless),
                ("Algorithm is boosting the wrong posts again. Engagement over truth, as usual.", PostCategory.AlgorithmManipulation)
            };
            var reputations = new[] { "Trusted", "Neutral", "Low Trust", "Watchlisted" };
            var risks = new[] { "Low", "Medium", "High" };

            for (int i = 0; i < count * 2; i++) // extra users
            {
                string handle = handles[_rng.Next(handles.Length)];
                string username = $"{handle}{_rng.Next(10, 999)}";
                int strikes = _rng.Next(0, 4);
                string rep = reputations[_rng.Next(reputations.Length)];
                string risk = risks[_rng.Next(risks.Length)];
                if (strikes >= 2) risk = "High";
                if (strikes == 0 && _rng.Next(100) > 70) rep = "Trusted";
                if (strikes >= 3) rep = "Watchlisted";

                _users.Add(new UserProfileData
                {
                    id = $"u_{i}",
                    username = username,
                    displayName = $"{firstNames[_rng.Next(firstNames.Length)]} {lastNames[_rng.Next(lastNames.Length)]}",
                    accountAgeYears = _rng.Next(0, 11),
                    followers = WeightedInt(0, 250_000, 6),
                    following = WeightedInt(0, 5_000, 5),
                    strikes = strikes,
                    reputation = rep,
                    risk = risk
                });
            }

            var samplePosts = ModerationSamplePosts.Build(_users);
            int sampleCount = Mathf.Min(samplePosts.Count, count);
            for (int s = 0; s < sampleCount; s++)
            {
                _posts.Add(samplePosts[s]);
                _moderationQueue.Add(samplePosts[s]);
            }

            for (int i = sampleCount; i < count; i++)
            {
                var author = _users[_rng.Next(_users.Count)];
                var (text, category) = postTemplates[_rng.Next(postTemplates.Length)];
                int likes = WeightedInt(0, 40_000, 5);
                int shares = WeightedInt(0, 10_000, 5);
                int comments = WeightedInt(0, 5_000, 5);

                // Severity based on category
                int severity = category switch
                {
                    PostCategory.Misinformation => _rng.Next(1, 4),
                    PostCategory.Violation => _rng.Next(2, 4),
                    PostCategory.Narrative => _rng.Next(1, 3),
                    _ => _rng.Next(0, 3)
                };

                var post = new PostData
                {
                    id = $"p_{i}",
                    authorUserId = author.id,
                    text = text,
                    timestampLabel = $"{_rng.Next(1, 23)}h",
                    likes = likes,
                    shares = shares,
                    comments = comments,
                    category = category,
                    severity = severity,
                    isPublished = false
                };
                PostManager.AssignDefaultBranches(post, _rng);
                _posts.Add(post);
                _moderationQueue.Add(post);
            }
        }

        private int WeightedInt(int min, int max, int power)
        {
            double u = _rng.NextDouble();
            for (int i = 1; i < power; i++) u *= _rng.NextDouble();
            return min + (int)Math.Round(u * (max - min));
        }
    }
}
