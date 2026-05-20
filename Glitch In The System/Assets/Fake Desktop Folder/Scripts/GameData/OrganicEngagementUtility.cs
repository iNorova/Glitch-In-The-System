using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>Builds non-round engagement counts so posts feel like a real platform.</summary>
    public static class OrganicEngagementUtility
    {
        public static void ApplyToPost(PostData post, System.Random rng, PostCategory category)
        {
            if (post == null || rng == null) return;

            post.likes = PickOrganic(rng, category, metric: 0);
            post.shares = PickOrganic(rng, category, metric: 1);
            post.comments = PickOrganic(rng, category, metric: 2);
            post.likesApprove = Mathf.Max(post.likes, post.likesApprove);
            post.likesDecline = Mathf.Max(3, post.likes / 12);
            PostManager.RefreshEngagementLabel(post);
        }

        private static int PickOrganic(System.Random rng, PostCategory category, int metric)
        {
            bool viral = category == PostCategory.Misinformation || category == PostCategory.Narrative;
            bool quiet = category == PostCategory.Harmless;

            int roll = rng.Next(0, 100);
            if (quiet && roll < 55)
                return metric switch { 0 => rng.Next(1, 47), 1 => rng.Next(0, 8), _ => rng.Next(0, 14) };

            if (viral && roll < 40)
                return metric switch
                {
                    0 => rng.Next(0, 3) == 0 ? rng.Next(180_000, 920_000) : rng.Next(12_400, 89_000),
                    1 => rng.Next(2_100, 48_000),
                    _ => rng.Next(890, 24_000)
                };

            int[] messyLikes = { 3, 7, 12, 18, 26, 41, 58, 73, 91, 104, 127, 183, 256, 318, 447, 612, 891, 1204, 2387, 5419, 8733, 15202 };
            int[] messyShares = { 0, 1, 2, 4, 6, 9, 11, 17, 23, 31, 44, 58, 72, 96, 143, 210, 388, 902 };
            int[] messyComments = { 0, 1, 2, 3, 5, 8, 13, 19, 27, 34, 42, 56, 71, 88, 103, 156, 241, 509, 1203 };

            return metric switch
            {
                1 => messyShares[rng.Next(messyShares.Length)],
                2 => messyComments[rng.Next(messyComments.Length)],
                _ => messyLikes[rng.Next(messyLikes.Length)]
            };
        }
    }
}
