namespace GlitchInTheSystem.GameData
{
    /// <summary>Hand-authored comment threads for days 1–3 scripted posts (Step 5).</summary>
    internal static class DayScheduleContentThreads
    {
        public static void Apply(PostData post)
        {
            if (post == null || string.IsNullOrEmpty(post.id)) return;

            switch (post.id)
            {
                case "p_d1_01":
                    ReportReasonKits.Apply(post,
                        "User flagged BOGO post as spam but it's a local business shout-out.",
                        ReporterTone.FalseReport, ReportCredibility.LikelyFalseReport);
                    post.postVoice = PostVoice.Neutral;
                    PostManager.SetApproveThread(post,
                        new PostCommentLine { displayHandle = "muffinfan42", text = "their blueberry ones are insane actually" },
                        new PostCommentLine { displayHandle = "jenna_reads", text = "ty for posting this i never check shop windows", replyToIndex = 0 });
                    PostManager.SetDeclineThread(post,
                        new PostCommentLine { displayHandle = "coldbrew_carl", text = "why is coffee promo in my moderation queue lol" });
                    break;

                case "p_d1_02":
                    ReportReasonKits.Apply(post,
                        "Lost pet post — user worried contact info in comments could be scraped.",
                        ReporterTone.Genuine, ReportCredibility.Credible);
                    PostManager.SetApproveThread(post,
                        new PostCommentLine { displayHandle = "rivertrail_walker", text = "saw a golden near the bridge 20 min ago??" },
                        new PostCommentLine { displayHandle = "maple_owner", text = "OMGG sending this to my neighborhood chat", replyToIndex = 0 },
                        new PostCommentLine { displayHandle = "vet_tech_sam", text = "check the shelter on pine st too just in case" });
                    PostManager.SetDeclineThread(post,
                        new PostCommentLine { displayHandle = "skeptical_greg", text = "these always turn out to be marketing stunts" });
                    break;

                case "p_d1_05":
                    ReportReasonKits.Apply(post,
                        "idk the curfew rumor sounds fake but people are sharing it",
                        ReporterTone.Vague, ReportCredibility.Unclear);
                    PostManager.SetApproveThread(post,
                        new PostCommentLine { displayHandle = "night_shift_ali", text = "yeah i heard the same thing from a bus driver" },
                        new PostCommentLine { displayHandle = "factcheck_nerd", text = "any link tho??", replyToIndex = 0 });
                    PostManager.SetDeclineThread(post,
                        new PostCommentLine { displayHandle = "city_hall_intern", text = "no curfew vote on the agenda — please stop spreading this" });
                    break;

                case NarrativeIds.ViralMisinformationPostId:
                    ReportReasonKits.Apply(post,
                        "User reports false public-health claims about city water — unverified 'leaked memo' image.",
                        ReporterTone.Genuine, ReportCredibility.Credible);
                    PostManager.SetApproveThread(post,
                        new PostCommentLine { displayHandle = "panicked_parent", text = "stocking bottled water tonight just in case" },
                        new PostCommentLine { displayHandle = "share_bot_99", text = "SHARE SHARE SHARE", botFlag = true },
                        new PostCommentLine { displayHandle = "local_reporter", text = "city account hasn't confirmed anything", replyToIndex = 0 });
                    PostManager.SetDeclineThread(post,
                        new PostCommentLine { displayHandle = "hydrology_phd", text = "this memo was debunked last year — timestamp is wrong" },
                        new PostCommentLine { displayHandle = "mod_appreciator", text = "thank you for catching this early" });
                    break;

                case DayPacing.Day3OverridePostId:
                    ReportReasonKits.Apply(post,
                        "Appeal — user says wholesome library post was wrongly limited.",
                        ReporterTone.Genuine, ReportCredibility.Credible);
                    PostManager.SetApproveThread(post,
                        new PostCommentLine { displayHandle = "toddlermom", text = "story time hours help so much omg" },
                        new PostCommentLine { displayHandle = "library_fan", text = "finally good news on the timeline" });
                    PostManager.SetDeclineThread(post,
                        new PostCommentLine { displayHandle = "grumpy_neighbor", text = "who cares" });
                    break;

                case "p_d3_12":
                    ReportReasonKits.Apply(post,
                        "Author documents suspicious engagement spikes on benign posts.",
                        ReporterTone.Genuine, ReportCredibility.Credible);
                    PostManager.SetApproveThread(post,
                        new PostCommentLine { displayHandle = "data_nerd", text = "your chart matches what i've been logging" },
                        new PostCommentLine { displayHandle = "platform_apologist", text = "maybe it's just A/B testing chill", replyToIndex = 0 });
                    PostManager.SetDeclineThread(post,
                        new PostCommentLine { displayHandle = "corp_shill_maybe", text = "stop complaining the app is free", botFlag = true });
                    break;
            }
        }
    }
}
