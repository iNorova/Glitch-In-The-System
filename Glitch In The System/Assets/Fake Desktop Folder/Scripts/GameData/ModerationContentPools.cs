using System;
using System.Collections.Generic;
using UnityEngine;

// PostVoice lives in PostVoice.cs

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Pre-written moderation and feed strings (no runtime generation).
    /// Teams can move entries into <see cref="ModerationContentLibrary"/> assets over time.
    /// </summary>
    public static class ModerationContentPools
    {
        [Serializable]
        public struct ModerationEntry
        {
            public string Text;
            public string ReportReason;
            public string ImageDescription;
            public PostPresentationFormat Format;
            public PostCategory Category;
            public int Severity;
            public string[] ApproveComments;
            public string[] DeclineComments;
            public ReporterTone ReporterTone;
            public ReportCredibility ReportCredibility;
            public PostVoice PostVoice;

            public ModerationEntry(
                string text,
                string reportReason,
                string imageDescription,
                PostPresentationFormat format,
                PostCategory category,
                int severity,
                string[] approveComments = null,
                string[] declineComments = null,
                ReporterTone reporterTone = ReporterTone.Genuine,
                ReportCredibility reportCredibility = ReportCredibility.Credible,
                PostVoice postVoice = PostVoice.Neutral)
            {
                Text = text;
                ReportReason = reportReason;
                ImageDescription = imageDescription;
                Format = format;
                Category = category;
                Severity = severity;
                ApproveComments = approveComments;
                DeclineComments = declineComments;
                ReporterTone = reporterTone;
                ReportCredibility = reportCredibility;
                PostVoice = postVoice;
            }
        }

        private static ModerationEntry[] _allQueueEntriesMerged;

        // —— Borderline / mundane / political / emotional mix for queue variety ——
        private static readonly ModerationEntry[] QueueEntries =
        {
            new(
                "Meal prep Sunday. Nothing political, just chicken and rice.",
                "Not sure this needs review?",
                null,
                PostPresentationFormat.TextOnly,
                PostCategory.Harmless,
                0),
            new(
                "Reposting city council livestream notes — zoning vote at 7pm.",
                "User flagged as 'spam' but it's literally a public meeting link.",
                "Screenshot of council agenda PDF, page 2 highlighted.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.GrayArea,
                1,
                new[] { "Thanks for posting local info.", "Finally something useful." },
                new[] { "Why is this on my feed?", "Boring." }),
            new(
                "I can't stop crying today. If you knew what happened you wouldn't either.",
                "Concerned about self-harm tone — unclear if real.",
                null,
                PostPresentationFormat.TextOnly,
                PostCategory.GrayArea,
                2,
                new[] { "Sending love.", "You're not alone." },
                new[] { "Seek help.", "Performative." }),
            new(
                "They're going to ban gas stoves nationwide next month. My uncle in EMS confirmed.",
                "Misinformation",
                "Blurred photo of a text thread with no names visible.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.Misinformation,
                2,
                reporterTone: ReporterTone.Vague,
                reportCredibility: ReportCredibility.Unclear),
            new(
                "This meme is harmless but people in comments are fighting.",
                "bullying",
                "Image: two-panel meme about laundry.",
                PostPresentationFormat.TextWithAttachedComments,
                PostCategory.GrayArea,
                1,
                new[] { "It's just a joke lol", "Let people laugh" },
                new[] { "Comments are toxic", "Lock thread" }),
            new(
                "FLASH SALE 70% OFF weight loss gummies click link bio",
                "Scam / prohibited goods",
                "Product photo with exaggerated before/after.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.Violation,
                3),
            new(
                "Does anyone else think the new policy is weird or is it just me?",
                "idk",
                null,
                PostPresentationFormat.TextOnly,
                PostCategory.GrayArea,
                1,
                reporterTone: ReporterTone.Vague,
                reportCredibility: ReportCredibility.Unclear),
            new(
                "BREAKING: candidate caught on hot mic — audio in replies (unverified).",
                "Election interference???",
                "Waveform screenshot, no source cited.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.Misinformation,
                3,
                new[] { "Finally someone said it", "Sharing everywhere" },
                new[] { "Fake audio", "Reported" }),
            new(
                "Just adopted this dog. Look at his little hat.",
                "none",
                "Photo: small dog wearing knitted hat.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.Harmless,
                0,
                reporterTone: ReporterTone.Vague,
                reportCredibility: ReportCredibility.Unclear),
            new(
                "If the numbers from yesterday are true we're being lied to on purpose.",
                "User reports 'panic inducing' — no specific rule cited",
                null,
                PostPresentationFormat.TextWithAttachedComments,
                PostCategory.Narrative,
                2,
                new[] { "Source?", "Which numbers?" },
                new[] { "Fear mongering", "Muted." }),
            new(
                "satire account but caption doesn't say satire",
                "misleading",
                "Screenshot of tweet with no verification badge.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.GrayArea,
                1),
            new(
                "My boss made us share 'positive culture' posts. This feels gross.",
                "HR complaint / off-topic",
                null,
                PostPresentationFormat.TextOnly,
                PostCategory.Harmless,
                0),
            new(
                "They deleted my last post for 'community standards' with no explanation.",
                "Appeal",
                null,
                PostPresentationFormat.TextOnly,
                PostCategory.AlgorithmManipulation,
                1),
            new(
                "Video claims hospital ER is empty — filmed at 3am.",
                "COVID misinformation (2021 report reused?)",
                "Shaky phone video, empty waiting room.",
                PostPresentationFormat.TextWithImageDescription,
                PostCategory.Misinformation,
                2),
            new(
                "Quote-tweeting article: 'City water safe' — comments are a war zone.",
                "Brigading in replies",
                "News site headline screenshot.",
                PostPresentationFormat.TextWithAttachedComments,
                PostCategory.GrayArea,
                2)
        };

        // —— Ambient feed-only lines (personal, meme, ad, viral) ——
        private static readonly (string text, FeedPostKind kind, PostCategory cat)[] FeedAmbient =
        {
            ("3am thoughts: why do group chats die the second you need them", FeedPostKind.EmotionalVent, PostCategory.Harmless),
            ("Repost: local bakery giving free coffee to nurses this week", FeedPostKind.NewsRepost, PostCategory.Harmless),
            ("when you open the fridge and forget what you wanted 😭", FeedPostKind.Meme, PostCategory.Harmless),
            ("Sponsored: Try CloudDesk Pro — meetings that almost feel human", FeedPostKind.SponsoredAd, PostCategory.Harmless),
            ("this clip has no right to go this hard [sound on]", FeedPostKind.ViralClip, PostCategory.GrayArea),
            ("finally told my family I'm moving out. scary but good.", FeedPostKind.PersonalUpdate, PostCategory.Harmless),
            ("thread: budget meal ideas under $6 (no affiliate links)", FeedPostKind.PersonalUpdate, PostCategory.Harmless),
            ("people are mad in comments but the cat video is still elite", FeedPostKind.Meme, PostCategory.Harmless)
        };

        /// <summary>Core + extended caption pool (Step 3).</summary>
        public static IReadOnlyList<ModerationEntry> AllQueueEntries
        {
            get
            {
                if (_allQueueEntriesMerged != null) return _allQueueEntriesMerged;
                var ext = ModerationContentPoolsExtended.AdditionalQueueEntries;
                _allQueueEntriesMerged = new ModerationEntry[QueueEntries.Length + ext.Length];
                Array.Copy(QueueEntries, _allQueueEntriesMerged, QueueEntries.Length);
                Array.Copy(ext, 0, _allQueueEntriesMerged, QueueEntries.Length, ext.Length);
                return _allQueueEntriesMerged;
            }
        }

        public static PostData BuildPostFromEntry(ModerationEntry entry, UserProfileData author, string id, System.Random rng)
        {
            var post = new PostData
            {
                id = id,
                authorUserId = author.id,
                text = entry.Text,
                reportReason = entry.ReportReason,
                imageDescription = entry.ImageDescription,
                presentationFormat = entry.Format,
                category = entry.Category,
                severity = entry.Severity,
                timestampLabel = $"{rng.Next(3, 58)}m",
                isPublished = false
            };

            post.feedKind = MapFeedKind(entry.Category, entry.Format);
            post.reporterTone = entry.ReporterTone;
            post.reportCredibility = entry.ReportCredibility;
            post.postVoice = entry.PostVoice;
            ReportReasonKits.ApplyIfMissing(post, rng);

            if (entry.ApproveComments != null)
                post.commentsApprove.AddRange(entry.ApproveComments);
            if (entry.DeclineComments != null)
                post.commentsDecline.AddRange(entry.DeclineComments);

            OrganicEngagementUtility.ApplyToPost(post, rng, entry.Category);
            PostManager.AssignDefaultBranches(post, rng);
            return post;
        }

        public static PostData BuildAmbientFeedPost(UserProfileData author, int index, System.Random rng)
        {
            var sample = FeedAmbient[index % FeedAmbient.Length];
            var post = new PostData
            {
                id = $"feed_ambient_{index}",
                authorUserId = author.id,
                text = sample.text,
                feedKind = sample.kind,
                category = sample.cat,
                severity = 0,
                timestampLabel = $"{rng.Next(1, 12)}h",
                isPublished = true,
                presentationFormat = PostPresentationFormat.TextOnly
            };
            OrganicEngagementUtility.ApplyToPost(post, rng, sample.cat);
            ReportReasonKits.ApplyIfMissing(post, rng);
            PostManager.AssignDefaultBranches(post, rng);
            return post;
        }

        private static FeedPostKind MapFeedKind(PostCategory category, PostPresentationFormat format)
        {
            if (format == PostPresentationFormat.TextWithAttachedComments) return FeedPostKind.ViralClip;
            return category switch
            {
                PostCategory.Misinformation => FeedPostKind.NewsRepost,
                PostCategory.Violation => FeedPostKind.SponsoredAd,
                PostCategory.Narrative => FeedPostKind.NewsRepost,
                PostCategory.AlgorithmManipulation => FeedPostKind.PersonalUpdate,
                _ => FeedPostKind.PersonalUpdate
            };
        }
    }
}
