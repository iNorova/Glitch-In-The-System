namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Cohesive engagement band for a post. Likes, shares, and comment count are always derived together
    /// from one tier (see <see cref="OrganicEngagementUtility"/>).
    /// </summary>
    public enum EngagementTier
    {
        /// <summary>Almost no traction — personal vents, dead posts.</summary>
        Ignored = 0,

        /// <summary>Typical friend-group post (dozens of likes, handful of replies).</summary>
        Normal = 1,

        /// <summary>Active thread — arguments, local drama, heated but not platform-wide viral.</summary>
        Heated = 2,

        /// <summary>Large spread — misinformation, political hooks, narrative panic (messy counts).</summary>
        Viral = 3,

        /// <summary>Clean round numbers — only for Algorithm / bot-boosted manipulation.</summary>
        ManipulatedRound = 4
    }
}
