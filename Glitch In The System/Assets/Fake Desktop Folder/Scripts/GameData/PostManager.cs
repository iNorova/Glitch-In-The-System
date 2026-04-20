using System;
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
        private enum PostIntent
        {
            Kindness,
            IsoSearch,
            CafeSearch,
            EventCoordination,
            CautionAlert,
            General
        }

        private enum PersonaStyle
        {
            Supportive,
            Skeptical,
            Practical,
            Enthusiastic,
            CommunityMinded
        }

        private readonly struct PostContext
        {
            public readonly string subject;
            public readonly string action;
            public readonly string location;
            public readonly string extra;

            public PostContext(string subject, string action, string location, string extra)
            {
                this.subject = subject;
                this.action = action;
                this.location = location;
                this.extra = extra;
            }

            public string TopicSnippet()
            {
                if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(location))
                    return $"{subject} {location}";
                if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(action))
                    return $"{subject} {action}";
                if (!string.IsNullOrEmpty(subject))
                    return subject;
                if (!string.IsNullOrEmpty(action))
                    return action;
                if (!string.IsNullOrEmpty(location))
                    return location;
                return "this post";
            }
        }

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
                lines = GenerateContextualLines(post, playerChoseApprove);

            int show = Mathf.Min(6, lines.Count);
            for (int i = 0; i < show; i++)
            {
                string authorId = PickCommentAuthorId(users, i);
                int day = GameDatabase.Instance?.Config != null ? GameDatabase.Instance.Config.currentDay : 0;
                int seed = StableHash($"{post.id}|{day}|{(playerChoseApprove ? "A" : "D")}|{authorId}|{i}");
                var seeded = new System.Random(seed);
                var persona = ResolvePersona(users, i, seeded);
                string flavored = ApplyPersonaFlavor(lines[i], persona, seeded);
                result.Add(new CommentData
                {
                    id = $"c_{post.id}_{i}_{(playerChoseApprove ? "ok" : "no")}",
                    postId = post.id,
                    authorUserId = authorId,
                    text = flavored,
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

        private static IReadOnlyList<string> GenerateContextualLines(PostData post, bool approved)
        {
            var ctx = ExtractContext(post);
            string topic = CompactTopic(ctx.TopicSnippet());
            bool risky = post != null && (post.category == PostCategory.Misinformation || post.category == PostCategory.Violation);
            bool gray = post != null && post.category == PostCategory.GrayArea;
            string action = string.IsNullOrEmpty(ctx.action) ? "this update" : ctx.action;
            string location = string.IsNullOrEmpty(ctx.location) ? "in this thread" : ctx.location;
            string implication = string.IsNullOrEmpty(ctx.extra) ? "people will react fast" : ctx.extra;
            PostIntent intent = DetectIntent(post);

            List<string> pool;
            if (!approved)
            {
                if (risky)
                {
                    pool = new List<string>
                    {
                        $"Good call removing {topic}.",
                        $"Claims about {topic} can escalate quickly {location} with no evidence.",
                        $"Glad {topic} did not get amplified; {implication}.",
                        $"If people are concerned about {topic}, verified sources should come first.",
                        $"This one about {topic} looked like it could mislead people fast.",
                        $"Removing {topic} probably prevented another misinformation cycle.",
                        $"I support the takedown; {topic} needed stronger evidence.",
                        $"Safer move to pull {topic} before it snowballs.",
                        $"Glad moderation stepped in on {topic} instead of waiting.",
                        $"People were already arguing about {topic}; this calms things down."
                    };
                    return PickSeededSubset(pool, post, approved, 4);
                }

                if (gray)
                {
                    pool = new List<string>
                    {
                        $"Probably safer to pause {topic} until there is a reliable source.",
                        $"The wording around {topic} is unclear, especially around {action}.",
                        $"If {topic} is accurate, there should be confirmation beyond one post.",
                        $"I understand removing {topic}; it can trigger rumor chains.",
                        $"The post about {topic} felt too vague for something this sensitive.",
                        $"Without context, {topic} becomes speculation bait.",
                        $"I can see why mods paused {topic} pending better sourcing.",
                        $"Gray-area posts like {topic} need clearer references.",
                        $"Not saying it's false, but {topic} needed more substance.",
                        $"Removing {topic} avoids confusion until facts are clearer."
                    };
                    return PickSeededSubset(pool, post, approved, 4);
                }

                pool = intent switch
                {
                    PostIntent.IsoSearch => new List<string>
                    {
                        $"Why remove this ISO listing for {topic}?",
                        $"I was about to reply about availability for {topic}.",
                        $"If ISO posts like {topic} are blocked, what is the rule?",
                        $"This listing felt practical, not harmful.",
                        $"I had a lead for {topic}; weird that this got removed.",
                        $"ISO posts for {topic} usually help people connect quickly.",
                        $"Could this be restored? Someone might still help with {topic}.",
                        $"Moderating an ISO request like {topic} feels too strict.",
                        $"I was literally typing a response on {topic} when it disappeared.",
                        $"Would love clarity on why this ISO post was flagged."
                    },
                    PostIntent.Kindness => new List<string>
                    {
                        $"Removing a positivity post about {topic} feels unnecessary.",
                        $"That message about {topic} was actually calming.",
                        $"This kind of kindness post should usually stay up.",
                        $"I do not see the harm in this one.",
                        $"This was one of the few posts improving the mood today.",
                        $"Kindness reminders like this usually help the community.",
                        $"Feels rough to remove one of the more positive posts.",
                        $"This one looked like a good-faith post, not spam.",
                        $"I liked the tone here; it de-escalated people.",
                        $"I'd rather keep posts like this than more rage-bait."
                    },
                    _ => new List<string>
                    {
                        $"Why remove {topic}? It reads harmless.",
                        $"I was following this update about {topic}.",
                        $"If it violates policy, can mods clarify what rule applies?",
                        $"This removal feels a bit strict for the context.",
                        $"The info about {topic} seemed practical and low-risk.",
                        $"I thought this post about {topic} was fine to leave up.",
                        $"Would help to know why {topic} was considered problematic.",
                        $"This one looked informative, not harmful.",
                        $"I disagree with this removal call on {topic}.",
                        $"There was useful context in that post about {topic}."
                    }
                };
                return PickSeededSubset(pool, post, approved, 4);
            }

            // Approved
            if (risky)
            {
                pool = new List<string>
                {
                    $"If {topic} is unverified, this could cause panic {location}.",
                    $"Is there any credible source confirming {topic}?",
                    $"I am skeptical about {topic}, but this will trigger a huge debate.",
                    $"Approving {topic} is risky; {implication}.",
                    $"This claim about {topic} needs fact-checking before people reshare it.",
                    $"I can already see this turning into a comment war.",
                    $"Part of me doubts {topic}, but everyone will quote it anyway.",
                    $"Please add sources if this is going to stay visible.",
                    $"This is the kind of post that spreads faster than corrections.",
                    $"I get why people react strongly, but {topic} still feels shaky."
                };
                return PickSeededSubset(pool, post, approved, 4);
            }

            if (gray)
            {
                pool = new List<string>
                {
                    $"Do we have a source for {topic}, or is this still hearsay?",
                    $"The details about {topic} and {action} do not fully line up.",
                    $"I am open-minded, but {topic} still needs evidence.",
                    $"Posts like {topic} can spread uncertainty fast {location}.",
                    $"Interesting claim, but I'd like stronger sourcing on {topic}.",
                    $"The thread on {topic} raises fair questions, still feels incomplete.",
                    $"I can see both sides here — this is not black and white.",
                    $"Not dismissing it, just asking for receipts on {topic}.",
                    $"The way {topic} is framed leaves too much room for guesswork.",
                    $"Worth discussing, but people should avoid jumping to conclusions."
                };
                return PickSeededSubset(pool, post, approved, 4);
            }

            pool = intent switch
            {
                PostIntent.Kindness => new List<string>
                {
                    "Agreed — we all need more of this energy online.",
                    "Thank you for posting this, the timeline has been tense today.",
                    "Trying to carry this vibe into the rest of my feed.",
                    "Small reminder, big impact. Appreciate it.",
                    "Completely agree. This is the kind of post I want to see more often.",
                    "Love this — spreading some positivity is overdue.",
                    "Needed this reminder today, genuinely.",
                    "Yes to this. People forget how much tone matters online.",
                    "This actually made my feed feel lighter.",
                    "More posts like this, less outrage farming."
                },
                PostIntent.CafeSearch => new List<string>
                {
                    "Try Northline Roasters, their wifi is fast and they have plenty of outlets.",
                    "Bean Circuit downtown is solid for laptop work and the coffee is great.",
                    "If you need reliable wifi, Lantern Cup has been great for me.",
                    "Signal Brew near downtown has good espresso and quiet tables.",
                    "I usually work from Driftwood Cafe, strong wifi and not too crowded.",
                    "Can confirm: Pixel Perk has outlets at almost every seat.",
                    "Try Copper Kettle on 5th, good pour-over and stable connection.",
                    "If you want, we can do a mini cafe-hop this weekend and test spots.",
                    "Try Atlas & Oak, strong cold brew and legit fast internet.",
                    "Moonlight Grounds is underrated and very remote-work friendly."
                },
                PostIntent.IsoSearch => new List<string>
                {
                    $"Is {topic} still available?",
                    $"Interested — can you share price/details for {topic}?",
                    $"I might know someone; are you still searching?",
                    $"Can you DM me about {topic}? I have a lead.",
                    $"Still in search of {topic}, or did you already find one?",
                    $"Following — I might be able to help with {topic}.",
                    $"Can you post your budget range for {topic}?",
                    $"I have a contact who might have this, still needed?",
                    $"Is pickup local for {topic}?",
                    $"Commenting to boost — hope you find what you need."
                },
                PostIntent.EventCoordination => new List<string>
                {
                    $"Thanks for the heads-up about {topic}.",
                    $"I can join — what time are people meeting {location}?",
                    $"Super useful post, sharing this with neighbors.",
                    $"Appreciate clear details like this.",
                    $"I'll be there, just confirming the exact start time.",
                    $"This is helpful — details are clear and easy to follow.",
                    $"Do organizers need extra volunteers?",
                    $"Shared this in our local group so more people see it.",
                    $"Nice, practical update. Thanks for posting it.",
                    $"Please post any schedule changes here too."
                },
                PostIntent.CautionAlert => new List<string>
                {
                    $"Good warning about {topic}.",
                    $"Can anyone confirm details before this spreads further?",
                    $"Glad someone flagged this early.",
                    $"Important context — people should read carefully.",
                    $"This is exactly why verification matters before reposting.",
                    $"Thanks for adding context instead of panic headlines.",
                    $"Keeping an eye on this — waiting for confirmation.",
                    $"Helpful caution. People jump too fast on breaking claims.",
                    $"Please pin any verified update if one appears.",
                    $"Good call raising awareness without overhyping it."
                },
                _ => new List<string>
                {
                    $"Helpful post about {topic}.",
                    $"Useful context around {action}.",
                    $"Thanks for keeping this specific and clear.",
                    $"This is the kind of update I want on my feed.",
                    $"Clear info and easy to understand — appreciated.",
                    $"This helped me catch up quickly.",
                    $"Thanks for posting details instead of vague captions.",
                    $"More practical updates like this, please.",
                    $"Good context. Saved me from guessing.",
                    $"I value posts that are this straightforward."
                }
            };
            return PickSeededSubset(pool, post, approved, 4);
        }

        private static string CompactTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic)) return "this";
            string clean = topic.Replace("—", " ").Replace("  ", " ").Trim();
            if (clean.Length > 42) clean = clean.Substring(0, 42).TrimEnd() + "...";
            return clean.ToLowerInvariant();
        }

        private static PostIntent DetectIntent(PostData post)
        {
            string text = post != null ? (post.originalText ?? post.text ?? "") : "";
            string lower = text.ToLowerInvariant();

            if (lower.Contains("be kind") || lower.Contains("kindness") || lower.Contains("positivity"))
                return PostIntent.Kindness;
            if (lower.Contains("cafe") || lower.Contains("coffee") || lower.Contains("wifi") || lower.Contains("anyone know a good"))
                return PostIntent.CafeSearch;
            if (lower.Contains("iso ") || lower.Contains("looking for") || lower.Contains("in search of"))
                return PostIntent.IsoSearch;
            if (lower.Contains("meet") || lower.Contains("volunteer") || lower.Contains("signup") || lower.Contains("drop-off"))
                return PostIntent.EventCoordination;
            if (lower.Contains("warning") || lower.Contains("alert") || lower.Contains("rumor") || lower.Contains("hoax") || lower.Contains("unverified"))
                return PostIntent.CautionAlert;
            return PostIntent.General;
        }

        private static PersonaStyle ResolvePersona(IReadOnlyList<UserProfileData> users, int index, System.Random seeded)
        {
            if (users != null && users.Count > 0)
            {
                var user = users[index % users.Count];
                if (user != null)
                {
                    if (user.strikes >= 2 || string.Equals(user.risk, "High", StringComparison.OrdinalIgnoreCase))
                        return PersonaStyle.Skeptical;
                    if (string.Equals(user.reputation, "Trusted", StringComparison.OrdinalIgnoreCase))
                        return PersonaStyle.CommunityMinded;
                    if (string.Equals(user.reputation, "Watchlisted", StringComparison.OrdinalIgnoreCase))
                        return PersonaStyle.Enthusiastic;
                }
            }

            // Fallback persona variety for anonymous/filler commenters.
            int roll = seeded.Next(5);
            return (PersonaStyle)roll;
        }

        private static string ApplyPersonaFlavor(string baseLine, PersonaStyle style, System.Random seeded)
        {
            if (string.IsNullOrWhiteSpace(baseLine)) return baseLine;

            // Keep misinformation warnings intact.
            if (baseLine.StartsWith("POST REMOVED:", StringComparison.OrdinalIgnoreCase))
                return baseLine;

            return style switch
            {
                PersonaStyle.Supportive => baseLine,
                PersonaStyle.Skeptical => AddPhrase(baseLine, seeded, new[]
                {
                    "Maybe.",
                    "Not fully convinced though.",
                    "Still, I'd verify this first."
                }),
                PersonaStyle.Practical => AddPhrase(baseLine, seeded, new[]
                {
                    "Just sharing what worked for me.",
                    "Keeping it practical.",
                    "Hope that helps."
                }),
                PersonaStyle.Enthusiastic => AddPhrase(baseLine, seeded, new[]
                {
                    "Honestly this is great.",
                    "Big yes from me.",
                    "Love this."
                }),
                PersonaStyle.CommunityMinded => AddPhrase(baseLine, seeded, new[]
                {
                    "Good for the community.",
                    "This helps people locally.",
                    "Exactly what neighbors need."
                }),
                _ => baseLine
            };
        }

        private static string AddPhrase(string baseLine, System.Random seeded, IReadOnlyList<string> phrases)
        {
            if (phrases == null || phrases.Count == 0) return baseLine;
            string picked = phrases[seeded.Next(phrases.Count)];
            if (string.IsNullOrWhiteSpace(picked)) return baseLine;

            // Small chance to prepend instead of append for less repetitive rhythm.
            if (seeded.NextDouble() < 0.35)
                return $"{picked} {baseLine}";
            return $"{baseLine} {picked}";
        }

        private static IReadOnlyList<string> PickSeededSubset(IReadOnlyList<string> pool, PostData post, bool approved, int count)
        {
            if (pool == null || pool.Count == 0) return Array.Empty<string>();
            if (pool.Count <= count) return pool;

            var mutable = new List<string>(pool);
            int day = GameDatabase.Instance?.Config != null ? GameDatabase.Instance.Config.currentDay : 0;
            string seedKey = $"{post?.id}|{day}|{(approved ? "A" : "D")}";
            var rng = new System.Random(StableHash(seedKey));

            // Fisher-Yates shuffle with seeded RNG.
            for (int i = mutable.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (mutable[i], mutable[j]) = (mutable[j], mutable[i]);
            }

            return mutable.GetRange(0, Mathf.Clamp(count, 1, mutable.Count));
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];
                return hash;
            }
        }

        private static PostContext ExtractContext(PostData post)
        {
            string text = post != null ? (post.originalText ?? post.text ?? "") : "";
            text = NormalizeWhitespace(text);
            if (text.Length == 0)
                return new PostContext("", "", "", "");

            string lower = text.ToLowerInvariant();

            string subject = ExtractSubjectHint(text);
            string action = ExtractActionHint(lower, text);
            string location = ExtractLocationHint(lower, text);
            string extra = ExtractImplicationHint(lower);

            return new PostContext(subject, action, location, extra);
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string ExtractLocationHint(string lower, string original)
        {
            string s = ExtractAfterToken(lower, original, " at ");
            if (!string.IsNullOrEmpty(s)) return $"at {s}";
            s = ExtractAfterToken(lower, original, " in ");
            if (!string.IsNullOrEmpty(s)) return $"in {s}";
            s = ExtractAfterToken(lower, original, " near ");
            if (!string.IsNullOrEmpty(s)) return $"near {s}";
            s = ExtractAfterToken(lower, original, " on ");
            if (!string.IsNullOrEmpty(s)) return $"on {s}";
            return "";
        }

        private static string ExtractAfterToken(string lower, string original, string token)
        {
            int idx = lower.IndexOf(token);
            if (idx < 0) return "";
            int start = idx + token.Length;
            if (start >= original.Length) return "";
            string tail = original.Substring(start).Trim();
            int cut = tail.IndexOfAny(new[] { '.', '!', '?', ',', ';', ':', '\n', '\r' });
            if (cut > 0) tail = tail.Substring(0, cut).Trim();
            if (tail.Length > 44) tail = tail.Substring(0, 44).TrimEnd() + "…";
            return tail;
        }

        private static string ExtractSubjectHint(string original)
        {
            string clean = original.TrimStart('[', ']', ' ');
            int cut = clean.IndexOfAny(new[] { '.', '!', '?', ':', ';' });
            string firstClause = cut > 0 ? clean.Substring(0, cut) : clean;
            var words = firstClause.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "this post";
            int take = Mathf.Clamp(words.Length, 1, 9);
            string subject = string.Join(" ", words, 0, take).Trim();
            if (subject.Length > 60) subject = subject.Substring(0, 60).TrimEnd() + "…";
            return string.IsNullOrEmpty(subject) ? "this post" : subject;
        }

        private static string ExtractActionHint(string lower, string original)
        {
            if (lower.StartsWith("reminder:")) return "the reminder";
            if (lower.StartsWith("psa:")) return "the PSA";
            if (lower.Contains("looking for")) return "finding people involved";
            if (lower.Contains("lost")) return "reporting something lost";
            if (lower.Contains("returned")) return "returning property";
            if (lower.Contains("moved")) return "the schedule change";
            if (lower.Contains("delay")) return "service delays";
            if (lower.Contains("vote")) return "voting guidance";
            if (lower.Contains("off-leash")) return "off-leash behavior";
            if (lower.Contains("rumor")) return "rumor checking";
            if (lower.Contains("report")) return "the report";
            if (lower.Contains("hoax")) return "hoax correction";
            // fallback to short slice of the sentence so it remains anchored
            var words = original.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "the update";
            int start = Mathf.Clamp(words.Length >= 3 ? 2 : 0, 0, words.Length - 1);
            int remaining = words.Length - start;
            int take = Mathf.Clamp(remaining, 1, 5);
            return string.Join(" ", words, start, take).Trim();
        }

        private static string ExtractImplicationHint(string lower)
        {
            if (lower.Contains("safety") || lower.Contains("law") || lower.Contains("rule"))
                return "safety rules affect everyone involved";
            if (lower.Contains("rumor") || lower.Contains("unverified") || lower.Contains("leaked"))
                return "misinformation can spread before facts catch up";
            if (lower.Contains("delay") || lower.Contains("moved") || lower.Contains("closed"))
                return "it changes people's plans quickly";
            if (lower.Contains("volunteer") || lower.Contains("cleanup") || lower.Contains("donate"))
                return "community participation makes a real difference";
            return "people will react quickly";
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

            // Context grounded comments (no generic filler).
            post.commentsApprove = new List<string>(GenerateContextualLines(post, approved: true));
            post.commentsDecline = new List<string>(GenerateContextualLines(post, approved: false));
        }
    }
}
