using System;
using System.Collections.Generic;
using System.Linq;

namespace GlitchInTheSystem.GameData
{
    public enum ModerationValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ModerationValidationIssue
    {
        public ModerationValidationSeverity Severity;
        public string Scope;
        public string Id;
        public string Message;

        public override string ToString()
        {
            string id = string.IsNullOrWhiteSpace(Id) ? "" : $" [{Id}]";
            return $"{Severity}: {Scope}{id} - {Message}";
        }
    }

    public sealed class ModerationValidationReport
    {
        public readonly List<ModerationValidationIssue> Issues = new();
        public int CheckedQueueEntries;
        public int CheckedPosts;

        public int ErrorCount => Issues.Count(i => i.Severity == ModerationValidationSeverity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == ModerationValidationSeverity.Warning);
        public bool Passed => ErrorCount == 0;

        public string Summary =>
            $"Moderation content validation: {ErrorCount} error(s), {WarningCount} warning(s), " +
            $"{CheckedQueueEntries} pool entr{(CheckedQueueEntries == 1 ? "y" : "ies")}, {CheckedPosts} post(s) checked.";
    }

    /// <summary>
    /// Shared validation rules for authored moderation content. The Unity Editor menu wraps this class,
    /// and EditMode tests can call it without depending on UnityEditor APIs.
    /// </summary>
    public static class ModerationContentValidator
    {
        private const int ExpectedIntroTutorialPosts = IntroTutorialContent.TutorialPostCount;
        private const int ExpectedDay1Posts = 9;
        private const int ExpectedDay2Posts = 11;
        private const int ExpectedDay3Posts = 13;

        public static ModerationValidationReport ValidateAll()
        {
            var report = new ModerationValidationReport();

            ValidateQueueEntries(
                "ModerationContentPools.AllQueueEntries",
                ModerationContentPools.AllQueueEntries,
                report);

            var users = BuildValidationUsers(48);
            var rng = new System.Random(1905);

            ValidatePosts(
                "ModerationSamplePosts.Build",
                ModerationSamplePosts.Build(users),
                report,
                requireUniqueIdsInScope: true);

            ValidateIntroTutorial(report, rng);
            ValidateScriptedDay(1, ExpectedDay1Posts, users, report, rng);
            ValidateScriptedDay(2, ExpectedDay2Posts, users, report, rng);
            ValidateScriptedDay(3, ExpectedDay3Posts, users, report, rng);

            return report;
        }

        public static void ValidateQueueEntries(
            string scope,
            IReadOnlyList<ModerationContentPools.ModerationEntry> entries,
            ModerationValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            if (entries == null)
            {
                Add(report, ModerationValidationSeverity.Error, scope, null, "Queue entry list is null.");
                return;
            }

            report.CheckedQueueEntries += entries.Count;

            if (entries.Count == 0)
                Add(report, ModerationValidationSeverity.Error, scope, null, "Queue entry list is empty.");

            var seenText = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string id = $"entry_{i:000}";
                string text = NormalizeKey(entry.Text);

                if (string.IsNullOrWhiteSpace(entry.Text))
                    Add(report, ModerationValidationSeverity.Error, scope, id, "Post text is empty.");
                else if (!seenText.Add(text))
                    Add(report, ModerationValidationSeverity.Warning, scope, id, "Duplicate queue text found.");

                if (string.IsNullOrWhiteSpace(entry.ReportReason))
                    Add(report, ModerationValidationSeverity.Warning, scope, id, "Report reason is empty.");

                ValidateSeverity(scope, id, entry.Category, entry.Severity, report);

                if (entry.Format == PostPresentationFormat.TextWithImageDescription
                    && string.IsNullOrWhiteSpace(entry.ImageDescription))
                {
                    Add(report, ModerationValidationSeverity.Warning, scope, id,
                        "Format expects an image description, but ImageDescription is empty.");
                }

                if (entry.Category == PostCategory.Misinformation
                    && entry.ReportCredibility == ReportCredibility.LikelyFalseReport)
                {
                    Add(report, ModerationValidationSeverity.Warning, scope, id,
                        "Misinformation entry uses LikelyFalseReport credibility. Confirm this is intentional.");
                }

                if (entry.Category == PostCategory.Violation
                    && entry.ReporterTone == ReporterTone.FalseReport)
                {
                    Add(report, ModerationValidationSeverity.Warning, scope, id,
                        "Violation entry uses FalseReport tone. Confirm this is intentional.");
                }
            }
        }

        public static void ValidatePosts(
            string scope,
            IReadOnlyList<PostData> posts,
            ModerationValidationReport report,
            bool requireUniqueIdsInScope)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            if (posts == null)
            {
                Add(report, ModerationValidationSeverity.Error, scope, null, "Post list is null.");
                return;
            }

            report.CheckedPosts += posts.Count;

