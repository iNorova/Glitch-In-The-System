using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Social
{
    /// <summary>Prevents panel images from blowing up VerticalLayoutGroup / ContentSizeFitter sizes.</summary>
    public static class SocialMediaFeedLayoutConstraints
    {        public const float DefaultDecorImageHeight = 80f;
        public const float DefaultCommentsPanelHeight = 110f;

        public static void ApplyToDesignTemplate(RectTransform templateRoot)
        {
            if (templateRoot == null) return;

            bool freeform = templateRoot.GetComponentInParent<SocialMediaFeedFreeformLayout>() != null;
            if (!freeform)
                ApplyFixedSizeToFeedPanels(templateRoot);

            ConstrainDecorImagesUnder(templateRoot);
        }

        public static void ApplyFixedSizeToFeedPanels(RectTransform cardRoot)
        {
            if (cardRoot == null) return;

            ApplyFixedPanelLayout(cardRoot.Find("CommentsPanel") as RectTransform);
            ApplyFixedPanelLayout(cardRoot.Find("CommentsSection/CommentsPanel") as RectTransform);
        }

        /// <summary>Restores dynamic height for cloned feed cards at play time.</summary>
        public static void PrepareRuntimeFeedCard(RectTransform cardRoot)
        {
            if (cardRoot == null) return;

            var panel = cardRoot.Find("CommentsPanel") as RectTransform
                ?? cardRoot.Find("CommentsSection/CommentsPanel") as RectTransform;
            if (panel != null)
                PrepareRuntimeCommentsPanel(panel);

            var cardFitter = cardRoot.GetComponent<ContentSizeFitter>();
            if (cardFitter == null)
                cardFitter = cardRoot.gameObject.AddComponent<ContentSizeFitter>();
            cardFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static void PrepareRuntimeCommentsPanel(RectTransform panel)
        {
            if (panel == null) return;

            var mask = panel.GetComponent<RectMask2D>();
            if (mask != null)
                Object.Destroy(mask);

            var le = panel.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = 48f;
                le.preferredHeight = -1f;
                le.flexibleHeight = 0f;
            }

            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandHeight = false;
            }

            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PrepareRuntimeCommentLines(panel);
        }

        private static void PrepareRuntimeCommentLines(RectTransform panel)
        {
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.name.StartsWith("Comment_", System.StringComparison.Ordinal))
                    continue;

                var lineLE = tmp.GetComponent<LayoutElement>();
                if (lineLE != null)
                    lineLE.preferredHeight = -1f;

                var lineFitter = tmp.GetComponent<ContentSizeFitter>();
                if (lineFitter == null)
                    lineFitter = tmp.gameObject.AddComponent<ContentSizeFitter>();
                lineFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                lineFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        public static void ApplyFixedPanelLayout(RectTransform panel)
        {
            if (panel == null) return;
#if UNITY_EDITOR
            if (Application.isPlaying) return;
#endif

            RemoveContentSizeFitter(panel.gameObject);

            var le = panel.GetComponent<LayoutElement>();
            if (le == null)
                le = panel.gameObject.AddComponent<LayoutElement>();

            if (le.preferredHeight < 40f)
            {
                le.minHeight = DefaultCommentsPanelHeight;
                le.preferredHeight = DefaultCommentsPanelHeight;
            }

            le.flexibleHeight = 0f;

            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childControlHeight = false;
                vlg.childForceExpandHeight = false;
            }

            if (panel.GetComponent<RectMask2D>() == null)
                panel.gameObject.AddComponent<RectMask2D>();
        }

        public static void ConstrainDecorImagesUnder(Transform root)
        {
            if (root == null) return;
#if UNITY_EDITOR
            if (Application.isPlaying) return;
#endif

            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (!IsDecorImage(img)) continue;

                var decor = img.GetComponent<SocialMediaFeedDecorImage>();
                if (decor == null)
                    decor = img.gameObject.AddComponent<SocialMediaFeedDecorImage>();

                decor.ApplyConstraints();
            }
        }

        public static bool IsDecorImage(Image img)
        {
            if (img == null) return false;
            if (img.GetComponent<Button>() != null) return false;
            if (img.GetComponent<VerticalLayoutGroup>() != null) return false;
            if (img.GetComponent<HorizontalLayoutGroup>() != null) return false;
            if (img.GetComponent<SocialMediaFeedPostTemplate>() != null) return false;
            if (img.GetComponent<SocialMediaFeedEditorPost>() != null && img.transform.childCount > 0) return false;

            var t = img.transform;
            if (t.name == "CommentsPanel" || t.name == "EditorFeedPost_Template") return false;

            return true;
        }

        private static void RemoveContentSizeFitter(GameObject go)
        {
            if (go == null) return;
            var fitter = go.GetComponent<ContentSizeFitter>();
            if (fitter == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(fitter);
            else
#endif
                Object.Destroy(fitter);
        }
    }
}
