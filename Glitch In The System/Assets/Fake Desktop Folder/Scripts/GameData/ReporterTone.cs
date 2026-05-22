namespace GlitchInTheSystem.GameData
{
    /// <summary>How the reporting user sounds — used for report copy selection and future moderator training.</summary>
    public enum ReporterTone
    {
        Genuine = 0,
        Vague = 1,
        BadFaith = 2,
        FalseReport = 3,
        OpinionBased = 4
    }

    /// <summary>Whether the report itself looks trustworthy (teaches false-report spotting later).</summary>
    public enum ReportCredibility
    {
        Credible = 0,
        Unclear = 1,
        LikelyFalseReport = 2
    }
}
