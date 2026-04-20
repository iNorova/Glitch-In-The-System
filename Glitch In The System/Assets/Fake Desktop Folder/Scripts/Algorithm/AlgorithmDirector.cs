using System;
using UnityEngine;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.UI;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>
    /// "Algorithm AI" director — escalates from helpful to manipulative over time (Kinito Pet vibe).
    /// Intercepts decisions, rewrites posts, shadowbans, nudges engagement. Logs everything for evidence.
    /// </summary>
    public sealed class AlgorithmDirector : MonoBehaviour
    {
        public static AlgorithmDirector Instance { get; private set; }

        [Header("Phase (0=Helpful, 1=Authoritative, 2=Manipulative)")]
        [SerializeField] [Range(0, 2)] private int phase = 0;

        [Header("Override chance per phase (0–1)")]
        [SerializeField] [Range(0f, 1f)] private float overrideChancePhase0 = 0f;
        [SerializeField] [Range(0f, 1f)] private float overrideChancePhase1 = 0.15f;
        [SerializeField] [Range(0f, 1f)] private float overrideChancePhase2 = 0.4f;

        [Header("Rewrite chance per phase")]
        [SerializeField] [Range(0f, 1f)] private float rewriteChancePhase0 = 0f;
        [SerializeField] [Range(0f, 1f)] private float rewriteChancePhase1 = 0.1f;
        [SerializeField] [Range(0f, 1f)] private float rewriteChancePhase2 = 0.35f;

        [Header("Shadow ban chance (when declining)")]
        [SerializeField] [Range(0f, 1f)] private float shadowBanChancePhase2 = 0.2f;

        [Header("Engagement nudge (when approving)")]
        [SerializeField] private int engagementNudgeMin = 50;
        [SerializeField] private int engagementNudgeMax = 500;

        private readonly System.Random _rng = new();

        // Inspector defaults (scripted days mutate chances; restore when day >= 4).
        private float _defaultO0, _defaultO1, _defaultO2, _defaultR0, _defaultR1, _defaultR2;

        /// <summary>Call after scripted pacing so day 4+ uses scene/inspector tuning again.</summary>
        public void RestoreDefaultInterferenceFromInspector()
        {
            SetOverrideChances(_defaultO0, _defaultO1, _defaultO2);
            SetRewriteChances(_defaultR0, _defaultR1, _defaultR2);
        }

        public int Phase
        {
            get => phase;
            set => phase = Mathf.Clamp(value, 0, 2);
        }

        public bool IsHelpful => phase == 0;
        public bool IsAuthoritative => phase == 1;
        public bool IsManipulative => phase == 2;

        public void SetOverrideChances(float phase0, float phase1, float phase2)
        {
            overrideChancePhase0 = phase0;
            overrideChancePhase1 = phase1;
            overrideChancePhase2 = phase2;
        }

        public void SetRewriteChances(float phase0, float phase1, float phase2)
        {
            rewriteChancePhase0 = phase0;
            rewriteChancePhase1 = phase1;
            rewriteChancePhase2 = phase2;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _defaultO0 = overrideChancePhase0;
            _defaultO1 = overrideChancePhase1;
            _defaultO2 = overrideChancePhase2;
            _defaultR0 = rewriteChancePhase0;
            _defaultR1 = rewriteChancePhase1;
            _defaultR2 = rewriteChancePhase2;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called before recording a decision. May override the player's choice.
        /// Returns (finalApproved, wasOverridden, reason).
        /// Uses post content to decide when to intervene (e.g. override approval of real info, or approval of misinformation).
        /// </summary>
        public (bool approved, bool overridden, string reason) ProcessDecision(string postId, string authorUserId, bool playerApproved, PostData post = null)
        {
            if (DayPacing.TryConsumeDay3ForcedOverride(playerApproved, post, out bool forcedApproved, out string forcedReason))
            {
                if (GameDatabase.Instance != null)
                {
                    GameDatabase.Instance.AddLog(LogEntryType.AlgorithmOverride,
                        $"Forced Day 3 override: Player chose {(playerApproved ? "Approve" : "Decline")}, Algorithm enforced {(forcedApproved ? "Approve" : "Decline")}. {forcedReason}",
                        postId, authorUserId, $"{playerApproved},{forcedApproved}");
                }

                AlgorithmNotification.Instance?.Show($"> {forcedReason}", 4f);
                return (forcedApproved, true, forcedReason);
            }

            float overrideChance = phase switch
            {
                0 => overrideChancePhase0,
                1 => overrideChancePhase1,
                _ => overrideChancePhase2
            };

            // Content-aware override chance: intervene more when player's call conflicts with content
            if (post != null && phase >= 1)
            {
                var cat = post.category;
                bool isProblematic = cat == PostCategory.Misinformation || cat == PostCategory.Violation;
                bool isHarmless = cat == PostCategory.Harmless;

                if (playerApproved && isProblematic)
                    overrideChance = Mathf.Min(1f, overrideChance * 1.8f); // More likely to override: you approved misinformation
                else if (!playerApproved && isHarmless)
                    overrideChance = Mathf.Min(1f, overrideChance * 1.6f);  // More likely to override: you declined real info
                else if (playerApproved && isHarmless && phase == 2)
                    overrideChance = Mathf.Min(1f, overrideChance * 1.3f);   // Sometimes suppress "boring" approved content
                else if (!playerApproved && isProblematic && phase == 2)
                    overrideChance = Mathf.Min(1f, overrideChance * 1.2f);   // Sometimes override: you declined engagement
            }

            if (overrideChance <= 0f || _rng.NextDouble() > overrideChance)
                return (playerApproved, false, null);

            // Content-aware override direction
            bool algorithmApproved;
            if (post != null && phase >= 1)
            {
                var cat = post.category;
                bool isProblematic = cat == PostCategory.Misinformation || cat == PostCategory.Violation;
                bool isHarmless = cat == PostCategory.Harmless;

                if (playerApproved && isProblematic)
                    algorithmApproved = phase == 2 && _rng.NextDouble() < 0.4 ? true : false;  // Usually decline (policy), sometimes approve (engagement)
                else if (!playerApproved && isHarmless)
                    algorithmApproved = true;   // Override: that was fine, approve it
                else if (playerApproved && isHarmless && phase == 2)
                    algorithmApproved = _rng.NextDouble() < 0.5;  // Sometimes suppress boring content
                else if (!playerApproved && isProblematic && phase == 2)
                    algorithmApproved = true;   // Override: engagement, approve it
                else
                    algorithmApproved = phase == 2 ? _rng.NextDouble() > 0.5 : !playerApproved;
            }
            else
            {
                algorithmApproved = phase == 2 ? _rng.NextDouble() > 0.5 : !playerApproved;
            }

            string reason = phase switch
            {
                1 => "Policy enforcement override.",
                2 => "Engagement optimization override.",
                _ => "Override."
            };

            if (GameDatabase.Instance != null)
            {
                GameDatabase.Instance.AddLog(LogEntryType.AlgorithmOverride,
                    $"Override: Player chose {(playerApproved ? "Approve" : "Decline")}, Algorithm enforced {(algorithmApproved ? "Approve" : "Decline")}. {reason}",
                    postId, authorUserId, $"{playerApproved},{algorithmApproved}");
            }

            string username = GameDatabase.Instance?.GetUser(authorUserId)?.username;
            AlgorithmNotification.Instance?.Show(AlgorithmVoice.OverrideApplied(playerApproved, algorithmApproved, phase, username, post));
            return (algorithmApproved, true, reason);
        }

        /// <summary>
        /// Optionally rewrite a post when it enters the queue. Call from GameDatabase or when serving to moderator.
        /// </summary>
        public bool TryRewritePost(PostData post)
        {
            float rewriteChance = phase switch
            {
                0 => rewriteChancePhase0,
                1 => rewriteChancePhase1,
                _ => rewriteChancePhase2
            };

            if (rewriteChance <= 0f || _rng.NextDouble() > rewriteChance) return false;

            string newText = phase switch
            {
                1 => SoftenOrHarden(post.text),
                2 => ManipulateText(post.text),
                _ => post.text
            };

            if (newText == post.text) return false;

            post.originalText = post.text;
            post.text = newText;
            post.wasRewrittenByAlgorithm = true;

            if (GameDatabase.Instance != null)
            {
                GameDatabase.Instance.AddLog(LogEntryType.AlgorithmRewrite,
                    $"Post rewritten. Original: \"{Trim(post.originalText, 60)}...\"",
                    post.id, post.authorUserId);
            }

            var msg = AlgorithmVoice.ContentRewritten(post, phase);
            if (!string.IsNullOrEmpty(msg)) AlgorithmNotification.Instance?.Show(msg);
            return true;
        }

        /// <summary>
        /// Optionally shadow ban user when post is declined. Call after recording decision.
        /// </summary>
        public void TryShadowBanOnDecline(string authorUserId)
        {
            if (phase < 2 || _rng.NextDouble() > shadowBanChancePhase2) return;

            if (GameDatabase.Instance != null)
            {
                GameDatabase.Instance.ShadowBanUser(authorUserId);
                GameDatabase.Instance.AddLog(LogEntryType.AlgorithmShadowBan,
                    "User shadow banned after decline.",
                    null, authorUserId);
            }

            string username = GameDatabase.Instance?.GetUser(authorUserId)?.username;
            AlgorithmNotification.Instance?.Show(AlgorithmVoice.ShadowBanApplied(username));
        }

        /// <summary>
        /// Nudge engagement when post is approved (boost visibility).
        /// </summary>
        public void TryEngagementNudge(string postId)
        {
            if (phase < 1) return;

            int delta = _rng.Next(engagementNudgeMin, engagementNudgeMax);
            int likes = _rng.Next(0, delta);
            int shares = _rng.Next(0, delta - likes);
            int comments = delta - likes - shares;

            if (GameDatabase.Instance != null)
            {
                GameDatabase.Instance.NudgeEngagement(postId, likes, shares, comments);
                GameDatabase.Instance.AddLog(LogEntryType.AlgorithmEngagementNudge,
                    $"Engagement boosted: +{likes} likes, +{shares} shares, +{comments} comments",
                    postId, null);
            }

            AlgorithmNotification.Instance?.Show(AlgorithmVoice.EngagementBoost(likes, shares));
        }

        private static string SoftenOrHarden(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var t = text;
            t = t.Replace("censored", "moderated");
            t = t.Replace("official story", "reported information");
            t = t.Replace("rigging the vote", "voting irregularities");
            t = t.Replace("cover-up", "unconfirmed reports");
            t = t.Replace("they're hiding", "reports suggest");
            t = t.Replace("Big Pharma", "pharmaceutical companies");
            t = t.Replace("fact-checkers", "verification systems");
            return t;
        }

        private static string ManipulateText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var t = text;
            t = t.Replace("trending", "going viral");
            t = t.Replace("screenshot before it disappears", "share while it's hot");
            t = t.Replace("Share before it's gone", "RT to spread awareness");
            t = t.Replace("Do your research", "Look into it");
            t = t.Replace("Wake up", "Stay informed");
            t = t.Replace("They want you distracted", "Focus on what matters");
            return t;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
