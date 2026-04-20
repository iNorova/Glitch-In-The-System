using System;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// A moderation decision (approve/decline) recorded by the player or overridden by the algorithm.
    /// </summary>
    [Serializable]
    public sealed class ModerationDecision
    {
        public string postId;
        public string authorUserId;
        public bool approved;
        /// <summary>What the player clicked before any algorithm override.</summary>
        public bool playerChoseApprove;
        public bool wasOverriddenByAlgorithm;
        public string playerReason;   // optional: why player chose
        public string algorithmReason; // if overridden
        public float timestamp;
    }
}
