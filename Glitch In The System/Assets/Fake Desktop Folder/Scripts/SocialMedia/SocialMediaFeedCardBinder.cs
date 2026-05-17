using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Social
{
    /// <summary>Updates text on scene-authored editor feed cards (does not change RectTransforms).</summary>
    public static class SocialMediaFeedCardBinder
    {
        public static void Apply(Transform cardRoot, PostData post, UserProfileData user, bool expandComments)
        {
            if (cardRoot == null || post == null) return;

            SetText(cardRoot, "AuthorText", user != null ? $"{user.displayName}  @{user.username}" : $"@{post.authorUserId}");
            SetText(cardRoot, "CategoryTag", SocialMediaFeedPresentation.CategoryLabel(post.category));
            var categoryTag = FindTmp(cardRoot, "CategoryTag");
            if (categoryTag != null)
                categoryTag.color = SocialMediaFeedPresentation.CategoryColor(post.category);

            SetText(cardRoot, "BodyText", SocialMediaFeedPresentation.SanitizeForTMP(post.text));
            SetText(cardRoot, "EngagementText", post.EngagementDisplay);
            SetText(cardRoot, "TimeText", post.timestampLabel);

            var engagementLabel = FindTmp(cardRoot, "EngagementLabel");
            if (engagementLabel != null)
            {
                bool show = !string.IsNullOrEmpty(post.engagementLabel);
                engagementLabel.gameObject.SetActive(show);
                if (show)
                {
                    engagementLabel.text = post.engagementLabel;
                    bool trending = post.engagementLabel == "TRENDING";
                    engagementLabel.color = trending
                        ? new Color(1f, 0.62f, 0.28f, 1f)
                        : new Color(0.65f, 0.72f, 0.82f, 1f);
                }
            }

            var stateText = FindTmp(cardRoot, "StateText");
            if (stateText != null)
            {
                string state = SocialMediaFeedPresentation.BuildStateLabel(post, user);
                stateText.gameObject.SetActive(!string.IsNullOrEmpty(state));
                if (!string.IsNullOrEmpty(state))
                    stateText.text = state;
            }

            int commentCount = post.commentPreview?.Count ?? 0;

            var commentsSection = cardRoot.Find("CommentsSection");
            if (commentsSection != null)
                commentsSection.gameObject.SetActive(commentCount > 0);

            foreach (var toggle in cardRoot.GetComponentsInChildren<Button>(true))
            {
                if (toggle == null || toggle.name != "ActionButton" && toggle.name != "CommentsToggle") continue;
                var label = toggle.GetComponentInChildren<TMP_Text>(true);
                if (label != null && commentCount > 0)
                    label.text = $"Comments ({commentCount})";
            }

            var commentsPanel = cardRoot.Find("CommentsPanel");
            if (commentsPanel == null)
                commentsPanel = cardRoot.Find("CommentsSection/CommentsPanel");
            if (commentsPanel != null)
            {
                bool isEditorPreview = false;
#if UNITY_EDITOR
                isEditorPreview = !Application.isPlaying && cardRoot.GetComponent<SocialMediaFeedEditorPost>() != null;
#endif

                bool showPanel = expandComments && commentCount > 0;
                if (isEditorPreview)
                    showPanel = true;

                commentsPanel.gameObject.SetActive(showPanel);
                int show = Mathf.Min(3, commentCount > 0 ? commentCount : 3);
                for (int i = 0; i < 3; i++)
                {
                    var line = FindTmp(commentsPanel, $"Comment_{i}");
                    if (line == null) continue;
                    bool visible = isEditorPreview || i < show;
                    line.gameObject.SetActive(visible);
                    if (!visible) continue;
                    if (commentCount > 0 && i < post.commentPreview.Count)
                    {
                        var c = post.commentPreview[i];
                        line.text = $"@{c.authorUserId}: {SocialMediaFeedPresentation.SanitizeForTMP(c.text)}";
                    }
                    else if (isEditorPreview)
                        line.text = line.text.Length > 0 ? line.text : "@user: Sample comment for layout.";
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && cardRoot.GetComponent<SocialMediaFeedEditorPost>() != null)
                SocialMediaFeedEditorUtility.ForcePostVisible(cardRoot as RectTransform);
#endif
        }

        private static void SetText(Transform root, string childName, string value)
        {
            var tmp = FindTmp(root, childName);
            if (tmp != null) tmp.text = value ?? string.Empty;
        }

        private static TMP_Text FindTmp(Transform root, string name)
        {
            var t = root.Find(name);
            if (t != null) return t.GetComponent<TMP_Text>();
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.name == name) return tmp;
            return null;
        }
    }
}
