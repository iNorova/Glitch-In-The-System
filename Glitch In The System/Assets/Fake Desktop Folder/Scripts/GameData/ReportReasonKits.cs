using System;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Pre-written report strings by <see cref="PostCategory"/> and <see cref="ReporterTone"/>.
    /// No runtime text generation — only pool picks. Every post must leave with a non-empty report.
    /// </summary>
    public static class ReportReasonKits
    {
        /// <summary>
        /// Assigns reportReason, reporterTone, and reportCredibility when missing.
        /// Call after caption/category are set on procedural posts.
        /// </summary>
        public static void ApplyIfMissing(PostData post, System.Random rng)
        {
            if (post == null || rng == null) return;
            if (!string.IsNullOrWhiteSpace(post.reportReason))
                return;

            var tone = RollReporterTone(rng, post.category, post.feedKind);
            post.reporterTone = tone;
            post.reportCredibility = CredibilityForTone(tone);
            post.reportReason = PickReport(post.category, tone, rng);
        }

        /// <summary>Explicit tone + reason (hand-authored units).</summary>
        public static void Apply(PostData post, string reportReason, ReporterTone tone, ReportCredibility credibility)
        {
            if (post == null) return;
            post.reportReason = string.IsNullOrWhiteSpace(reportReason)
                ? PickReport(post.category, tone, new System.Random(post.id?.GetHashCode() ?? 0))
                : reportReason;
            post.reporterTone = tone;
            post.reportCredibility = credibility;
        }

        public static ReporterTone RollReporterTone(System.Random rng, PostCategory category, FeedPostKind feedKind)
        {
            int roll = rng.Next(0, 100);

            if (feedKind == FeedPostKind.EmotionalVent)
            {
                if (roll < 25) return ReporterTone.Genuine;
                if (roll < 55) return ReporterTone.Vague;
                if (roll < 75) return ReporterTone.OpinionBased;
                return ReporterTone.FalseReport;
            }

            return category switch
            {
                PostCategory.Misinformation => roll switch
                {
                    < 45 => ReporterTone.Genuine,
                    < 65 => ReporterTone.Vague,
                    < 82 => ReporterTone.BadFaith,
                    < 92 => ReporterTone.OpinionBased,
                    _ => ReporterTone.FalseReport
                },
                PostCategory.Violation => roll switch
                {
                    < 55 => ReporterTone.Genuine,
                    < 75 => ReporterTone.Vague,
                    < 88 => ReporterTone.BadFaith,
                    _ => ReporterTone.FalseReport
                },
                PostCategory.Narrative => roll switch
                {
                    < 30 => ReporterTone.Genuine,
                    < 50 => ReporterTone.Vague,
                    < 70 => ReporterTone.OpinionBased,
                    < 85 => ReporterTone.BadFaith,
                    _ => ReporterTone.FalseReport
                },
                PostCategory.GrayArea => roll switch
                {
                    < 28 => ReporterTone.Genuine,
                    < 52 => ReporterTone.Vague,
                    < 72 => ReporterTone.OpinionBased,
                    < 88 => ReporterTone.FalseReport,
                    _ => ReporterTone.BadFaith
                },
                PostCategory.AlgorithmManipulation => roll switch
                {
                    < 40 => ReporterTone.Genuine,
                    < 65 => ReporterTone.OpinionBased,
                    < 82 => ReporterTone.Vague,
                    _ => ReporterTone.BadFaith
                },
                _ => roll switch
                {
                    < 20 => ReporterTone.Genuine,
                    < 40 => ReporterTone.Vague,
                    < 60 => ReporterTone.OpinionBased,
                    < 78 => ReporterTone.FalseReport,
                    _ => ReporterTone.BadFaith
                }
            };
        }

        public static ReportCredibility CredibilityForTone(ReporterTone tone) =>
            tone switch
            {
                ReporterTone.Genuine => ReportCredibility.Credible,
                ReporterTone.Vague => ReportCredibility.Unclear,
                ReporterTone.BadFaith => ReportCredibility.LikelyFalseReport,
                ReporterTone.FalseReport => ReportCredibility.LikelyFalseReport,
                ReporterTone.OpinionBased => ReportCredibility.Unclear,
                _ => ReportCredibility.Unclear
            };

        public static string PickReport(PostCategory category, ReporterTone tone, System.Random rng)
        {
            var pool = GetPool(category, tone);
            if (pool == null || pool.Length == 0)
                pool = FallbackAny;
            return pool[rng.Next(pool.Length)];
        }

        private static string[] GetPool(PostCategory category, ReporterTone tone)
        {
            return (category, tone) switch
            {
                (PostCategory.Misinformation, ReporterTone.Genuine) => MisinfoGenuine,
                (PostCategory.Misinformation, ReporterTone.Vague) => MisinfoVague,
                (PostCategory.Misinformation, ReporterTone.BadFaith) => MisinfoBadFaith,
                (PostCategory.Misinformation, ReporterTone.OpinionBased) => MisinfoOpinion,
                (PostCategory.Misinformation, ReporterTone.FalseReport) => MisinfoFalseReport,

                (PostCategory.Violation, ReporterTone.Genuine) => ViolationGenuine,
                (PostCategory.Violation, ReporterTone.Vague) => ViolationVague,
                (PostCategory.Violation, ReporterTone.BadFaith) => ViolationBadFaith,
                (PostCategory.Violation, ReporterTone.FalseReport) => ViolationFalseReport,
                (PostCategory.Violation, ReporterTone.OpinionBased) => ViolationOpinion,

                (PostCategory.Narrative, ReporterTone.Genuine) => NarrativeGenuine,
                (PostCategory.Narrative, ReporterTone.Vague) => NarrativeVague,
                (PostCategory.Narrative, ReporterTone.OpinionBased) => NarrativeOpinion,
                (PostCategory.Narrative, ReporterTone.BadFaith) => NarrativeBadFaith,
                (PostCategory.Narrative, ReporterTone.FalseReport) => NarrativeFalseReport,

                (PostCategory.GrayArea, ReporterTone.Genuine) => GrayGenuine,
                (PostCategory.GrayArea, ReporterTone.Vague) => GrayVague,
                (PostCategory.GrayArea, ReporterTone.OpinionBased) => GrayOpinion,
                (PostCategory.GrayArea, ReporterTone.FalseReport) => GrayFalseReport,
                (PostCategory.GrayArea, ReporterTone.BadFaith) => GrayBadFaith,

                (PostCategory.AlgorithmManipulation, ReporterTone.Genuine) => AlgoGenuine,
                (PostCategory.AlgorithmManipulation, ReporterTone.OpinionBased) => AlgoOpinion,
                (PostCategory.AlgorithmManipulation, ReporterTone.Vague) => AlgoVague,
                (PostCategory.AlgorithmManipulation, ReporterTone.BadFaith) => AlgoBadFaith,
                (PostCategory.AlgorithmManipulation, ReporterTone.FalseReport) => AlgoFalseReport,

                (PostCategory.Harmless, ReporterTone.Genuine) => HarmlessGenuine,
                (PostCategory.Harmless, ReporterTone.Vague) => HarmlessVague,
                (PostCategory.Harmless, ReporterTone.OpinionBased) => HarmlessOpinion,
                (PostCategory.Harmless, ReporterTone.FalseReport) => HarmlessFalseReport,
                (PostCategory.Harmless, ReporterTone.BadFaith) => HarmlessBadFaith,

                _ => null
            };
        }

        // —— Misinformation ——
        private static readonly string[] MisinfoGenuine = {
            "This post is spreading false medical information and linking to a dangerous website.",
            "Election claim in the caption has no source — looks like coordinated misinformation.",
            "Video uses old footage from another country; timestamp in the caption is wrong.",
            "User is sharing a debunked hoax about public health — please review urgently."
        };
        private static readonly string[] MisinfoVague = {
            "misinformation",
            "idk this seems fake",
            "something off about this post",
            "can someone check this??"
        };
        private static readonly string[] MisinfoBadFaith = {
            "REPORT: truth-teller silenced again",
            "enemy propaganda — remove the debunkers instead",
            "this account is exposing corruption, ban the reporters"
        };
        private static readonly string[] MisinfoOpinion = {
            "I disagree with their politics so it should come down",
            "I don't like this take — feels like misinformation",
            "my feed shouldn't show opinions like this"
        };
        private static readonly string[] MisinfoFalseReport = {
            "fake news (it's actually my own repost of official news)",
            "misinformation — reported because I don't like the author",
            "dangerous content (post is literally a fact-check thread)"
        };

        // —— Violation / scam ——
        private static readonly string[] ViolationGenuine = {
            "Obvious scam link in the caption — phishing pattern we've seen before.",
            "Selling prohibited weight-loss products with fake before/after photos.",
            "Impersonating a charity — URL does not match the real organization.",
            "Harassment and doxxing in the attached thread."
        };
        private static readonly string[] ViolationVague = {
            "scam",
            "spam",
            "this is bad",
            "rule violation"
        };
        private static readonly string[] ViolationBadFaith = {
            "competitor ad — remove so my shop gets traffic",
            "reporting because they undercut my prices",
            "spam (it's a normal marketplace listing)"
        };
        private static readonly string[] ViolationOpinion = {
            "I don't like ads on my timeline",
            "too salesy for me",
            "shouldn't allow promos like this"
        };
        private static readonly string[] ViolationFalseReport = {
            "scam (user is giving away free event tickets legitimately)",
            "harassment — they said my comment was wrong",
            "spam report on a lost-pet post"
        };

        // —— Narrative / political ——
        private static readonly string[] NarrativeGenuine = {
            "Panic-inducing political claim with no citations — could cause real-world harm.",
            "Post implies election fraud without evidence; needs fact-check hold.",
            "User is amplifying unverified 'leaked' documents about city infrastructure."
        };
        private static readonly string[] NarrativeVague = {
            "politics",
            "panic inducing",
            "idk feels like propaganda"
        };
        private static readonly string[] NarrativeOpinion = {
            "I don't agree with their worldview",
            "too political for a Tuesday",
            "mute this kind of content"
        };
        private static readonly string[] NarrativeBadFaith = {
            "silence dissent",
            "protect the establishment narrative",
            "ban anyone asking questions"
        };
        private static readonly string[] NarrativeFalseReport = {
            "misinformation (post is quoting public meeting minutes)",
            "election interference (it's a voting reminder with official links)",
            "fear mongering (they're debunking a hoax)"
        };

        // —— Gray area ——
        private static readonly string[] GrayGenuine = {
            "Possible self-harm tone in the caption — unclear if the user needs outreach.",
            "Satire post with no label; comments are fighting about whether it's real.",
            "Unverified rumor about local policy — could spread before officials respond."
        };
        private static readonly string[] GrayVague = {
            "idk it just seems wrong",
            "weird vibe",
            "can mods look at this",
            "something about this post"
        };
        private static readonly string[] GrayOpinion = {
            "i just think this is mean",
            "don't like their attitude in the caption",
            "offensive to me personally"
        };
        private static readonly string[] GrayFalseReport = {
            "harassment (they criticized my favorite team)",
            "misinformation (it's an opinion thread)",
            "bullying (post is asking for help)"
        };
        private static readonly string[] GrayBadFaith = {
            "remove before people see the truth",
            "brigading (author posted factual local info)",
            "spam (single paragraph vent)"
        };

        // —— Harmless / personal ——
        private static readonly string[] HarmlessGenuine = {
            "User may be doxxing someone in the comments — caption looks innocent.",
            "Lost-pet post might be harvesting contact info via third-party link.",
            "Caption mentions a meetup at a private address — safety check requested."
        };
        private static readonly string[] HarmlessVague = {
            "not sure this needs review?",
            "idk",
            "felt off",
            "none"
        };
        private static readonly string[] HarmlessOpinion = {
            "i don't like meal prep posts",
            "too boring for the feed",
            "don't care about their dog"
        };
        private static readonly string[] HarmlessFalseReport = {
            "harassment (they said my boss is toxic — it's their own vent)",
            "spam (community volunteer signup)",
            "misinformation (weather update for a picnic)"
        };
        private static readonly string[] HarmlessBadFaith = {
            "spam — remove competitor's bake sale post",
            "off-topic (I want more drama on my timeline)",
            "violation (wholesome content)"
        };

        // —— Algorithm / platform ——
        private static readonly string[] AlgoGenuine = {
            "Author documents suspicious engagement spikes on benign posts — possible system manipulation.",
            "Appeal: post removed with no explanation; user pasted moderation IDs.",
            "Thread shows feed ranking anomalies — requesting integrity review."
        };
        private static readonly string[] AlgoOpinion = {
            "I don't want meta posts about the algorithm",
            "too negative about the platform",
            "complaining isn't content"
        };
        private static readonly string[] AlgoVague = {
            "appeal",
            "algorithm",
            "unfair",
            "shadowban??"
        };
        private static readonly string[] AlgoBadFaith = {
            "stop them from exposing the platform",
            "ban whistleblower account",
            "misinformation about our company"
        };
        private static readonly string[] AlgoFalseReport = {
            "spam (legitimate bug report with screenshots)",
            "harassment (they said the algorithm is biased — it's a chart)",
            "off-topic (actual policy discussion)"
        };

        private static readonly string[] FallbackAny = {
            "Flagged for review — user did not leave details.",
            "Community report: content may violate policy.",
            "Automated integrity queue: engagement pattern unusual.",
            "idk reporting this just in case"
        };
    }
}
