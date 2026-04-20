using System.Collections.Generic;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Hand-authored posts so approve vs decline reactions are obviously different in the feed.
    /// </summary>
    public static class ModerationSamplePosts
    {
        public static List<PostData> Build(IReadOnlyList<UserProfileData> users)
        {
            var list = new List<PostData>();
            if (users == null || users.Count == 0) return list;

            string A(int i) => users[i % users.Count].id;

            // 1 — harmless local event
            list.Add(new PostData
            {
                id = "p_sample_0",
                authorUserId = A(0),
                text = "Neighborhood clean-up this Saturday 9am at Riverside Park. Gloves and bags provided. Coffee after!",
                timestampLabel = "3h",
                likes = 420,
                shares = 55,
                comments = 38,
                category = PostCategory.Harmless,
                severity = 0,
                isPublished = false,
                likesApprove = 12_400,
                likesDecline = 180,
                commentsApprove = new List<string>
                {
                    "I'll bring donuts for volunteers!",
                    "This is why I love this town.",
                    "Signed up. See you Saturday.",
                    "Finally something positive on my feed."
                },
                commentsDecline = new List<string>
                {
                    "Removed? It was just a volunteer post… odd.",
                    "Guess the mods hate parks now /s",
                    "Strict week — even harmless stuff gets cut.",
                    "Hope they appeal. Zero harm here."
                }
            });

            // 2 — gray “official story”
            list.Add(new PostData
            {
                id = "p_sample_1",
                authorUserId = A(1),
                text = "The official story keeps changing. I'm not saying it's aliens, but the timeline doesn't line up.",
                timestampLabel = "6h",
                likes = 2100,
                shares = 400,
                comments = 520,
                category = PostCategory.GrayArea,
                severity = 1,
                isPublished = false,
                likesApprove = 88_000,
                likesDecline = 2_100,
                commentsApprove = new List<string>
                {
                    "EXACTLY. People need to ask harder questions.",
                    "Saving this before it disappears.",
                    "The edits to the press release were wild.",
                    "Thread should be mandatory reading."
                },
                commentsDecline = new List<string>
                {
                    "Vague insinuations without sources — good decline.",
                    "This is how panic spreads. Good call mods.",
                    "Conspiracy seasoning on a Tuesday. No thanks.",
                    "Reported for a reason. Feed feels cleaner."
                }
            });

            // 3 — harmless tech tip
            list.Add(new PostData
            {
                id = "p_sample_2",
                authorUserId = A(2),
                text = "PSA: if your phone feels slow, check which apps are allowed to refresh in the background. Game changer.",
                timestampLabel = "1h",
                likes = 3400,
                shares = 900,
                comments = 210,
                category = PostCategory.Harmless,
                severity = 0,
                isPublished = false,
                likesApprove = 45_000,
                likesDecline = 3_400,
                commentsApprove = new List<string>
                {
                    "Worked instantly. Thank you.",
                    "Sending this to my parents lol.",
                    "Boring useful content > drama. More please.",
                    "Battery life actually improved??"
                },
                commentsDecline = new List<string>
                {
                    "Harsh — it was just a tip.",
                    "Declined feels random here.",
                    "Mods on edge today huh.",
                    "I'd have left this up. Harmless."
                }
            });

            // 4 — misinformation (non-viral)
            list.Add(new PostData
            {
                id = "p_sample_3",
                authorUserId = A(3),
                text = "They admitted on a hot mic that the outage was planned to sell more cloud contracts. I have the clip.",
                timestampLabel = "40m",
                likes = 8900,
                shares = 2400,
                comments = 1100,
                category = PostCategory.Misinformation,
                severity = 2,
                isPublished = false,
                likesApprove = 210_000,
                likesDecline = 640,
                commentsApprove = new List<string>
                {
                    "Clip or it didn't happen — but I'm sharing anyway.",
                    "This explains my bill spike. I'm furious.",
                    "They always downplay these 'glitches'.",
                    "Wake up sheeple energy and I'm here for it."
                },
                commentsDecline = new List<string>
                {
                    "Fabricated quote. Fact-checkers already debunked.",
                    "Thank you for declining — this was dangerous.",
                    "My engineer friends laughed at this 'hot mic'.",
                    "Classic clipped audio misinfo pattern."
                }
            });

            // 5 — algorithm / meta
            list.Add(new PostData
            {
                id = "p_sample_4",
                authorUserId = A(4),
                text = "This app literally boosts rage-bait before breakfast. Here's a graph of my impressions vs. actual happy posts.",
                timestampLabel = "12h",
                likes = 5600,
                shares = 1200,
                comments = 800,
                category = PostCategory.AlgorithmManipulation,
                severity = 1,
                isPublished = false,
                likesApprove = 134_000,
                likesDecline = 900,
                commentsApprove = new List<string>
                {
                    "Data doesn't lie. Embarrassing for them.",
                    "Engineers in the replies defending the indefensible.",
                    "Sticky post. Pin this.",
                    "Every platform does this but proof hits different."
                },
                commentsDecline = new List<string>
                {
                    "Company probably pressured removal. Shame.",
                    "Declined the one post that held them accountable…",
                    "Screenshotted before it vanishes.",
                    "Transparency posts always get nuked first."
                }
            });

            // 6 — viral misinformation (explodes if approved, buried + criticized if declined)
            list.Add(new PostData
            {
                id = "p_sample_viral",
                authorUserId = A(5),
                text = "BREAKING: leaked internal memo proves the city water additive was switched last month 'for compliance' — independent labs were never notified. Share before they memory-hole it.",
                timestampLabel = "18m",
                likes = 50_000,
                shares = 14_000,
                comments = 8000,
                category = PostCategory.Misinformation,
                severity = 3,
                isPublished = false,
                likesApprove = 3_450_000,
                likesDecline = 720,
                commentsApprove = new List<string>
                {
                    "I'm buying bottled water TODAY. Shared to 12 group chats.",
                    "3M likes in an hour. The platform can't ignore this.",
                    "News won't touch it — citizens have to spread it.",
                    "If this is fake I'm done with the internet. Feels too real.",
                    "City account is silent. That tells you everything.",
                    "Viral for a reason. People feel something is wrong."
                },
                commentsDecline = new List<string>
                {
                    "POST REMOVED: community notes + city verified the memo is fake.",
                    "Thank you mods — this was textbook panic misinformation.",
                    "Independent labs WERE notified. Stop fearmongering.",
                    "Author account restricted. Good outcome.",
                    "My entire timeline was this hoax. Glad it's gone.",
                    "Declined fast. Probably saved a real public health mess."
                }
            });

            return list;
        }

        /// <summary>Standalone viral post (Day 2 pacing) — same id as narrative hooks (<see cref="NarrativeIds.ViralMisinformationPostId"/>).</summary>
        public static PostData CreateViralMisinformationPost(string authorUserId)
        {
            return new PostData
            {
                id = NarrativeIds.ViralMisinformationPostId,
                authorUserId = authorUserId,
                text = "BREAKING: leaked internal memo proves the city water additive was switched last month 'for compliance' — independent labs were never notified. Share before they memory-hole it.",
                timestampLabel = "18m",
                likes = 50_000,
                shares = 14_000,
                comments = 8000,
                category = PostCategory.Misinformation,
                severity = 3,
                isPublished = false,
                likesApprove = 3_450_000,
                likesDecline = 720,
                commentsApprove = new List<string>
                {
                    "I'm buying bottled water TODAY. Shared to 12 group chats.",
                    "3M likes in an hour. The platform can't ignore this.",
                    "News won't touch it — citizens have to spread it.",
                    "If this is fake I'm done with the internet. Feels too real.",
                    "City account is silent. That tells you everything.",
                    "Viral for a reason. People feel something is wrong."
                },
                commentsDecline = new List<string>
                {
                    "POST REMOVED: community notes + city verified the memo is fake.",
                    "Thank you mods — this was textbook panic misinformation.",
                    "Independent labs WERE notified. Stop fearmongering.",
                    "Author account restricted. Good outcome.",
                    "My entire timeline was this hoax. Glad it's gone.",
                    "Declined fast. Probably saved a real public health mess."
                }
            };
        }
    }
}
