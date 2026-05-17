using UnityEngine;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// The one scene post used as the visual design for all feed cards at play time (images, panels, layout).
    /// Edit this object in the editor; clones are created per post when the game runs.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SocialMediaFeedPostTemplate : MonoBehaviour
    {
        public const string TemplateObjectName = "EditorFeedPost_Template";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            SocialMediaFeedLayoutConstraints.ApplyToDesignTemplate(transform as RectTransform);
        }
#endif
    }
}
