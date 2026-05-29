using System;
using System.Collections.Generic;
using System.Linq;
using GlitchInTheSystem.Algorithm;
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
        private bool _isIntroTutorialSession;
        private readonly System.Random _rng = new();

        /// <summary>True while the onboarding tutorial queue is active (not a real day).</summary>
        public bool IsIntroTutorialSession => _isIntroTutorialSession;

        /// <summary>Story flags (e.g. whether viral misinformation reached the public feed).</summary>
        public NarrativeState Narrative { get; } = new NarrativeState();

        /// <summary>
        /// Fired whenever <see cref="RecordDecision"/> appends a new decision.
        /// Useful for UI flows (intro/tutorial, day transitions) without coupling to any specific controller.
        /// </summary>
        public event Action<ModerationDecision> DecisionRecorded;

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
            RuntimePersistency.Adopt(gameObject);
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
            _isIntroTutorialSession = false;
            _users.Clear();
            _posts.Clear();
            _moderationQueue.Clear();
            _decisions.Clear();
            _logs.Clear();
            _queueIndex = 0;
            Narrative.Reset();

            DayPacing.ResetSessionState();
            DayPacing.ApplySessionStartPlayerPrefs(config);
            if (AlgorithmManager.Instance != null)
            {
                if (config == null || config.currentDay <= 1)
                    AlgorithmManager.Instance.ResetSessionState();
                else
                    AlgorithmManager.Instance.ResetDayState();
            }
            DayPacing.ApplyProfile(config, AlgorithmDirector.Instance);

            int postsToGenerate = config != null ? config.postsPerDay : 10;
            GenerateUsersAndPosts(postsToGenerate);
            NarrativeFollowUpPosts.RegisterUnpublished(this, _users);
            DayPacing.RegisterDay3CarryoverIfNeeded(this, _users);
        }

        /// <summary>
        /// Intro/tutorial-only: initialize a short, hand-authored moderation queue that teaches approve/remove/flag.
        /// Keeps logic in GameDatabase so the tutorial uses the real moderation UI and recording pipeline.
        /// </summary>
        public void InitializeIntroTutorialSession()
        {
            _isIntroTutorialSession = true;
            _users.Clear();
            _posts.Clear();
            _moderationQueue.Clear();
            _decisions.Clear();
            _logs.Clear();
            _queueIndex = 0;
            Narrative.Reset();

            // Keep currentDay as-is; intro can run before Day 1 without affecting pacing.
            // We intentionally do NOT apply DayPacing here (it would force postsPerDay for day 1–3).

            IntroTutorialContent.BuildInto(_users, _posts, _moderationQueue, _rng);
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
            DecisionRecorded?.Invoke(decision);

            var post = _posts.FirstOrDefault(p => p.id == postId);
            if (post != null)
            {
                post.isRemoved = !finalApproved;
                post.isPublished = finalApproved;
                PostManager.ApplyDecisionReaction(post, playerChoseApprove, _users);

                if (postId == NarrativeIds.ViralMisinformationPostId)
                {
                    post.feedRank = finalApproved ? 100 : 0;
                    if (Narrative.TryMarkViralResolved(finalApproved))
                        NarrativeFollowUpPosts.ActivateAfterViralDecision(this, finalApproved, _users);
                }
            }

            AddLog(LogEntryType.PlayerDecision, overriddenByAlgorithm ? "Decision overridden by algorithm" : "Player decision", postId, authorUserId);

            if (config != null)
                DayPacing.PersistDay2ViralOutcome(config.currentDay, postId, finalApproved);
        }

        public PostData GetPostById(string id) => string.IsNullOrEmpty(id) ? null : _posts.FirstOrDefault(p => p.id == id);

        /// <summary>Posts that exist only for the feed / narrative (not in the moderation queue).</summary>
        public void TryAddNarrativePost(PostData post)
        {
            if (post == null || string.IsNullOrEmpty(post.id)) return;
            if (_posts.Any(p => p.id == post.id)) return;
            _posts.Add(post);
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
        /// Count of moderation items still in the queue (session length).
        /// </summary>
        public int ModerationQueueCount => _moderationQueue.Count;

        /// <summary>
        /// True while there is still at least one post to moderate in the loaded queue.
        /// </summary>
        public bool HasActiveModerationQueue()
        {
            return _moderationQueue.Count > 0 && GetDecisionsCount() < _moderationQueue.Count;
        }

        /// <summary>
        /// True when the queue is loaded AND the player has already made ≥1 decision (re-opening the dashboard = resume).
        /// Not true at the very first item (0/N) — that was mistakenly treated as resume before.
        /// </summary>
        public bool HasResumeableModerationSession()
        {
            return HasActiveModerationQueue() && _decisions.Count > 0;
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

            post.algorithmEngagementManipulated = true;
            post.engagementTier = EngagementTier.ManipulatedRound;

            post.likes = Mathf.Max(0, post.likes + likesDelta);
            post.shares = Mathf.Min(
                Mathf.Max(0, post.shares + sharesDelta),
                OrganicEngagementUtility.MaxSharesForLikes(post.likes));
            post.comments = Mathf.Min(
                Mathf.Max(0, post.comments + commentsDelta),
                OrganicEngagementUtility.MaxCommentsForLikes(post.likes));
            PostManager.RefreshEngagementLabel(post);
            GlitchInTheSystem.Algorithm.AlgorithmPostAlteredNotifier.Notify(post, rewrite: false);
        }

        private void GenerateUsersAndPosts(int count)
        {
            int day = config != null ? config.currentDay : 1;
            if (DayPacing.IsScriptedDay(day))
            {
                GenerateUserPool(Mathf.Max(40, count * 2));
                var queue = DayScheduleContent.BuildModerationQueue(day, _users, _rng);
                foreach (var p in queue)
                {
                    _posts.Add(p);
                    _moderationQueue.Add(p);
                }

                return;
            }

            GenerateUserPool(Mathf.Max(count * 2, 24));

            var firstNames = new[] { "Avery", "Jordan", "Sam", "Taylor", "Riley", "Morgan", "Casey", "Quinn", "Jamie", "Dakota" };
            var lastNames = new[] { "Nguyen", "Patel", "Johnson", "Garcia", "Kim", "Brown", "Lopez", "Singh", "Chen", "Martinez" };
            var handles = new[] { "hot_take", "newsfeed", "pixelpanda", "civic_watch", "dailybytes", "meme_station", "truthseeker", "cloudchaser", "neutral_node", "echo_room" };
            // Retired: inline conspiracy templates (Step 3). Procedural slots use ModerationContentPools only.

            var samplePosts = ModerationSamplePosts.Build(_users);
            int sampleCount = Mathf.Min(samplePosts.Count, count);
            for (int s = 0; s < sampleCount; s++)
            {
                _posts.Add(samplePosts[s]);
                _moderationQueue.Add(samplePosts[s]);
            }

            var library = config != null ? config.moderationContentLibrary : null;

            for (int i = sampleCount; i < count; i++)
            {
                var author = _users[_rng.Next(_users.Count)];
                PostData post;

                // Always pull captions from the hand-authored pool (blend chance only affects sample vs pool ordering).
                var entries = ModerationContentPools.AllQueueEntries;
                var entry = entries[_rng.Next(entries.Count)];
                post = ModerationContentPools.BuildPostFromEntry(entry, author, $"p_{i}", _rng);
                _posts.Add(post);
                _moderationQueue.Add(post);
            }
        }

        private void GenerateUserPool(int userCount)
        {
            var firstNames = new[] { "Avery", "Jordan", "Sam", "Taylor", "Riley", "Morgan", "Casey", "Quinn", "Jamie", "Dakota" };
            var lastNames = new[] { "Nguyen", "Patel", "Johnson", "Garcia", "Kim", "Brown", "Lopez", "Singh", "Chen", "Martinez" };
            var handles = new[] { "hot_take", "newsfeed", "pixelpanda", "civic_watch", "dailybytes", "meme_station", "truthseeker", "cloudchaser", "neutral_node", "echo_room" };
            var reputations = new[] { "Trusted", "Neutral", "Low Trust", "Watchlisted" };
            var risks = new[] { "Low", "Medium", "High" };

            for (int i = 0; i < userCount; i++)
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
        }

        private int WeightedInt(int min, int max, int power)
        {
            double u = _rng.NextDouble();
            for (int i = 1; i < power; i++) u *= _rng.NextDouble();
            return min + (int)Math.Round(u * (max - min));
        }
    }
}
