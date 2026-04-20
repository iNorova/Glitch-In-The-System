using System;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Lightweight flags for story consequences (extended without touching core moderation flow).
    /// </summary>
    [Serializable]
    public sealed class NarrativeState
    {
        /// <summary>True if the viral water memo post ended up published (player + algorithm final outcome).</summary>
        public bool approvedViralMisinformation;

        /// <summary>True after the viral post has been moderated once this session.</summary>
        public bool viralArcResolved;

        public void Reset()
        {
            approvedViralMisinformation = false;
            viralArcResolved = false;
        }

        /// <summary>Returns true the first time the viral arc completes (so follow-ups spawn only once).</summary>
        public bool TryMarkViralResolved(bool finalApproved)
        {
            if (viralArcResolved) return false;
            viralArcResolved = true;
            approvedViralMisinformation = finalApproved;
            return true;
        }
    }
}
