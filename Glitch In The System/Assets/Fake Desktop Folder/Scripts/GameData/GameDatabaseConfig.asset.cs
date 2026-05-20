using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// ScriptableObject config for GameDatabase (posts per day, seed content, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabaseConfig", menuName = "Glitch In The System/Game Database Config")]
    public sealed class GameDatabaseConfig : ScriptableObject
    {
        [Header("Session")]
        public int currentDay = 1;
        public int postsPerDay = 10;

        [Header("Content mix (100+ total)")]
        public int harmlessCount = 25;
        public int violationCount = 25;
        public int misinformationCount = 20;
        public int grayAreaCount = 15;
        public int narrativeCount = 15;
        public int algorithmManipulationCount = 10;

        [Header("Algorithm")]
        public int algorithmPhase = 0; // 0=Helpful, 1=Authoritative, 2=Manipulative

        [Header("Day 1 testing (other days not wired yet)")]
        [Tooltip("When enabled, Day 1 uses the test chances below instead of zero interference.")]
        public bool day1EnableAlgorithmTest = false;
        [Range(0, 2)] public int day1TestPhase = 1;
        [Range(0f, 1f)] public float day1TestOverrideChance = 0.25f;
        [Range(0f, 1f)] public float day1TestRewriteChance = 0.4f;
        [Tooltip("Scales override/rewrite rolls on Day 1 while testing (1 = full strength).")]
        [Range(0.05f, 1.5f)] public float day1TestHostilityMultiplier = 1f;

        [Header("Moderation content")]
        [Tooltip("Optional pool of realistic posts (edit in Project without Play mode).")]
        public ModerationContentLibrary moderationContentLibrary;
    }
}
