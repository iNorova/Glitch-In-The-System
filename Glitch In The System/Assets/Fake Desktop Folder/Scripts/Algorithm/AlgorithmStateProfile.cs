using UnityEngine;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>
    /// Per-state tuning for interference and messaging. Assign one asset per Passive / Assertive / Aggressive mood.
    /// </summary>
    [CreateAssetMenu(fileName = "AlgorithmStateProfile", menuName = "Glitch In The System/Algorithm State Profile")]
    public sealed class AlgorithmStateProfile : ScriptableObject
    {
        [Header("State mapping")]
        public AlgorithmBehaviourState behaviourState = AlgorithmBehaviourState.Assertive;

        [Tooltip("Inclusive trust range that activates this profile.")]
        [Range(0f, 100f)] public float trustMinInclusive = 34f;
        [Range(0f, 100f)] public float trustMaxInclusive = 66f;

        [Header("Interference (0–1)")]
        [Range(0f, 1f)] public float overrideChance = 0.15f;
        [Range(0f, 1f)] public float rewriteChance = 0.1f;
        [Range(0f, 1f)] public float shadowBanChance = 0.2f;

        [Header("Engagement nudge")]
        public int engagementNudgeMin = 50;
        public int engagementNudgeMax = 500;

        [Header("Messaging")]
        [Range(0f, 1f)] public float messageDeliveryChance = 0.4f;

        public bool ContainsTrust(float trust) =>
            trust >= trustMinInclusive && trust <= trustMaxInclusive;
    }
}
