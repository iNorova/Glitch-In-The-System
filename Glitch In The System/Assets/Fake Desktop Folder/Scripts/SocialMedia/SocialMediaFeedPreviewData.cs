using System.Collections.Generic;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Social
{
    /// <summary>Sample posts/comments for edit-mode feed preview and offline filler.</summary>
    public static class SocialMediaFeedPreviewData
    {
        public const string PreviewUserId = "preview_user";
        public const int PlayFillerPostCount = 6;

        /// <summary>Same filler posts as <see cref="SocialMediaFeedController"/> uses at play time.</summary>
        public static List<PostData> CreatePlayFillerPosts()
        {
            var posts = new List<PostData>(PlayFillerPostCount);
            string[] texts =
            {
                "Gym update: finally hit a new PR today, let's go.",
                "Anyone know a good cafe near downtown with outlets and fast wifi?",
                "New game patch dropped and it fixed so many issues.",
                "Rainy day playlist recommendations? Need chill tracks.",
                "Tried making homemade ramen tonight and it actually worked.",
                "Weekend plans: movie night and catching up on sleep."
            };

            for (int i = 0; i < texts.Length; i++)
            {
                var post = new PostData
                {
                    id = $"demo_{i}",
                    authorUserId = $"user{i + 1}",
                    text = texts[i],
                    timestampLabel = $"{(i + 1) * 7}m",
                    category = PostCategory.Harmless,
                    severity = 0,
                    isPublished = true
                };
                var demoRng = new System.Random(10_000 + i);
                OrganicEngagementUtility.ApplyToPost(post, demoRng, PostCategory.Harmless);
                ReportReasonKits.ApplyIfMissing(post, demoRng);
                PostManager.AssignDefaultBranches(post, demoRng);
                PostManager.ApplyDecisionReaction(post, approvedOutcome: true, users: null);
                PostManager.RefreshEngagementLabel(post);
                posts.Add(post);
            }

            return posts;
        }

        public static List<PostData> CreatePreviewPosts(int count)
        {
            count = UnityEngine.Mathf.Clamp(count, 1, 8);
            var posts = new List<PostData>(count);
            var rng = new System.Random(4242);

            var samples = new[]
            {
                (
                    "Gym update: finally hit a new PR today. Who else is training this weekend?",
                    PostCategory.Harmless,
                    "TRENDING",
                    new[] { "Let's gooo", "What lift?", "Inspiring!" }
                ),
                (
                    "BREAKING: leaked memo says the outage was intentional. Share before they delete this.",
                    PostCategory.Misinformation,
                    "TRENDING",
                    new[] { "This can't be real", "Source?", "Mods need to look at this" }
                ),
                (
                    "Reminder: be kind online. People are going through a lot right now.",
                    PostCategory.Harmless,
                    "",
                    new[] { "Needed this", "Thank you", "Sharing with friends" }
                ),
                (
                    "If you know, you know. The official story still doesn't add up.",
                    PostCategory.Narrative,
                    "LOW ENGAGEMENT",
                    new[] { "👀", "Following", "Receipts when?" }
                )
            };

            for (int i = 0; i < count; i++)
            {
                var s = samples[i % samples.Length];
                var post = new PostData
                {
                    id = $"preview_{i}",
                    authorUserId = PreviewUserId,
                    text = s.Item1,
                    timestampLabel = $"{(i + 1) * 12}m",
                    category = s.Item2,
                    severity = s.Item2 == PostCategory.Misinformation ? 2 : 0,
                    isPublished = true
                };
                var tier = s.Item2 == PostCategory.Misinformation ? EngagementTier.Heated : EngagementTier.Normal;
                OrganicEngagementUtility.ApplyTier(post, tier, rng);
                ReportReasonKits.ApplyIfMissing(post, rng);
                PostManager.AssignDefaultBranches(post, rng);
                PostManager.ApplyDecisionReaction(post, approvedOutcome: true, users: null);
                PostManager.RefreshEngagementLabel(post);

                post.commentPreview.Clear();
                for (int c = 0; c < s.Item4.Length; c++)
                {
                    post.commentPreview.Add(new CommentData
                    {
                        authorUserId = $"preview_commenter_{c}",
                        text = s.Item4[c],
                        likes = 12 + c * 7
                    });
                }

                if (i == 1)
                    post.wasRewrittenByAlgorithm = true;

                posts.Add(post);
            }

            return posts;
        }

        public static UserProfileData CreatePreviewUser()
        {
            return new UserProfileData
            {
                id = PreviewUserId,
                username = "preview_mod",
                displayName = "Preview Author"
            };
        }

        /// <summary>Rich sample post (comments, labels) for the single edit-mode design template.</summary>
        public static PostData GetDesignTemplateSamplePost()
        {
            var posts = CreatePreviewPosts(1);
            return posts.Count > 0 ? posts[0] : null;
        }
    }
}
