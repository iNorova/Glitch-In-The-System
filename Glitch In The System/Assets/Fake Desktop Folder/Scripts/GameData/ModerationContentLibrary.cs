using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Optional ScriptableObject wrapper so designers can duplicate/tune pools without code changes.
    /// When empty, <see cref="ModerationContentPools"/> static entries are used.
    /// </summary>
    [CreateAssetMenu(fileName = "ModerationContentLibrary", menuName = "Glitch In The System/Moderation Content Library")]
    public sealed class ModerationContentLibrary : ScriptableObject
    {
        [Tooltip("Extra hand-authored posts merged into day 4+ queues.")]
        public ModerationContentPools.ModerationEntry[] extraQueueEntries;

        [Tooltip("If true, procedural day 4+ posts pull from ModerationContentPools instead of inline templates.")]
        public bool preferPoolEntriesForProceduralDays = true;

        [Range(0f, 1f)]
        [Tooltip("Chance a procedural slot uses a borderline pool entry instead of legacy templates.")]
        public float poolEntryBlendChance = 0.65f;
    }
}
