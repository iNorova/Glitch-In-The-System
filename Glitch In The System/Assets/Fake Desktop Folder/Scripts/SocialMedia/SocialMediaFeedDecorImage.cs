using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Keeps a decorative sprite from expanding layout groups / parent panels.
    /// Added automatically on images under the feed design template.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class SocialMediaFeedDecorImage : MonoBehaviour
    {
        [SerializeField] private float height = SocialMediaFeedLayoutConstraints.DefaultDecorImageHeight;
        [SerializeField] private bool stretchWidth = true;

        public float Height => height;

        private void Reset() => ApplyConstraints();

        private void OnValidate() => ApplyConstraints();

        public void ApplyConstraints()
        {
            var img = GetComponent<Image>();
            if (img == null) return;

            img.preserveAspect = true;

            var le = GetComponent<LayoutElement>();
            if (le == null)
                le = gameObject.AddComponent<LayoutElement>();

            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;

            if (stretchWidth)
            {
                le.flexibleWidth = 1f;
                le.preferredWidth = -1f;
            }

            var rt = transform as RectTransform;
            if (rt == null) return;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, height);
        }
    }
}
