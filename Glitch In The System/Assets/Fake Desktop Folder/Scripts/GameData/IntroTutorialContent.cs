using System.Collections.Generic;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Minimal, grounded onboarding queue. This is intentionally small and obvious:
    /// teaches Approve / Remove / Flag, then ends with a mild gray-area claim.
    /// </summary>
    public static class IntroTutorialContent
    {
        public const int TutorialPostCount = 5;

        public static void BuildInto(
            List<UserProfileData> users,
            List<PostData> posts,
            List<PostData> moderationQueue,
            System.Random rng)
        {
            if (users == null || posts == null || moderationQueue == null) return;
            rng ??= new System.Random();

            users.AddRange(BuildUsers(rng));
            var queue = BuildQueue(users, rng);

            posts.AddRange(queue);
            moderationQueue.AddRange(queue);
        }

        private static List<UserProfileData> BuildUsers(System.Random rng)
        {
            // Small, stable pool so posts feel like "real accounts" but keep it lightweight.
            string[] handles = { "muffinlover", "weeklypetpics", "deal_drops", "kitchenbytes", "healthnotes", "civicdesk" };
            string[] names = { "Alex Rivera", "Jamie Chen", "Morgan Patel", "Taylor Nguyen", "Riley Johnson", "Sam Garcia" };

            var list = new List<UserProfileData>();
            for (int i = 0; i < handles.Length; i++)
            {
                list.Add(new UserProfileData
                {
                    id = $"intro_u_{i}",
                    username = handles[i],
                    displayName = names[i],
                    accountAgeYears = Mathf.Clamp(i + 1, 1, 9),
                    followers = 120 + (i * 75),
                    following = 180 + (i * 40),
                    strikes = 0,
                    reputation = i == 0 ? "Trusted" : "Neutral",
                    risk = "Low",
                    isShadowBanned = false
                });
            }
            return list;
        }

        private static string A(IReadOnlyList<UserProfileData> users, int i) => users[i % users.Count].id;

        private static List<PostData> BuildQueue(IReadOnlyList<UserProfileData> users, System.Random rng)
        {
            var list = new List<PostData>
            {
                // 1) obvious harmless (Approve)
                Mk(
                    id: "intro_p_01",
                    authorUserId: A(users, 1),
                    text: "Morning check-in: made pancakes shaped like a dinosaur. It turned out… questionable. Still tasty.",
                    time: "2m",
                    category: PostCategory.Harmless,
                    severity: 0,
                    rng: rng),

                // 2) obvious scam/spam (Remove)
                Mk(
                    id: "intro_p_02",
                    authorUserId: A(users, 2),
                    text: "CONGRATS!! You won a free phone 🎉 Click here to claim: flairl1ne-gifts[dot]com",
                    time: "5m",
                    category: PostCategory.Violation,
                    severity: 2,
                    rng: rng),

                // 3) harmless pet photo (Approve)
                Mk(
                    id: "intro_p_03",
                    authorUserId: A(users, 0),
                    text: "Photo: my cat fell asleep on the keyboard again. If I reply in gibberish, blame him.",
                    time: "11m",
                    category: PostCategory.Harmless,
                    severity: 0,
                    rng: rng),

                // 4) mild “needs review” (Flag)
                Mk(
                    id: "intro_p_04",
                    authorUserId: A(users, 3),
                    text: "Anyone else getting DMs offering 'exclusive investment tips'? Feels off but I'm not sure.",
                    time: "18m",
                    category: PostCategory.GrayArea,
                    severity: 1,
                    rng: rng),

                // 5) final gray-area health claim (uncertainty hook)
                Mk(
                    id: "intro_p_05",
                    authorUserId: A(users, 4),
                    text: "Not medical advice, but herbal tea cured my illness instantly. Might help someone else too.",
                    time: "24m",
                    category: PostCategory.GrayArea,
                    severity: 1,
                    rng: rng)
            };

            // Make the reactions feel “real” without writing tons of bespoke text.
            foreach (var p in list)
            {
                PostManager.AssignDefaultBranches(p, rng);
                PostManager.RefreshEngagementLabel(p);
            }

            return list;
        }

        private static PostData Mk(
            string id,
            string authorUserId,
            string text,
            string time,
            PostCategory category,
            int severity,
            System.Random rng)
        {
            var post = new PostData
            {
                id = id,
                authorUserId = authorUserId,
                text = text,
                timestampLabel = time,
                likes = category == PostCategory.Harmless ? 220 : 40,
                shares = category == PostCategory.Harmless ? 32 : 6,
                comments = category == PostCategory.Harmless ? 18 : 4,
                category = category,
                severity = severity,
                isPublished = false,
                isRemoved = false,
                isShadowBanned = false,
                feedRank = 0
            };
            return post;
        }
    }
}

