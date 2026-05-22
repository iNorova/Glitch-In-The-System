using System;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Lightweight social comment record for feed cards.
    /// </summary>
    [Serializable]
    public sealed class CommentData
    {
        public string id;
        public string postId;
        public string authorUserId;
        /// <summary>Shown in UI when set; falls back to author username.</summary>
        public string displayHandle;
        public string text;
        public string timestampLabel; // e.g. "12m"
        public int likes;
        public bool isHidden;
        /// <summary>Index into sibling preview list for threaded display (-1 = top-level).</summary>
        public int replyToIndex = -1;
        public bool botFlag;
    }
}
