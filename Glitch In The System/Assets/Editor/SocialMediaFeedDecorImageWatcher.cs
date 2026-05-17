#if UNITY_EDITOR
using GlitchInTheSystem.Social;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Auto-constrains images added under the feed design template.</summary>
[InitializeOnLoad]
internal static class SocialMediaFeedDecorImageWatcher
{
    static SocialMediaFeedDecorImageWatcher()
    {
        ObjectFactory.componentWasAdded += OnComponentWasAdded;
    }

    private static void OnComponentWasAdded(Component component)
    {
        if (Application.isPlaying) return;
        if (component is not Image image) return;
        if (!IsUnderDesignTemplate(image.transform)) return;
        if (!SocialMediaFeedLayoutConstraints.IsDecorImage(image)) return;
        if (image.GetComponent<SocialMediaFeedDecorImage>() != null) return;

        var decor = image.gameObject.AddComponent<SocialMediaFeedDecorImage>();
        decor.ApplyConstraints();

        var template = image.GetComponentInParent<SocialMediaFeedPostTemplate>();
        if (template != null)
            SocialMediaFeedLayoutConstraints.ApplyFixedSizeToFeedPanels(template.transform as RectTransform);
    }

    private static bool IsUnderDesignTemplate(Transform t)
    {
        return t != null && t.GetComponentInParent<SocialMediaFeedPostTemplate>(true) != null;
    }
}
#endif
