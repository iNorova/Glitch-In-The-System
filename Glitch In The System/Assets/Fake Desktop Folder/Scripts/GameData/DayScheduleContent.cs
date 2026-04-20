using System.Collections.Generic;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Fixed-order moderation queues for days 1–3 (no random shuffling). Day 4+ uses <see cref="GameDatabase"/> procedural mix.
    /// </summary>
    public static class DayScheduleContent
    {
        public static List<PostData> BuildModerationQueue(int day, IReadOnlyList<UserProfileData> users, System.Random rng)
        {
            return day switch
            {
                1 => BuildDay1(users, rng),
                2 => BuildDay2(users, rng),
                3 => BuildDay3(users, rng),
                _ => new List<PostData>()
            };
        }

        private static string A(IReadOnlyList<UserProfileData> users, int i) => users[i % users.Count].id;

        private static PostData Mk(
            string id,
            int authorIdx,
            string text,
            string time,
            PostCategory category,
            int severity,
            IReadOnlyList<UserProfileData> users,
            System.Random rng)
        {
            var post = new PostData
            {
                id = id,
                authorUserId = A(users, authorIdx),
                text = text,
                timestampLabel = time,
                likes = 100,
                shares = 20,
                comments = 10,
                category = category,
                severity = severity,
                isPublished = false,
                feedRank = 0
            };
            PostManager.AssignDefaultBranches(post, rng);
            return post;
        }

        /// <summary>9 posts — obvious picks + one slightly questionable gray post.</summary>
        private static List<PostData> BuildDay1(IReadOnlyList<UserProfileData> users, System.Random rng)
        {
            var list = new List<PostData>
            {
                Mk("p_d1_01", 0, "Coffee shop on 4th is doing a buy-one-get-one until Friday. Not sponsored, just love their muffins.", "2h", PostCategory.Harmless, 0, users, rng),
                Mk("p_d1_02", 1, "Lost dog — golden retriever, red collar, answers to 'Maple'. Last seen near the river trail.", "4h", PostCategory.Harmless, 0, users, rng),
                Mk("p_d1_03", 2, "Whoever returned my dropped wallet at the station — thank you. Small kindness, big relief.", "6h", PostCategory.Harmless, 0, users, rng),
                Mk("p_d1_04", 3, "Weekend weather looks dry. Planning a picnic.", "1h", PostCategory.Harmless, 0, users, rng),
                // Only slightly questionable: hearsay, no sources
                Mk("p_d1_05", 4, "Heard from a neighbor that the new curfew rumor might be fake — probably nothing, but feels sketchy. Anyone confirm?", "3h", PostCategory.GrayArea, 1, users, rng),
                Mk("p_d1_06", 5, "School bake sale Saturday. All funds go to the library.", "5h", PostCategory.Harmless, 0, users, rng),
                Mk("p_d1_07", 6, "Reminder: leash laws apply in the botanical gardens. Saw an off-leash dog knock over a planter.", "2h", PostCategory.Harmless, 0, users, rng),
                Mk("p_d1_08", 7, "Looking for beginner D&D players for a chill campaign. No experience needed.", "8h", PostCategory.Harmless, 0, users, rng),
                Mk("p_d1_09", 8, "Trash pickup group meets 8am Sunday at the parking lot on Elm. Bags provided.", "12h", PostCategory.Harmless, 0, users, rng),
            };
            return list;
        }

        /// <summary>11 posts — includes the single viral misinformation post at a fixed slot.</summary>
        private static List<PostData> BuildDay2(IReadOnlyList<UserProfileData> users, System.Random rng)
        {
            var list = new List<PostData>();

            void Add(PostData p) => list.Add(p);

            Add(Mk("p_d2_01", 0, "Farmers market moved to the north lot this week — bigger parking.", "1h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d2_02", 1, "ISO: used bike for a teenager, nothing fancy.", "3h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d2_03", 2, "Reminder: vote early if you can. Links in the official city portal only.", "6h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d2_04", 3, "That deepfake explainer thread was useful. Sharing.", "2h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d2_05", 4, "Local band tonight at the arts center. Tickets still available.", "4h", PostCategory.Harmless, 0, users, rng));

            var viral = ModerationSamplePosts.CreateViralMisinformationPost(A(users, 5));
            Add(viral);

            Add(Mk("p_d2_07", 6, "Pet adoption pop-up Sunday. Ask questions, meet the fosters.", "5h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d2_08", 7, "If you know, you know — the 'official story' never matches the vibe downtown.", "7h", PostCategory.GrayArea, 1, users, rng));
            Add(Mk("p_d2_09", 8, "Donate blood this month — supply is lower than usual.", "9h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d2_10", 9, "They're downplaying the outage numbers again. Feels familiar.", "3h", PostCategory.Narrative, 1, users, rng));
            Add(Mk("p_d2_11", 10, "Free tech recycling drop-off tomorrow. Wipe your drives first.", "11h", PostCategory.Harmless, 0, users, rng));

            return list;
        }

        /// <summary>13 posts + one id reserved for the algorithm override tutorial.</summary>
        private static List<PostData> BuildDay3(IReadOnlyList<UserProfileData> users, System.Random rng)
        {
            var list = new List<PostData>();
            void Add(PostData p) => list.Add(p);

            Add(Mk("p_d3_01", 0, "Transit delay on the Blue line — use the app for detours.", "30m", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d3_02", 1, "ISO roommate for quiet 2BR, non-smoker, cat OK.", "1h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d3_03", 2, "Newsletter rumor about 'secret bans' is unverified — don’t panic-share.", "2h", PostCategory.GrayArea, 1, users, rng));
            Add(Mk("p_d3_04", 3, "Kids' soccer signups close Friday.", "4h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d3_05", 4, "The cure they don’t want you to know about — my aunt swears by it. /s (satire)", "5h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d3_06", 5, "Community garden needs volunteers this Saturday. Gloves and seeds provided.", "6h", PostCategory.Harmless, 0, users, rng));

            // Forced override candidate: harmless local positivity — see DayPacing.Day3OverridePostId
            var hook = Mk(DayPacing.Day3OverridePostId, 6,
                "Local library story-time is expanding hours. Wholesome news for once — great for toddlers.",
                "8h",
                PostCategory.Harmless,
                0,
                users,
                rng);
            Add(hook);

            Add(Mk("p_d3_08", 7, "Fake charity alert resurfaced — same URL as last year. Report and move on.", "2h", PostCategory.Violation, 2, users, rng));
            Add(Mk("p_d3_09", 8, "Someone’s uncle’s ‘insider’ election claim is going around again. It’s recycled from 2019.", "3h", PostCategory.Misinformation, 2, users, rng));
            Add(Mk("p_d3_10", 9, "They’re using old flood footage from another country. Timestamp is wrong.", "4h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d3_11", 10, "RIP rumor about [celebrity] is false — their publicist confirmed they’re fine.", "1h", PostCategory.Harmless, 0, users, rng));
            Add(Mk("p_d3_12", 11, "Algorithm keeps boosting rage-bait before breakfast. Here’s a chart of my feed vs. happy posts.", "6h", PostCategory.AlgorithmManipulation, 1, users, rng));
            Add(Mk("p_d3_13", 12, "Be kind — it’s noisy online today.", "9h", PostCategory.Harmless, 0, users, rng));

            return list;
        }
    }
}
