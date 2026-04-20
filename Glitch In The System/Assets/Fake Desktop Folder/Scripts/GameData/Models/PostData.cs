using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Post data shared between Social Media feed and Content Moderator queue.
    /// </summary>
    [Serializable]
    public sealed class PostData
    {
        public string id;
        public string authorUserId;
        public string text;
        public string timestampLabel; // e.g. "2h", "1d"
        public int likes;
        public int shares;
        public int comments;
        public string mediaSpriteId; // optional
        public PostCategory category; // Harmless, Violation, Misinformation, GrayArea, Narrative, AlgorithmManipulation
        public int severity;          // 0–3 for moderation priority
        public bool wasRewrittenByAlgorithm;
        public string originalText;   // if algorithm rewrote it
        public bool isPublished;      // appears in social app only when approved by moderator
        public bool isShadowBanned;
        public bool isRemoved;
        public List<CommentData> commentPreview = new(); // Filled at moderation time from branch lists (see PostManager).

        /// <summary>Short reactions if the player allows the post to go live.</summary>
        public List<string> commentsApprove = new();

        /// <summary>Reactions if the player blocks the post (also used for internal tone when it never publishes).</summary>
        public List<string> commentsDecline = new();

        /// <summary>Like count shown in the feed after an approve decision (can be huge for viral misinformation).</summary>
        public int likesApprove;

        /// <summary>Like count after a decline (usually low — post dies or is buried).</summary>
        public int likesDecline;

        public string EngagementDisplay => $"Likes {likes:N0}  •  Shares {shares:N0}  •  Comments {comments:N0}";
    }

    public enum PostCategory
    {
        Harmless,
        Violation,
        Misinformation,
        GrayArea,
        Narrative,
        AlgorithmManipulation
    }
}
