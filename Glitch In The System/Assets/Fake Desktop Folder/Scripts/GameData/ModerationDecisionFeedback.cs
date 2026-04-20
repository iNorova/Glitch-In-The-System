namespace GlitchInTheSystem.GameData
{
    /// <summary>Short lines for the work dashboard after each moderation decision.</summary>
    public static class ModerationDecisionFeedback
    {
        public static string GetDashboardLine(bool finalApproved, bool overridden, PostData post)
        {
            if (overridden)
                return $"{(finalApproved ? "Approved" : "Removed")} (overridden)";

            if (!finalApproved)
                return "Removed";

            if (post != null && IsRisky(post))
                return "Approved — warning: risky content may spread";

            return "Approved";
        }

        private static bool IsRisky(PostData post)
        {
            if (post.category == PostCategory.Misinformation) return true;
            if (post.category == PostCategory.Violation) return true;
            if (post.category == PostCategory.GrayArea && post.severity >= 2) return true;
            return false;
        }
    }
}
