using System;
using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// User profile data shared between Social Media app and Content Moderator.
    /// </summary>
    [Serializable]
    public sealed class UserProfileData
    {
        public string id;
        public string username;
        public string displayName;
        public int accountAgeYears;
        public int followers;
        public int following;
        public int strikes;
        public string reputation; // Trusted, Neutral, Low Trust, Watchlisted
        public string risk;       // Low, Medium, High
        public bool isShadowBanned;
        public string avatarSpriteId; // optional: for future sprite ref

        public string UsernameDisplay => $"@{username}";
    }
}
