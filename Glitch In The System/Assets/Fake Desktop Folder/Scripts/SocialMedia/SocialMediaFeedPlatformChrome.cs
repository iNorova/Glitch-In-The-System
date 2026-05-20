using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Adds lightweight "real platform" chrome: trending topics, suggested friends, notification badge.
    /// Built once at runtime under the social window (event-driven, no Update).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SocialMediaFeedPlatformChrome : MonoBehaviour
    {
        [SerializeField] private bool showTrendingRail = true;
        [SerializeField] private bool showSuggestedFriends = true;
        [SerializeField] private bool showNotificationBadge = true;

        private bool _built;

        public void EnsureChrome(RectTransform floatingPanel)
        {
            if (_built || floatingPanel == null) return;
            _built = true;

            if (showNotificationBadge)
                BuildNotificationBadge(floatingPanel);

            if (showTrendingRail)
                BuildTrendingRail(floatingPanel);

            if (showSuggestedFriends)
                BuildSuggestedFriends(floatingPanel);
        }

        private static void BuildNotificationBadge(RectTransform root)
        {
            var topBar = root.Find("TopBar") as RectTransform;
            if (topBar == null) return;
            if (topBar.Find("NotificationBadge") != null) return;

            var badge = CreateTextBlock("NotificationBadge", topBar, "3", 11, TextAlignmentOptions.Center,
                new Color(1f, 0.35f, 0.35f, 1f));
            var rt = badge.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-52f, -6f);
            rt.sizeDelta = new Vector2(22f, 18f);
            badge.transform.parent.GetComponent<Image>()?.gameObject.AddComponent<LayoutElement>();
        }

        private static void BuildTrendingRail(RectTransform root)
        {
            if (root.Find("TrendingRail") != null) return;

            var rail = new GameObject("TrendingRail", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            rail.transform.SetParent(root, false);
            var rt = rail.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-168f, 48f);
            rt.offsetMax = new Vector2(-8f, -88f);
            rail.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f, 0.92f);

            var layout = rail.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            CreateTextBlock("TrendingTitle", rt, "Trending", 14, TextAlignmentOptions.MidlineLeft,
                new Color(0.75f, 0.8f, 0.92f, 1f));
            CreateTextBlock("Trend1", rt, "• water memo fact-check", 12, TextAlignmentOptions.TopLeft,
                new Color(0.65f, 0.7f, 0.78f, 1f));
            CreateTextBlock("Trend2", rt, "• election audio clip", 12, TextAlignmentOptions.TopLeft,
                new Color(0.65f, 0.7f, 0.78f, 1f));
            CreateTextBlock("Trend3", rt, "• hospital wait times", 12, TextAlignmentOptions.TopLeft,
                new Color(0.65f, 0.7f, 0.78f, 1f));
        }

        private static void BuildSuggestedFriends(RectTransform root)
        {
            if (root.Find("SuggestedFriends") != null) return;

            var box = new GameObject("SuggestedFriends", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            box.transform.SetParent(root, false);
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(12f, 12f);
            rt.sizeDelta = new Vector2(150f, 72f);
            box.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 0.9f);

            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 2;
            layout.childControlWidth = true;

            CreateTextBlock("SuggestTitle", rt, "People you may know", 11, TextAlignmentOptions.MidlineLeft,
                new Color(0.7f, 0.76f, 0.88f, 1f));
            CreateTextBlock("Suggest1", rt, "@pixelpanda", 11, TextAlignmentOptions.MidlineLeft,
                new Color(0.55f, 0.6f, 0.7f, 1f));
            CreateTextBlock("Suggest2", rt, "@civic_watch", 11, TextAlignmentOptions.MidlineLeft,
                new Color(0.55f, 0.6f, 0.7f, 1f));
        }

        private static TextMeshProUGUI CreateTextBlock(string name, RectTransform parent, string text, int size,
            TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
