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
        public string text;
        public string timestampLabel; // e.g. "12m"
        public int likes;
        public bool isHidden;
    }
}
