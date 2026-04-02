using System;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// System log entry for evidence / whistleblower mechanic. Tracks algorithm actions and player actions.
    /// </summary>
    [Serializable]
    public sealed class LogEntry
    {
        public string id;
        public LogEntryType type;
        public string description;
        public string postId;
        public string userId;
        public float timestamp;
        public string rawData; // JSON or key-value for debugging
    }

    public enum LogEntryType
    {
        PlayerDecision,
        AlgorithmOverride,
        AlgorithmRewrite,
        AlgorithmShadowBan,
        AlgorithmEngagementNudge,
        AlgorithmCommentDeletion,
        PlayerBehavior
    }
}
