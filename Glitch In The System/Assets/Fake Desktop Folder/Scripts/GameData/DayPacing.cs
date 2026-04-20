using System.Collections.Generic;
using GlitchInTheSystem.Algorithm;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Central place for day 1–3 pacing: post counts, algorithm softness, PlayerPrefs carryover, one Day 3 override.
    /// </summary>
    public static class DayPacing
    {
        public const string PlayerPrefsViralSpread = "GITMS_ViralSpread";
        public const string Day3OverridePostId = "p_d3_override";
        public const string Day3CarryoverPostId = "p_d3_carryover";

        private static bool _day3ForcedOverrideConsumed;

        public static bool IsScriptedDay(int day) => day >= 1 && day <= 3;

        public static void ResetSessionState()
        {
            _day3ForcedOverrideConsumed = false;
        }

        /// <summary>Starting Day 1 clears saved viral outcome so a new run doesn’t inherit old consequences.</summary>
        public static void ApplySessionStartPlayerPrefs(GameDatabaseConfig config)
        {
            if (config == null) return;
            if (config.currentDay <= 1)
                PlayerPrefs.SetInt(PlayerPrefsViralSpread, 0);
        }

        /// <summary>Sets posts-per-day for early days and configures AlgorithmDirector (no overrides on days 1–2; day 3 uses one forced override only).</summary>
        public static void ApplyProfile(GameDatabaseConfig config, AlgorithmDirector algorithm)
        {
            if (config == null) return;
            int day = Mathf.Clamp(config.currentDay, 1, 999);

            if (day == 1)
                config.postsPerDay = 9;
            else if (day == 2)
                config.postsPerDay = 11;
            else if (day == 3)
                config.postsPerDay = 13;

            if (algorithm == null) return;

            // Day 1: tutorial — phase 0, zero interference.
            if (day == 1)
            {
                algorithm.Phase = 0;
                algorithm.SetOverrideChances(0f, 0f, 0f);
                algorithm.SetRewriteChances(0f, 0f, 0f);
                return;
            }

            // Day 2: doubt — phase 1 for subtle engagement nudges + rare soft rewrites; still no random overrides.
            if (day == 2)
            {
                algorithm.Phase = 1;
                algorithm.SetOverrideChances(0f, 0f, 0f);
                algorithm.SetRewriteChances(0f, 0.06f, 0.08f);
                return;
            }

            // Day 3: manipulation intro — phase 1, rare random overrides off; forced override handled in ProcessDecision.
            if (day == 3)
            {
                algorithm.Phase = 1;
                algorithm.SetOverrideChances(0f, 0f, 0f);
                algorithm.SetRewriteChances(0f, 0.1f, 0.12f);
                return;
            }

            // Day 4+ — use config phase; restore inspector curve (scripted days zeroed chances on the component).
            algorithm.Phase = Mathf.Clamp(config.algorithmPhase, 0, 2);
            algorithm.RestoreDefaultInterferenceFromInspector();
        }

        /// <summary>After moderating the viral post on Day 2, persist whether it went live (for Day 3 consequence).</summary>
        public static void PersistDay2ViralOutcome(int currentDay, string postId, bool finalApproved)
        {
            if (currentDay != 2) return;
            if (postId != NarrativeIds.ViralMisinformationPostId) return;
            PlayerPrefs.SetInt(PlayerPrefsViralSpread, finalApproved ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>If Day 3 and specific post: one guaranteed algorithm override (player declined harmless → platform approves).</summary>
        public static bool TryConsumeDay3ForcedOverride(bool playerApproved, PostData post, out bool algorithmApproved, out string reason)
        {
            algorithmApproved = playerApproved;
            reason = null;
            if (GameDatabase.Instance?.Config == null || post == null) return false;
            if (GameDatabase.Instance.Config.currentDay != 3) return false;
            if (post.id != Day3OverridePostId) return false;
            if (_day3ForcedOverrideConsumed) return false;

            // Only intervene if the player blocks harmless “positive local” content.
            if (!playerApproved && (post.category == PostCategory.Harmless || post.category == PostCategory.GrayArea))
            {
                _day3ForcedOverrideConsumed = true;
                algorithmApproved = true;
                reason = "Engagement policy: positive local content surfaced.";
                return true;
            }

            return false;
        }

        /// <summary>If the player let the Day 2 viral memo spread, seed a dark follow-up in the feed (not in the queue).</summary>
        public static void RegisterDay3CarryoverIfNeeded(GameDatabase db, System.Collections.Generic.IReadOnlyList<UserProfileData> users)
        {
            if (db == null || users == null || users.Count == 0) return;
            if (db.Config == null || db.Config.currentDay != 3) return;
            if (PlayerPrefs.GetInt(PlayerPrefsViralSpread, 0) != 1) return;

            var post = new PostData
            {
                id = Day3CarryoverPostId,
                authorUserId = users[0].id,
                text = "Day 3 update: ER visits tied to water panic are still above normal. Officials repeat — municipal water met safety specs; the viral memo was fake.",
                timestampLabel = "28m",
                category = PostCategory.Narrative,
                severity = 1,
                isPublished = true,
                isRemoved = false,
                feedRank = 97,
                likesApprove = 62_000,
                likesDecline = 0,
                commentsApprove = new List<string>
                {
                    "We warned yesterday this would happen.",
                    "Mods on Day 2 let a hoax go mega-viral. Look at the charts.",
                    "Real nurses, real exhaustion. Thanks algorithm.",
                    "Can we pin accountability posts?",
                    "This timeline is receipts."
                },
                commentsDecline = new List<string>()
            };

            db.TryAddNarrativePost(post);
            PostManager.ApplyDecisionReaction(post, true, users);
        }
    }
}
