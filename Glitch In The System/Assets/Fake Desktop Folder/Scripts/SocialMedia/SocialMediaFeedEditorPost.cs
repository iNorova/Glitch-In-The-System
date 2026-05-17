using UnityEngine;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Marks a feed card placed in the scene for layout editing (visible when the game is not running).
    /// Hidden at play time; live posts are built procedurally by <see cref="SocialMediaFeedController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SocialMediaFeedEditorPost : MonoBehaviour { }
}
