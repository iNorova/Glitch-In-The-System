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
    }
}
