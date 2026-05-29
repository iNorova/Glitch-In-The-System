using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Builds likes, shares, and comment counts as one unit from a single <see cref="EngagementTier"/> roll.
    /// Shares never exceed likes × 0.4; comment count never exceeds likes × 0.15.
    /// Round numbers are reserved for <see cref="EngagementTier.ManipulatedRound"/> (Algorithm / bot boost only).
    /// </summary>
    public static class OrganicEngagementUtility
    {
        public const float MaxShareRatio = 0.4f;
        public const float MaxCommentRatio = 0.15f;

        // Messy like counts per tier (pre-authored — not independent random metrics).
        private static readonly int[] IgnoredLikes = { 3, 7, 11, 14, 19, 23, 31, 38, 42 };
        private static readonly int[] NormalLikes = { 47, 58, 73, 91, 104, 127, 183, 256, 318, 447, 612, 891, 1204 };
        private static readonly int[] HeatedLikes = { 2387, 3412, 5419, 7823, 8733, 11204, 15202, 28441, 42107 };
        private static readonly int[] ViralLikes =
        {
            12437, 18933, 28441, 41207, 58912, 87331, 142880, 218441, 402119, 847293
        };

        // Algorithm / bot campaigns — intentionally round (Step 1 rule).
        private static readonly int[] ManipulatedRoundLikes = { 420, 8800, 12400, 45000, 134000, 210000 };
        private static readonly int[] ManipulatedRoundShares = { 40, 900, 2400, 12000, 28000 };
        private static readonly int[] ManipulatedRoundComments = { 12, 180, 800, 4200, 11000 };

        /// <summary>
        /// Rolls a tier from category probabilities, then applies cohesive metrics to <paramref name="post"/>.
        /// </summary>
        public static void ApplyToPost(PostData post, System.Random rng, PostCategory category)
        {
            if (post == null || rng == null) return;

            bool forceManipulated = post.algorithmEngagementManipulated;
            EngagementTier tier = forceManipulated
                ? EngagementTier.ManipulatedRound
                : RollTier(rng, category, post.feedKind);

            ApplyTier(post, tier, rng);
        }

        /// <summary>
        /// Applies a fixed tier (scripted days 1–3, narrative beats). Does not re-roll.
        /// </summary>
        public static void ApplyTier(PostData post, EngagementTier tier, System.Random rng)
        {
            if (post == null) return;

            if (tier == EngagementTier.ManipulatedRound && !post.algorithmEngagementManipulated)
            {
                // Safety: round numbers only when explicitly flagged — downgrade to Heated.
                tier = EngagementTier.Heated;
            }

            post.engagementTier = tier;

            if (tier == EngagementTier.ManipulatedRound)
            {
                ApplyManipulatedRoundMetrics(post, rng);
            }
            else
            {
                int likes = PickFromPool(rng, TierLikes(tier));
                ApplyCohesiveMetrics(post, likes, rng, messy: true);
            }

            post.likesApprove = Mathf.Max(post.likes, post.likesApprove);
            post.likesDecline = Mathf.Max(3, Mathf.Max(post.likesDecline, post.likes / 12));
            PostManager.RefreshEngagementLabel(post);
        }

        /// <summary>Category-weighted tier roll. Personal / harmless content rarely goes viral.</summary>
        public static EngagementTier RollTier(System.Random rng, PostCategory category, FeedPostKind feedKind = FeedPostKind.PersonalUpdate)
        {
            if (rng == null) return EngagementTier.Normal;

            int roll = rng.Next(0, 100);

            if (feedKind == FeedPostKind.EmotionalVent)
                return roll < 72 ? EngagementTier.Ignored : EngagementTier.Normal;

            switch (category)
            {
                case PostCategory.Harmless:
                    if (roll < 38) return EngagementTier.Ignored;
                    if (roll < 88) return EngagementTier.Normal;
                    if (roll < 97) return EngagementTier.Heated;
                    return EngagementTier.Viral;

                case PostCategory.GrayArea:
                    if (roll < 18) return EngagementTier.Ignored;
                    if (roll < 58) return EngagementTier.Normal;
                    if (roll < 88) return EngagementTier.Heated;
                    return EngagementTier.Viral;

                case PostCategory.Misinformation:
                case PostCategory.Narrative:
                    if (roll < 8) return EngagementTier.Normal;
                    if (roll < 35) return EngagementTier.Heated;
                    return EngagementTier.Viral;

                case PostCategory.Violation:
                    if (roll < 25) return EngagementTier.Ignored;
                    if (roll < 70) return EngagementTier.Normal;
                    return EngagementTier.Heated;

                case PostCategory.AlgorithmManipulation:
                    if (roll < 20) return EngagementTier.Normal;
                    if (roll < 75) return EngagementTier.Heated;
                    return EngagementTier.Viral;

                default:
                    if (roll < 30) return EngagementTier.Ignored;
                    if (roll < 80) return EngagementTier.Normal;
                    return EngagementTier.Heated;
            }
        }

        private static int[] TierLikes(EngagementTier tier) =>
            tier switch
            {
                EngagementTier.Ignored => IgnoredLikes,
                EngagementTier.Normal => NormalLikes,
                EngagementTier.Heated => HeatedLikes,
                EngagementTier.Viral => ViralLikes,
                _ => NormalLikes
            };

        private static void ApplyManipulatedRoundMetrics(PostData post, System.Random rng)
        {
            post.likes = PickFromPool(rng, ManipulatedRoundLikes);
            int maxShares = MaxSharesForLikes(post.likes);
            int maxComments = MaxCommentsForLikes(post.likes);

            int targetShares = PickFromPool(rng, ManipulatedRoundShares);
            int targetComments = PickFromPool(rng, ManipulatedRoundComments);

            post.shares = Mathf.Clamp(targetShares, 0, maxShares);
            post.comments = Mathf.Clamp(targetComments, 0, maxComments);
            post.engagementLabel = "TRENDING";
        }

        /// <summary>Sets likes first, then derives shares/comments with ratio caps and messy jitter.</summary>
        private static void ApplyCohesiveMetrics(PostData post, int likes, System.Random rng, bool messy)
        {
            likes = Mathf.Max(0, likes);
            post.likes = likes;

            int maxShares = MaxSharesForLikes(likes);
            int maxComments = MaxCommentsForLikes(likes);

            if (likes <= 0)
            {
                post.shares = 0;
                post.comments = 0;
                return;
            }

            float shareFraction = messy
                ? 0.04f + (float)rng.NextDouble() * 0.32f
                : 0.15f;
            float commentFraction = messy
                ? 0.02f + (float)rng.NextDouble() * 0.12f
                : 0.08f;

            post.shares = Mathf.Clamp(Mathf.RoundToInt(likes * shareFraction), 0, maxShares);
            post.comments = Mathf.Clamp(Mathf.RoundToInt(likes * commentFraction), 0, maxComments);

            if (messy && post.shares > 0)
                post.shares = JitterAwayFromRound(post.shares, rng, maxShares);
            if (messy && post.comments > 0)
                post.comments = JitterAwayFromRound(post.comments, rng, maxComments);
        }

        public static int MaxSharesForLikes(int likes) =>
            Mathf.FloorToInt(Mathf.Max(0, likes) * MaxShareRatio);

        public static int MaxCommentsForLikes(int likes) =>
            Mathf.FloorToInt(Mathf.Max(0, likes) * MaxCommentRatio);

        private static int PickFromPool(System.Random rng, int[] pool)
        {
            if (pool == null || pool.Length == 0) return 0;
            return pool[rng.Next(pool.Length)];
        }

        /// <summary>Nudges value off multiples of 100/1000 so organic posts do not look bot-boosted.</summary>
        private static int JitterAwayFromRound(int value, System.Random rng, int cap)
        {
            if (value <= 0) return value;
            if (value % 100 != 0 && value % 1000 != 0) return Mathf.Min(value, cap);

            int jitter = rng.Next(3, 27);
            int adjusted = value + (rng.Next(2) == 0 ? jitter : -jitter);
            return Mathf.Clamp(Mathf.Max(0, adjusted), 0, cap);
        }
    }
}
