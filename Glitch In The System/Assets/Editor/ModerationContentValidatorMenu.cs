#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using GlitchInTheSystem.GameData;
using UnityEditor;
using UnityEngine;

namespace GlitchInTheSystem.Editor
{
    public static class ModerationContentValidatorMenu
    {
        [MenuItem("Glitch In The System/Tools/Validate Moderation Content", false, 120)]
        public static void ValidateModerationContent()
        {
            var report = ModerationContentValidator.ValidateAll();

            if (report.Issues.Count == 0)
            {
                Debug.Log(report.Summary);
            }
            else
            {
                foreach (var issue in report.Issues.OrderByDescending(i => i.Severity))
                {
                    if (issue.Severity == ModerationValidationSeverity.Error)
                        Debug.LogError(issue.ToString());
                    else if (issue.Severity == ModerationValidationSeverity.Warning)
                        Debug.LogWarning(issue.ToString());
                    else
                        Debug.Log(issue.ToString());
                }

                Debug.Log(report.Summary);
            }

            EditorUtility.DisplayDialog(
                "Moderation Content Validation",
                report.Summary,
                report.Passed ? "Looks good" : "Review errors");
        }

        [MenuItem("Glitch In The System/Tools/Run Moderation Validator Self Test", false, 121)]
        public static void RunValidatorSelfTest()
        {
            var failures = new List<string>();

            var currentContent = ModerationContentValidator.ValidateAll();
            if (currentContent.ErrorCount != 0)
                failures.Add("Current authored content should not contain validation errors.\n" +
                             string.Join("\n", currentContent.Issues));

            var brokenPostReport = new ModerationValidationReport();
            ModerationContentValidator.ValidatePosts(
                "BrokenPostFixture",
                new List<PostData>
                {
                    new()
                    {
                        id = "",
                        authorUserId = "",
                        text = "",
                        timestampLabel = "",
                        category = PostCategory.Misinformation,
                        severity = 7
                    }
                },
                brokenPostReport,
                requireUniqueIdsInScope: true);

            if (brokenPostReport.ErrorCount < 4)
                failures.Add("Broken post fixture should produce at least 4 errors.\n" +
                             string.Join("\n", brokenPostReport.Issues));

            var brokenEntryReport = new ModerationValidationReport();
            ModerationContentValidator.ValidateQueueEntries(
                "BrokenEntryFixture",
                new[]
                {
                    new ModerationContentPools.ModerationEntry(
                        text: "",
                        reportReason: "",
                        imageDescription: "",
                        format: PostPresentationFormat.TextWithImageDescription,
                        category: PostCategory.Violation,
                        severity: -1)
                },
                brokenEntryReport);

            if (brokenEntryReport.ErrorCount < 2 || brokenEntryReport.WarningCount < 2)
                failures.Add("Broken queue entry fixture should produce at least 2 errors and 2 warnings.\n" +
                             string.Join("\n", brokenEntryReport.Issues));

            if (failures.Count > 0)
                throw new InvalidOperationException("Moderation validator self-test failed:\n\n" +
                                                    string.Join("\n\n", failures));

            Debug.Log("Moderation validator self-test passed.");
        }
    }
}
#endif
