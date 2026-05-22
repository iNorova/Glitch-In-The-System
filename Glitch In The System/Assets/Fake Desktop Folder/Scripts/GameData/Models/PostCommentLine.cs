using System;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// One authored comment in an approve/decline thread (pre-written, no runtime generation).
    /// </summary>
    [Serializable]
    public sealed class PostCommentLine
    {
        public string text;
        /// <summary>Display handle e.g. jenna_reads (without @).</summary>
        public string displayHandle;
        /// <summary>-1 = top-level reply to post; otherwise index into the same thread list.</summary>
        public int replyToIndex = -1;
        public bool botFlag;
    }
}
