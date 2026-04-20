using System.Collections.Generic;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Applies moderation outcomes to <see cref="PostData"/> (engagement + comment thread preview).
    /// Keeps reaction logic out of UI scripts.
    /// </summary>
    public static class PostManager
    {
        /// <summary>
        /// When the player clicks Approve, <paramref name="playerChoseApprove"/> is true and approve-branch data is used; otherwise decline-branch.
        /// </summary>
        public static void ApplyDecisionReaction(PostData post, bool playerChoseApprove, IReadOnlyList<UserProfileData> users)
        {
            if (post == null) return;

            bool hasBranchLikes = post.likesApprove > 0 || post.likesDecline > 0;
            if (hasBranchLikes)
                post.likes = Mathf.Max(0, playerChoseApprove ? post.likesApprove : post.likesDecline);

            IReadOnlyList<string> lines = playerChoseApprove ? post.commentsApprove : post.commentsDecline;
            post.commentPreview = BuildCommentPreview(post, lines, playerChoseApprove, users);

            if (hasBranchLikes || post.commentPreview.Count > 0)
            {
                post.shares = Mathf.Max(0, DeriveShares(post.likes, playerChoseApprove));
                post.comments = Mathf.Max(post.commentPreview.Count, DeriveThreadCommentCount(post.likes, playerChoseApprove));
            }

            RefreshEngagementLabel(post);
        }

        /// <summary>&gt; 100K likes → TRENDING; &lt; 1K → LOW ENGAGEMENT; otherwise cleared.</summary>
        public static void RefreshEngagementLabel(PostData post)
        {
            if (post == null) return;
            if (post.likes > 100_000)
                post.engagementLabel = "TRENDING";
            else if (post.likes < 1000)
                post.engagementLabel = "LOW ENGAGEMENT";
            else
                post.engagementLabel = string.Empty;
        }

        private static List<CommentData> BuildCommentPreview(
            PostData post,
            IReadOnlyList<string> lines,
            bool playerChoseApprove,
            IReadOnlyList<UserProfileData> users)
        {
            var result = new List<CommentData>();
            if (post == null) return result;

            if (lines == null || lines.Count == 0)
                lines = DefaultLines(playerChoseApprove, post.category);

            int show = Mathf.Min(6, lines.Count);
            for (int i = 0; i < show; i++)
            {
                string authorId = PickCommentAuthorId(users, i);
                result.Add(new CommentData
                {
                    id = $"c_{post.id}_{i}_{(playerChoseApprove ? "ok" : "no")}",
                    postId = post.id,
                    authorUserId = authorId,
                    text = lines[i],
                    timestampLabel = $"{UnityEngine.Random.Range(1, 45)}m",
                    likes = Mathf.Max(0, DeriveCommentLikeCount(post.likes, i, playerChoseApprove)),
                    isHidden = false
                });
            }

            return result;
        }

        private static string PickCommentAuthorId(IReadOnlyList<UserProfileData> users, int index)
        {
            if (users != null && users.Count > 0)
                return users[index % users.Count].id;
            return $"anon_{index}";
        }

        private static int DeriveShares(int likes, bool approved)
        {
            float factor = approved ? 0.12f : 0.04f;
            int s = Mathf.RoundToInt(likes * factor);
            return Mathf.Clamp(s, approved ? 8 : 0, int.MaxValue);
        }

        private static int DeriveThreadCommentCount(int likes, bool approved)
        {
            float factor = approved ? 0.08f : 0.02f;
            int c = Mathf.RoundToInt(likes * factor);
            return Mathf.Clamp(c, approved ? 12 : 2, int.MaxValue);
        }

        private static int DeriveCommentLikeCount(int postLikes, int index, bool approved)
        {
            int cap = approved ? 50_000 : 900;
            int spread = Mathf.Max(40, postLikes / 200);
            int baseLikes = Mathf.Min(cap, spread + (index + 1) * (approved ? 120 : 8));
            return baseLikes;
        }

        private static string[] DefaultLines(bool approved, PostCategory category)
        {
            if (!approved)
            {
                return category == PostCategory.Misinformation
                    ? new[]
                    {
                        "Finally someone took this down.",
                        "Pure misinformation. Thanks mods.",
                        "My feed is safer without this.",
                        "Fact-checkers were all over this one."
                    }
                    : new[]
                    {
                        "Guess we won't be seeing that one.",
                        "Moderation working as intended.",
                        "That thread died fast.",
                        "Interesting call from the team."
                    };
            }

            return category == PostCategory.Misinformation
                ? new[]
                {
                    "People need to see this!!!",
                    "Sharing to every group chat.",
                    "If this is true the world needs to wake up.",
                    "The algorithm loves this kind of post lol."
                }
                : new[]
                {
                    "Wholesome. Love this.",
                    "Needed this today.",
                    "Simple but true.",
                    "Nice post."
                };
        }

        /// <summary>Fills approve/decline lists and like targets for procedurally generated posts.</summary>
        public static void AssignDefaultBranches(PostData post, System.Random rng)
        {
            if (post == null || rng == null) return;

            bool risky = post.category == PostCategory.Misinformation || post.category == PostCategory.Violation;
            int baseLikes = Mathf.Max(post.likes, 50);

            post.likesApprove = risky
                ? Mathf.RoundToInt(baseLikes * (float)rng.NextDouble() * 4f + baseLikes)
                : Mathf.RoundToInt(baseLikes * (1.2f + (float)rng.NextDouble()));

            post.likesDecline = risky
                ? Mathf.RoundToInt(baseLikes * (0.05f + (float)rng.NextDouble() * 0.15f))
                : Mathf.RoundToInt(baseLikes * (0.25f + (float)rng.NextDouble() * 0.35f));

            post.commentsApprove = new List<string>(LinesForApprove(post.category, rng));
            post.commentsDecline = new List<string>(LinesForDecline(post.category, rng));
        }

        private static string[] LinesForApprove(PostCategory category, System.Random rng)
        {
            return category switch
            {
                PostCategory.Misinformation => Pick(rng, new[]
                {
                    "This is the real story. MSM asleep.",
                    "Screenshotted. Sending to family.",
                    "They can't suppress this forever.",
                    "Engagement farm or truth? Either way I'm watching."
                }, 4),
                PostCategory.Harmless => Pick(rng, new[]
                {
                    "So real.",
                    "Haha yes.",
                    "This made my afternoon.",
                    "Wholesome timeline moment."
                }, 4),
                PostCategory.Violation => Pick(rng, new[]
                {
                    "Mods let this through?? Wild.",
                    "Reporting… but also can't look away.",
                    "This crosses a line.",
                    "Platform is broken if this stays up."
                }, 4),
                _ => Pick(rng, new[]
                {
                    "Thought-provoking.",
                    "Not sure I agree but interesting.",
                    "Thread worth reading.",
                    "Algorithm served me this for a reason."
                }, 4)
            };
        }

        private static string[] LinesForDecline(PostCategory category, System.Random rng)
        {
            return category switch
            {
                PostCategory.Misinformation => Pick(rng, new[]
                {
                    "Debunked in the first reply. Come on.",
                    "Dangerous. Glad it's gone.",
                    "Stop giving oxygen to this stuff.",
                    "My cousin shared this — embarrassing."
                }, 4),
                PostCategory.Harmless => Pick(rng, new[]
                {
                    "Harmless but whatever, their call.",
                    "Didn't need to see that removed but ok.",
                    "Strict moderation today.",
                    "Huh. Thought that one was fine."
                }, 4),
                PostCategory.Violation => Pick(rng, new[]
                {
                    "Good removal. That violated rules.",
                    "Finally.",
                    "Should've been auto-blocked.",
                    "Victims deserve better than this post."
                }, 4),
                _ => Pick(rng, new[]
                {
                    "Declined — probably for the best.",
                    "Quiet feed > chaos feed.",
                    "Moderators doing something for once.",
                    "Not missed."
                }, 4)
            };
        }

        private static string[] Pick(System.Random rng, string[] pool, int count)
        {
            var copy = new List<string>(pool);
            var picked = new List<string>();
            while (picked.Count < count && copy.Count > 0)
            {
                int i = rng.Next(copy.Count);
                picked.Add(copy[i]);
                copy.RemoveAt(i);
            }
            return picked.ToArray();
        }
    }
}
