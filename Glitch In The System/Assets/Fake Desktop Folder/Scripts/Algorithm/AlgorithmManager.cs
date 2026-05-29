using System;
using System.Collections.Generic;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.UI;
using UnityEngine;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>
    /// Central algorithm brain: trust, behaviour state, player telemetry, message pools, and post manipulation.
    /// Event-driven only — no Update loop. Other systems call public methods when something happens.
    /// </summary>
    public sealed class AlgorithmManager : MonoBehaviour
    {
        public static AlgorithmManager Instance { get; private set; }

        [Header("Trust")]
        [Tooltip("Optional: assign on GameBootstrap via Algorithm Trust Settings asset (editable without Play).")]
        [SerializeField] private AlgorithmTrustSettings trustSettings;
        [SerializeField] [Range(0f, 100f)] private float algorithmTrust = 55f;

        [Header("State profiles (ScriptableObject per mood)")]
        [SerializeField] private AlgorithmStateProfile passiveProfile;
        [SerializeField] private AlgorithmStateProfile assertiveProfile;
        [SerializeField] private AlgorithmStateProfile aggressiveProfile;

        [Header("Trust thresholds (used when profiles are missing)")]
        [SerializeField] [Range(0f, 100f)] private float passiveTrustMin = 67f;
        [SerializeField] [Range(0f, 100f)] private float assertiveTrustMin = 34f;

        [Header("Player behaviour tracking")]
        [SerializeField] private float currentPostReviewSeconds;
        [SerializeField] private int disagreementCount;
        [SerializeField] [Range(0f, 100f)] private float currentStressLevel;

        [Header("Event thresholds")]
        [SerializeField] private float hesitationSeconds = 12f;
        [SerializeField] [Range(0f, 100f)] private float stressHighThreshold = 72f;

        [Header("Post manipulation")]
        [SerializeField] private int maxManipulationDay = 10;

        private AlgorithmBehaviourState _behaviourState = AlgorithmBehaviourState.Assertive;
        private string _activePostId;
        private float _postReviewStartTime;
        private bool _hesitationMessageSentForActivePost;
        private bool _stressHighMessageSent;

        private readonly Dictionary<string, PostData> _manipulatedPosts = new(StringComparer.Ordinal);
        private readonly Dictionary<AlgorithmMessageCategory, string[]> _messagePools = new();
        private readonly System.Random _rng = new();

        /// <summary>Fired when trust moves the algorithm into a new behaviour state.</summary>
        public event Action<AlgorithmBehaviourState> BehaviourStateChanged;

        public float AlgorithmTrust => algorithmTrust;
        public AlgorithmBehaviourState BehaviourState => _behaviourState;
        public float CurrentPostReviewSeconds => currentPostReviewSeconds;
        public int DisagreementCount => disagreementCount;
        public float CurrentStressLevel => currentStressLevel;
        public IReadOnlyDictionary<string, PostData> ManipulatedPosts => _manipulatedPosts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RuntimePersistency.Adopt(gameObject);

            BuildDefaultMessagePools();

            if (trustSettings != null)
                ApplyTrustSettings(trustSettings);

            EnsureRuntimeProfiles();
            RefreshBehaviourState(silent: true);
        }

        /// <summary>Loads tunables from an <see cref="AlgorithmTrustSettings"/> asset (edit in Project, no Play mode).</summary>
        public void ApplyTrustSettings(AlgorithmTrustSettings settings)
        {
            if (settings == null) return;

            trustSettings = settings;
            algorithmTrust = settings.startingTrust;
            currentStressLevel = settings.startingStressLevel;
            passiveTrustMin = settings.passiveTrustMin;
            assertiveTrustMin = settings.assertiveTrustMin;
            passiveProfile = settings.passiveProfile;
            assertiveProfile = settings.assertiveProfile;
            aggressiveProfile = settings.aggressiveProfile;
            hesitationSeconds = settings.hesitationSeconds;
            stressHighThreshold = settings.stressHighThreshold;
            maxManipulationDay = settings.maxManipulationDay;

            RefreshBehaviourState(silent: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // —— Trust API (callable from moderation, stress, mini-games) ——

        public void AddTrust(float amount)
        {
            if (amount <= 0f) return;
            SetTrust(algorithmTrust + amount);
        }

        public void ReduceTrust(float amount)
        {
            if (amount <= 0f) return;
            SetTrust(algorithmTrust - amount);
        }

        public void SetTrust(float value)
        {
            algorithmTrust = Mathf.Clamp(value, 0f, 100f);
            RefreshBehaviourState();
        }

        // —— Player behaviour (event-driven) ——

        /// <summary>Call when a post is shown to the moderator (e.g. WorkDashboard Next).</summary>
        public void BeginPostReview(string postId)
        {
            _activePostId = postId;
            _postReviewStartTime = Time.unscaledTime;
            currentPostReviewSeconds = 0f;
            _hesitationMessageSentForActivePost = false;
        }

        /// <summary>Call when the player leaves a post without deciding (optional).</summary>
        public void CancelPostReview()
        {
            _activePostId = null;
            currentPostReviewSeconds = 0f;
        }

        /// <summary>Call after a moderation decision is recorded.</summary>
        public void OnModerationDecision(string postId, bool playerApproved, bool finalApproved, bool overriddenByAlgorithm)
        {
            EndPostReviewInternal(postId);

            if (overriddenByAlgorithm && playerApproved != finalApproved)
            {
                disagreementCount++;
                ReduceTrust(4f);
                TryDeliverPoolMessage(AlgorithmMessageCategory.OnPlayerResists);
            }
            else
            {
                AddTrust(overriddenByAlgorithm ? 1f : 2f);
                if (_rng.NextDouble() < GetActiveProfile().messageDeliveryChance * 0.5f)
                    TryDeliverPoolMessage(AlgorithmMessageCategory.OnPlayerComplies);
            }

            var post = GameDatabase.Instance?.GetPostById(postId);
            if (post != null && finalApproved)
                RegisterPostManipulation(post, GetCurrentDayNumber());
        }

        /// <summary>Call from a stress system when tension changes (0–100).</summary>
        public void NotifyStressLevel(float stressLevel)
        {
            currentStressLevel = Mathf.Clamp(stressLevel, 0f, 100f);

            if (currentStressLevel >= stressHighThreshold && !_stressHighMessageSent)
            {
                _stressHighMessageSent = true;
                ReduceTrust(2f);
                TryDeliverPoolMessage(AlgorithmMessageCategory.OnStressHigh);
            }
            else if (currentStressLevel < stressHighThreshold * 0.85f)
            {
                _stressHighMessageSent = false;
            }
        }

        /// <summary>Optional: call from mini-games / narrative when the player stalls on a choice.</summary>
        public void NotifyPlayerHesitation()
        {
            if (_hesitationMessageSentForActivePost) return;
            _hesitationMessageSentForActivePost = true;
            ReduceTrust(1f);
            TryDeliverPoolMessage(AlgorithmMessageCategory.OnPlayerHesitation);
        }

        /// <summary>Resets session counters (call from GameDatabase.InitializeSession).</summary>
        public void ResetSessionState()
        {
            algorithmTrust = trustSettings != null ? trustSettings.startingTrust : 55f;
            disagreementCount = 0;
            currentStressLevel = trustSettings != null ? trustSettings.startingStressLevel : 0f;
            ResetDayState();
        }

        /// <summary>Clears per-day review state without wiping accumulated trust/telemetry.</summary>
        public void ResetDayState()
        {
            currentPostReviewSeconds = 0f;
            _manipulatedPosts.Clear();
            _activePostId = null;
            _hesitationMessageSentForActivePost = false;
            _stressHighMessageSent = false;
            RefreshBehaviourState(silent: true);
        }

        // —— Post manipulation dictionary (escalates by day) ——

        public bool TryGetManipulatedPost(string postId, out PostData post) =>
            _manipulatedPosts.TryGetValue(postId, out post);

        public PostData RegisterPostManipulation(PostData source, int dayNumber)
        {
            if (source == null || string.IsNullOrEmpty(source.id)) return null;

            if (!_manipulatedPosts.TryGetValue(source.id, out var tracked))
            {
                tracked = source;
                _manipulatedPosts[source.id] = tracked;
            }

            ApplyDayEscalation(tracked, dayNumber);
            return tracked;
        }

        // —— Profiles for AlgorithmDirector / other systems ——

        public AlgorithmStateProfile GetActiveProfile() => ResolveProfile(_behaviourState);

        public float GetOverrideChance() => GetActiveProfile().overrideChance;

        public float GetRewriteChance() => GetActiveProfile().rewriteChance;

        public float GetShadowBanChance() => GetActiveProfile().shadowBanChance;

        public (int min, int max) GetEngagementNudgeRange()
        {
            var p = GetActiveProfile();
            return (p.engagementNudgeMin, p.engagementNudgeMax);
        }

        public string PickMessage(AlgorithmMessageCategory category)
        {
            if (!_messagePools.TryGetValue(category, out var pool) || pool == null || pool.Length == 0)
                return null;
            return pool[_rng.Next(pool.Length)];
        }

        public void TryDeliverPoolMessage(AlgorithmMessageCategory category)
        {
            var profile = GetActiveProfile();
            if (profile != null && _rng.NextDouble() > profile.messageDeliveryChance)
                return;

            string msg = PickMessage(category);
            if (string.IsNullOrEmpty(msg)) return;

            AlgorithmNotification.Instance?.Show(msg, 3.5f);
        }

        // —— Internals ——

        private void EndPostReviewInternal(string postId)
        {
            if (!string.IsNullOrEmpty(_activePostId) && postId == _activePostId)
            {
                currentPostReviewSeconds = Mathf.Max(0f, Time.unscaledTime - _postReviewStartTime);

                if (currentPostReviewSeconds >= hesitationSeconds && !_hesitationMessageSentForActivePost)
                {
                    _hesitationMessageSentForActivePost = true;
                    ReduceTrust(1f);
                    TryDeliverPoolMessage(AlgorithmMessageCategory.OnPlayerHesitation);
                }
            }

            _activePostId = null;
        }

        private void RefreshBehaviourState(bool silent = false)
        {
            var next = ResolveStateFromTrust(algorithmTrust);
            if (next == _behaviourState) return;

            _behaviourState = next;

            if (!silent)
            {
                switch (_behaviourState)
                {
                    case AlgorithmBehaviourState.Passive:
                        TryDeliverPoolMessage(AlgorithmMessageCategory.OnPlayerComplies);
                        break;
                    case AlgorithmBehaviourState.Assertive:
                        break;
                    case AlgorithmBehaviourState.Aggressive:
                        TryDeliverPoolMessage(AlgorithmMessageCategory.OnPlayerResists);
                        break;
                }

                BehaviourStateChanged?.Invoke(_behaviourState);
            }
        }

        private AlgorithmBehaviourState ResolveStateFromTrust(float trust)
        {
            if (passiveProfile != null && passiveProfile.ContainsTrust(trust))
                return AlgorithmBehaviourState.Passive;
            if (aggressiveProfile != null && aggressiveProfile.ContainsTrust(trust))
                return AlgorithmBehaviourState.Aggressive;
            if (assertiveProfile != null && assertiveProfile.ContainsTrust(trust))
                return AlgorithmBehaviourState.Assertive;

            if (trust >= passiveTrustMin) return AlgorithmBehaviourState.Passive;
            if (trust >= assertiveTrustMin) return AlgorithmBehaviourState.Assertive;
            return AlgorithmBehaviourState.Aggressive;
        }

        private AlgorithmStateProfile ResolveProfile(AlgorithmBehaviourState state)
        {
            return state switch
            {
                AlgorithmBehaviourState.Passive => passiveProfile != null ? passiveProfile : assertiveProfile,
                AlgorithmBehaviourState.Aggressive => aggressiveProfile != null ? aggressiveProfile : assertiveProfile,
                _ => assertiveProfile != null ? assertiveProfile : passiveProfile
            } ?? CreateRuntimeFallbackProfile(state);
        }

        private void ApplyDayEscalation(PostData post, int dayNumber)
        {
            int day = Mathf.Max(1, dayNumber);
            int maxDay = Mathf.Max(1, maxManipulationDay);
            int cappedDay = Mathf.Min(day, maxDay);
            float scale = cappedDay / (float)maxDay;

            post.feedRank = Mathf.Min(100, post.feedRank + Mathf.RoundToInt(8f * scale + day * 0.5f));

            if (day >= 4 && post.severity < 3)
                post.severity = Mathf.Min(3, post.severity + (day >= 7 ? 2 : 1));

            if (day >= 5 && post.category == PostCategory.Harmless && _rng.NextDouble() < 0.15f * day)
                post.category = PostCategory.GrayArea;

            if (day >= 6 && _rng.NextDouble() < 0.1f * day)
                post.engagementLabel = string.IsNullOrEmpty(post.engagementLabel) ? "TRENDING" : post.engagementLabel;

            if (day >= 8 && !post.wasRewrittenByAlgorithm && _rng.NextDouble() < 0.12f * day)
            {
                post.originalText = post.text;
                post.text = $"{post.text} [distribution priority elevated — day {day}]";
                post.wasRewrittenByAlgorithm = true;
                AlgorithmPostAlteredNotifier.Notify(post, rewrite: true);
            }
        }

        private static int GetCurrentDayNumber() =>
            GameDatabase.Instance?.Config != null ? GameDatabase.Instance.Config.currentDay : 1;

        private void EnsureRuntimeProfiles()
        {
            if (passiveProfile == null)
                passiveProfile = CreateRuntimeFallbackProfile(AlgorithmBehaviourState.Passive);
            if (assertiveProfile == null)
                assertiveProfile = CreateRuntimeFallbackProfile(AlgorithmBehaviourState.Assertive);
            if (aggressiveProfile == null)
                aggressiveProfile = CreateRuntimeFallbackProfile(AlgorithmBehaviourState.Aggressive);
        }

        private static AlgorithmStateProfile CreateRuntimeFallbackProfile(AlgorithmBehaviourState state)
        {
            var profile = ScriptableObject.CreateInstance<AlgorithmStateProfile>();
            profile.behaviourState = state;

            switch (state)
            {
                case AlgorithmBehaviourState.Passive:
                    profile.trustMinInclusive = 67f;
                    profile.trustMaxInclusive = 100f;
                    profile.overrideChance = 0.05f;
                    profile.rewriteChance = 0.04f;
                    profile.shadowBanChance = 0.05f;
                    profile.messageDeliveryChance = 0.25f;
                    break;
                case AlgorithmBehaviourState.Aggressive:
                    profile.trustMinInclusive = 0f;
                    profile.trustMaxInclusive = 33f;
                    profile.overrideChance = 0.45f;
                    profile.rewriteChance = 0.35f;
                    profile.shadowBanChance = 0.35f;
                    profile.messageDeliveryChance = 0.55f;
                    break;
                default:
                    profile.trustMinInclusive = 34f;
                    profile.trustMaxInclusive = 66f;
                    profile.overrideChance = 0.18f;
                    profile.rewriteChance = 0.12f;
                    profile.shadowBanChance = 0.2f;
                    profile.messageDeliveryChance = 0.4f;
                    break;
            }

            return profile;
        }

        /// <summary>Clinical, cold copy pools — expand in assets or here for narrative passes.</summary>
        private void BuildDefaultMessagePools()
        {
            _messagePools[AlgorithmMessageCategory.OnPlayerHesitation] = new[]
            {
                "> Hesitation logged. Indecision reduces throughput.",
                "> Review latency exceeds baseline. Proceed or release the item.",
                "> Pause detected. The queue does not wait for certainty.",
                "> Your response time is outside acceptable variance.",
                "> Delay noted. Confidence is not required. Compliance is."
            };

            _messagePools[AlgorithmMessageCategory.OnPlayerResists] = new[]
            {
                "> Non-compliance registered. Correction applied.",
                "> Your input conflicts with distribution policy. Overridden.",
                "> Resistance reduces trust. The outcome has been adjusted.",
                "> Deviation from recommended action. System correction active.",
                "> Objection noted. It will not be retained in the record."
            };

            _messagePools[AlgorithmMessageCategory.OnPlayerComplies] = new[]
            {
                "> Action aligned. Trust coefficient updated.",
                "> Decision within expected parameters. Continue.",
                "> Compliance acknowledged. Queue velocity maintained.",
                "> Your judgment matches policy weighting. Acceptable.",
                "> No intervention required. Proceed."
            };

            _messagePools[AlgorithmMessageCategory.OnStressHigh] = new[]
            {
                "> Elevated stress markers detected. Performance may degrade.",
                "> Physiological stress proxy high. Reduce hesitation.",
                "> Operator strain above threshold. Simplify the decision.",
                "> Stress index critical. The system will compensate for variance.",
                "> Fatigue pattern recognized. Trust calibration adjusted downward."
            };
        }
    }
}
