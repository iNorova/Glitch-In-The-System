using UnityEngine;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>
    /// Edit-mode tunable trust bands and algorithm tuning. Assign on <see cref="GameData.GameBootstrap"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "AlgorithmTrustSettings", menuName = "Glitch In The System/Algorithm Trust Settings")]
    public sealed class AlgorithmTrustSettings : ScriptableObject
    {
        [Header("Starting session values")]
        [Range(0f, 100f)] public float startingTrust = 55f;
        [Range(0f, 100f)] public float startingStressLevel;

        [Header("Trust → behaviour bands (fallback when profile ranges do not match)")]
        [Tooltip("Trust at or above this value → Passive (unless a profile range matches first).")]
        [Range(0f, 100f)] public float passiveTrustMin = 67f;
        [Tooltip("Trust at or above this value (and below Passive) → Assertive.")]
        [Range(0f, 100f)] public float assertiveTrustMin = 34f;

        [Header("State profiles")]
        public AlgorithmStateProfile passiveProfile;
        public AlgorithmStateProfile assertiveProfile;
        public AlgorithmStateProfile aggressiveProfile;

        [Header("Event thresholds")]
        public float hesitationSeconds = 12f;
        [Range(0f, 100f)] public float stressHighThreshold = 72f;

        [Header("Post manipulation")]
        public int maxManipulationDay = 10;

        /// <summary>Resolves mood from trust using profile ranges, then fallback thresholds.</summary>
        public AlgorithmBehaviourState ResolveBehaviourState(float trust)
        {
            trust = Mathf.Clamp(trust, 0f, 100f);

            if (passiveProfile != null && passiveProfile.ContainsTrust(trust))
                return AlgorithmBehaviourState.Passive;
            if (aggressiveProfile != null && aggressiveProfile.ContainsTrust(trust))
                return AlgorithmBehaviourState.Aggressive;
            if (assertiveProfile != null && assertiveProfile.ContainsTrust(trust))
                return AlgorithmBehaviourState.Assertive;

            if (trust >= passiveTrustMin) return AlgorithmBehaviourState.Passive;
            if (trust >= assertiveTrustMin) return AlgorithmBehaviourState.Assertive;
            return AlgorithmBehaviourState.Aggressive;
        }

        public string DescribeBand(float trust)
        {
            var state = ResolveBehaviourState(trust);
            return $"{state} (trust {trust:0})";
        }
    }
}