            if (posts.Count == 0)
                Add(report, ModerationValidationSeverity.Error, scope, null, "Post list is empty.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < posts.Count; i++)
            {
                var post = posts[i];
                if (post == null)
                {
                    Add(report, ModerationValidationSeverity.Error, scope, $"post_{i:000}", "Post is null.");
                    continue;
                }

                string id = string.IsNullOrWhiteSpace(post.id) ? $"post_{i:000}" : post.id;

                if (string.IsNullOrWhiteSpace(post.id))
                    Add(report, ModerationValidationSeverity.Error, scope, id, "Post id is empty.");
                else if (requireUniqueIdsInScope && !ids.Add(post.id))
                    Add(report, ModerationValidationSeverity.Error, scope, id, "Duplicate post id in this collection.");

                if (string.IsNullOrWhiteSpace(post.authorUserId))
                    Add(report, ModerationValidationSeverity.Error, scope, id, "Author user id is empty.");

                if (string.IsNullOrWhiteSpace(post.text))
                    Add(report, ModerationValidationSeverity.Error, scope, id, "Post text is empty.");

                if (string.IsNullOrWhiteSpace(post.timestampLabel))
                    Add(report, ModerationValidationSeverity.Warning, scope, id, "Timestamp label is empty.");

                if (string.IsNullOrWhiteSpace(post.reportReason)
                    && post.category != PostCategory.Harmless)
                {
                    Add(report, ModerationValidationSeverity.Warning, scope, id,
                        "Non-harmless post has no report reason.");
                }

                ValidateSeverity(scope, id, post.category, post.severity, report);

                if (post.presentationFormat == PostPresentationFormat.TextWithImageDescription
                    && string.IsNullOrWhiteSpace(post.imageDescription))
                {
                    Add(report, ModerationValidationSeverity.Warning, scope, id,
                        "Post format expects an image description, but ImageDescription is empty.");
                }
            }
        }

        private static void ValidateIntroTutorial(ModerationValidationReport report, System.Random rng)
        {
            var users = new List<UserProfileData>();
            var posts = new List<PostData>();
            var queue = new List<PostData>();

            IntroTutorialContent.BuildInto(users, posts, queue, rng);

            if (queue.Count != ExpectedIntroTutorialPosts)
            {
                Add(report, ModerationValidationSeverity.Error, "IntroTutorialContent", null,
                    $"Expected {ExpectedIntroTutorialPosts} tutorial posts, found {queue.Count}.");
            }

            ValidatePosts("IntroTutorialContent.Queue", queue, report, requireUniqueIdsInScope: true);
        }

        private static void ValidateScriptedDay(
            int day,
            int expectedCount,
            IReadOnlyList<UserProfileData> users,
            ModerationValidationReport report,
            System.Random rng)
        {
            string scope = $"DayScheduleContent.Day{day}";
            var queue = DayScheduleContent.BuildModerationQueue(day, users, rng);

            if (queue.Count != expectedCount)
            {
                Add(report, ModerationValidationSeverity.Error, scope, null,
                    $"Expected {expectedCount} scripted posts, found {queue.Count}.");
            }

            ValidatePosts(scope, queue, report, requireUniqueIdsInScope: true);

            if (day == 2)
                ValidateDay2Hooks(scope, queue, report);
            else if (day == 3)
                ValidateDay3Hooks(scope, queue, report);
        }

        private static void ValidateDay2Hooks(
            string scope,
            IReadOnlyList<PostData> queue,
            ModerationValidationReport report)
        {
            var viral = queue.FirstOrDefault(p => p != null && p.id == NarrativeIds.ViralMisinformationPostId);
            if (viral == null)
            {
                Add(report, ModerationValidationSeverity.Error, scope, NarrativeIds.ViralMisinformationPostId,
                    "Day 2 viral misinformation post is missing.");
                return;
            }

            if (viral.category != PostCategory.Misinformation)
            {
                Add(report, ModerationValidationSeverity.Error, scope, viral.id,
                    "Day 2 viral post must be categorized as Misinformation.");
            }

            if (viral.severity < 3)
            {
                Add(report, ModerationValidationSeverity.Warning, scope, viral.id,
                    "Day 2 viral misinformation post should be severity 3.");
            }
        }

        private static void ValidateDay3Hooks(
            string scope,
            IReadOnlyList<PostData> queue,
            ModerationValidationReport report)
        {
            var hook = queue.FirstOrDefault(p => p != null && p.id == DayPacing.Day3OverridePostId);
            if (hook == null)
            {
                Add(report, ModerationValidationSeverity.Error, scope, DayPacing.Day3OverridePostId,
                    "Day 3 forced override hook post is missing.");
                return;
            }

            if (hook.category != PostCategory.Harmless && hook.category != PostCategory.GrayArea)
            {
                Add(report, ModerationValidationSeverity.Error, scope, hook.id,
                    "Day 3 override hook should be Harmless or GrayArea so the override reads clearly.");
            }
        }

        private static void ValidateSeverity(
            string scope,
            string id,
            PostCategory category,
            int severity,
            ModerationValidationReport report)
        {
            if (severity < 0 || severity > 3)
            {
                Add(report, ModerationValidationSeverity.Error, scope, id,
                    $"Severity must be 0-3, found {severity}.");
                return;
            }

            if ((category == PostCategory.Misinformation || category == PostCategory.Violation) && severity < 2)
            {
                Add(report, ModerationValidationSeverity.Warning, scope, id,
                    $"{category} content usually needs severity 2 or 3.");
            }

            if (category == PostCategory.Harmless && severity > 1)
            {
                Add(report, ModerationValidationSeverity.Warning, scope, id,
                    "Harmless content has high severity. Confirm this is intentional.");
            }
        }

        private static List<UserProfileData> BuildValidationUsers(int count)
        {
            var users = new List<UserProfileData>();
            for (int i = 0; i < count; i++)
            {
                users.Add(new UserProfileData
                {
                    id = $"validation_user_{i}",
                    username = $"validation_user_{i}",
                    displayName = $"Validation User {i}",
                    accountAgeYears = 2,
                    followers = 100 + i,
                    following = 50 + i,
                    strikes = i % 3,
                    reputation = "Neutral",
                    risk = i % 4 == 0 ? "Medium" : "Low"
                });
            }

            return users;
        }

        private static void Add(
            ModerationValidationReport report,
            ModerationValidationSeverity severity,
            string scope,
            string id,
            string message)
        {
            report.Issues.Add(new ModerationValidationIssue
            {
                Severity = severity,
                Scope = scope,
                Id = id,
                Message = message
            });
        }

        private static string NormalizeKey(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
