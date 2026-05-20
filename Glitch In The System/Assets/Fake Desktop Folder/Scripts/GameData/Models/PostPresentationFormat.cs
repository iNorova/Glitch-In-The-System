namespace GlitchInTheSystem.GameData
{
    /// <summary>How a moderation-queue post is laid out in the Work Dashboard.</summary>
    public enum PostPresentationFormat
    {
        TextOnly = 0,
        TextWithImageDescription = 1,
        TextWithAttachedComments = 2
    }

    /// <summary>Flavor tag for social feed presentation (organic mix of post types).</summary>
    public enum FeedPostKind
    {
        PersonalUpdate = 0,
        NewsRepost = 1,
        Meme = 2,
        EmotionalVent = 3,
        SponsoredAd = 4,
        ViralClip = 5
    }
}
