using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.Social;
using GlitchInTheSystem.UI;

/// <summary>
/// Renders a social-media style feed from GameDatabase posts.
/// Uses the same post source as WorkDashboard so both apps stay in sync.
/// </summary>
public sealed class SocialMediaFeedController : MonoBehaviour, IScrollHandler
{
    [Header("Data")]
    [SerializeField] private bool useGameDatabase = true;
    [SerializeField] private bool autoInitializeSessionIfEmpty = true;
    [SerializeField] private bool includeRemovedPosts = false;

    [Header("UI Bindings")]
    [SerializeField] private TMP_Text feedStatsText;
    [SerializeField] private RectTransform feedContent;
    [SerializeField] private ScrollRect feedScrollRect;
    [SerializeField] private Button refreshButton;

    [Header("Behavior")]
    [SerializeField] private float autoRefreshSeconds = 0f;
    [SerializeField] private bool forceRuntimeFeedHost = true;
    [SerializeField] private float wheelScrollSpeed = 0.08f;

    private float _nextRefreshAt;
    private string _lastSignature;

    private void Awake()
    {
        AutoBindByName();
        WireButtonsIfPresent();
        EnsureWindowFocusHandler();
    }

    private void OnEnable()
    {
        AutoBindByName();
        WireButtonsIfPresent();
        EnsureWindowFocusHandler();
        RefreshFeed(force: true);
        StartCoroutine(RefreshNextFrame());
        _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.2f, autoRefreshSeconds);
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        RefreshFeed(force: true);
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;
        if (autoRefreshSeconds <= 0f) return;
        if (Time.unscaledTime < _nextRefreshAt) return;

        _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.2f, autoRefreshSeconds);
        RefreshFeed(force: false);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (feedScrollRect == null || !feedScrollRect.IsActive()) return;
        float wheel = eventData != null ? eventData.scrollDelta.y : 0f;
        if (Mathf.Abs(wheel) < 0.01f) return;
        float next = feedScrollRect.verticalNormalizedPosition + (wheel * wheelScrollSpeed);
        feedScrollRect.verticalNormalizedPosition = Mathf.Clamp01(next);
    }

    public void RefreshFeedNow()
    {
        AutoBindByName();
        WireButtonsIfPresent();
        RefreshFeed(force: true);
    }

    private void RefreshFeed(bool force)
    {
        if (forceRuntimeFeedHost)
            EnsureGuaranteedRuntimeHost();

        EnsureRuntimeFeedTree();
        if (feedScrollRect != null && feedContent != null && !feedContent.IsChildOf(feedScrollRect.transform))
            feedContent = null;

        if (feedContent == null)
        {
            AutoBindByName();
            if (feedContent == null)
            {
                if (feedStatsText != null) feedStatsText.text = "Feed bind missing";
                return;
            }
        }

        if (!useGameDatabase || GameDatabase.Instance == null)
        {
            var filler = GenerateFillerPosts();
            RebuildEntries(filler, _ => null);
            if (feedStatsText != null) feedStatsText.text = $"Filler feed  |  Posts: {filler.Count}";
            return;
        }

        if (autoInitializeSessionIfEmpty && GameDatabase.Instance.Posts.Count == 0)
            GameDatabase.Instance.InitializeSession();

        var approvedPosts = FeedManager.GetPublishedPostsForFeed(GameDatabase.Instance, includeRemovedPosts);
        var posts = BuildDisplayFeed(approvedPosts);

        EnsureFeedLayout();

        string signature = FeedManager.BuildSignature(posts);
        if (!force && signature == _lastSignature) return;
        _lastSignature = signature;

        RebuildEntries(posts, id => GameDatabase.Instance.GetUser(id));

        if (feedStatsText != null)
        {
            int day = GameDatabase.Instance.Config != null ? GameDatabase.Instance.Config.currentDay : 1;
            int rendered = feedContent != null ? feedContent.childCount : 0;
            float w = feedScrollRect != null ? feedScrollRect.GetComponent<RectTransform>().rect.width : 0f;
            float h = feedScrollRect != null ? feedScrollRect.GetComponent<RectTransform>().rect.height : 0f;
            string quirk = day == 3 ? "  |  Sync: unstable" : "";
            feedStatsText.text = $"Day {day}{quirk}  |  Live: {approvedPosts.Count}  |  Feed: {posts.Count}  |  Rendered: {rendered}";
        }

        if (feedScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(feedContent);
            feedScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void RebuildEntries(IReadOnlyList<PostData> posts, Func<string, UserProfileData> getUser)
    {
        // Clear immediately from layout hierarchy to avoid delayed-destroy stacking.
        for (int i = feedContent.childCount - 1; i >= 0; i--)
        {
            var child = feedContent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        if (posts.Count == 0)
        {
            CreateEmptyStateCard();
            return;
        }

        foreach (var post in posts)
        {
            var user = getUser(post.authorUserId);
            CreateFeedCard(post, user);
        }
    }

    private void CreateEmptyStateCard()
    {
        var card = new GameObject("EmptyState", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(feedContent, false);
        card.GetComponent<Image>().color = new Color(0.14f, 0.15f, 0.18f, 0.96f);
        var le = card.GetComponent<LayoutElement>();
        le.preferredHeight = 120f;
        le.minHeight = 100f;

        CreateTMP(
            "EmptyText",
            card.transform as RectTransform,
            "No visible posts yet.\nOpen Work Dashboard and start moderating to affect this feed.",
            15,
            TextAlignmentOptions.Center,
            new Color(0.85f, 0.89f, 0.97f, 1f)
        ).textWrappingMode = TextWrappingModes.Normal;
    }

    private void EnsureFeedLayout()
    {
        if (feedContent == null) return;

        // ScrollRect content should be top-anchored, not fully stretched.
        feedContent.anchorMin = new Vector2(0f, 1f);
        feedContent.anchorMax = new Vector2(1f, 1f);
        feedContent.pivot = new Vector2(0.5f, 1f);
        feedContent.anchoredPosition = Vector2.zero;
        feedContent.sizeDelta = new Vector2(0f, feedContent.sizeDelta.y);

        var layout = feedContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = feedContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = feedContent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = feedContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(feedContent);
    }

    private void EnsureRuntimeFeedTree()
    {
        // If references are valid and active, keep them.
        if (feedContent != null && feedContent.gameObject.activeInHierarchy && feedScrollRect != null)
            return;

        AutoBindByName();
        if (feedContent != null && feedScrollRect != null) return;

        // Try to repair/create feed hierarchy under FloatingPanel/Body
        var body = FindRect("FloatingPanel/Body") ?? FindRect("Body");
        if (body == null) return;
        if (!body.gameObject.activeSelf) body.gameObject.SetActive(true);

        var feedScroll = FindRect("FloatingPanel/Body/FeedScroll") ?? FindRect("Body/FeedScroll");
        ScrollRect sr = feedScroll != null ? feedScroll.GetComponent<ScrollRect>() : null;
        RectTransform viewport = null;
        RectTransform content = null;

        if (feedScroll == null)
        {
            var feedGo = new GameObject("FeedScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            feedGo.transform.SetParent(body, false);
            feedScroll = feedGo.transform as RectTransform;
            var img = feedGo.GetComponent<Image>();
            img.color = new Color(0.09f, 0.10f, 0.12f, 1f);

            var le = feedGo.GetComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.flexibleWidth = 1f;
            le.minHeight = 200f;

            sr = feedGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
        }
        if (!feedScroll.gameObject.activeSelf) feedScroll.gameObject.SetActive(true);

        // Always enforce full-size fill inside Body.
        if (feedScroll != null)
        {
            feedScroll.anchorMin = Vector2.zero;
            feedScroll.anchorMax = Vector2.one;
            feedScroll.offsetMin = Vector2.zero;
            feedScroll.offsetMax = Vector2.zero;
            feedScroll.pivot = new Vector2(0.5f, 0.5f);

            var scrollLE = feedScroll.GetComponent<LayoutElement>() ?? feedScroll.gameObject.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            scrollLE.flexibleWidth = 1f;
            if (scrollLE.minHeight < 200f) scrollLE.minHeight = 200f;
        }

        var existingViewport = feedScroll.Find("Viewport") as RectTransform;
        if (existingViewport == null)
        {
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            vpGo.transform.SetParent(feedScroll, false);
            viewport = vpGo.transform as RectTransform;
            var vpImg = vpGo.GetComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            vpImg.raycastTarget = true;
        }
        else
        {
            viewport = existingViewport;
        }
        if (!viewport.gameObject.activeSelf) viewport.gameObject.SetActive(true);
        var oldMask = viewport.GetComponent<Mask>();
        if (oldMask != null) oldMask.enabled = false;
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();

        var existingContent = viewport.Find("Content") as RectTransform;
        if (existingContent == null)
        {
            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(viewport, false);
            content = cGo.transform as RectTransform;
        }
        else
        {
            content = existingContent;
        }
        if (!content.gameObject.activeSelf) content.gameObject.SetActive(true);

        // Stretch viewport to scroll
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.pivot = new Vector2(0.5f, 0.5f);

        sr.viewport = viewport;
        sr.content = content;
        sr.horizontal = false;
        sr.vertical = true;
        sr.inertia = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;
        feedScrollRect = sr;
        feedContent = content;
        feedContent.localScale = Vector3.one;

        // If feed area is collapsed by layout edits, give it a visible fallback size.
        if (feedScroll.rect.width < 20f || feedScroll.rect.height < 20f)
        {
            feedScroll.anchorMin = new Vector2(0f, 0f);
            feedScroll.anchorMax = new Vector2(1f, 1f);
            feedScroll.offsetMin = new Vector2(0f, 0f);
            feedScroll.offsetMax = new Vector2(0f, 0f);
            feedScroll.sizeDelta = new Vector2(0f, 0f);
        }

        // Ensure content starts at top-left and is not off-screen.
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Creates a dedicated runtime feed host under FloatingPanel to bypass broken/edited layout trees.
    /// This guarantees a visible scroll area even if Body/FeedScroll was modified in scene.
    /// </summary>
    private void EnsureGuaranteedRuntimeHost()
    {
        var floatingPanel = FindRect("FloatingPanel");
        if (floatingPanel == null) floatingPanel = transform as RectTransform;
        if (floatingPanel == null) return;

        var host = floatingPanel.Find("RuntimeFeedHost") as RectTransform;
        if (host == null)
        {
            var hostGo = new GameObject("RuntimeFeedHost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hostGo.transform.SetParent(floatingPanel, false);
            host = hostGo.transform as RectTransform;
            var img = hostGo.GetComponent<Image>();
            img.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
            img.raycastTarget = true;

            // Reserve top for TopBar + StatsBar.
            host.anchorMin = new Vector2(0f, 0f);
            host.anchorMax = new Vector2(1f, 1f);
            host.offsetMin = new Vector2(12f, 12f);
            host.offsetMax = new Vector2(-12f, -104f);
            host.pivot = new Vector2(0.5f, 0.5f);
            host.SetAsLastSibling();
        }
        if (!host.gameObject.activeSelf) host.gameObject.SetActive(true);
        var hostLE = host.GetComponent<LayoutElement>() ?? host.gameObject.AddComponent<LayoutElement>();
        hostLE.ignoreLayout = true;

        // Re-assert anchors each refresh in case scene layout changed them.
        host.anchorMin = new Vector2(0f, 0f);
        host.anchorMax = new Vector2(1f, 1f);
        host.offsetMin = new Vector2(12f, 12f);
        host.offsetMax = new Vector2(-12f, -104f);
        host.pivot = new Vector2(0.5f, 0.5f);

        var scroll = host.Find("FeedScrollRT") as RectTransform;
        if (scroll == null)
        {
            var sGo = new GameObject("FeedScrollRT", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            sGo.transform.SetParent(host, false);
            scroll = sGo.transform as RectTransform;
            var sImg = sGo.GetComponent<Image>();
            sImg.color = new Color(0, 0, 0, 0.08f);
            sImg.raycastTarget = true;
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = Vector2.zero;
            scroll.offsetMax = Vector2.zero;

            var sr = sGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
        }

        var viewport = scroll.Find("Viewport") as RectTransform;
        if (viewport == null)
        {
            var vGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            vGo.transform.SetParent(scroll, false);
            viewport = vGo.transform as RectTransform;
            var vImg = vGo.GetComponent<Image>();
            vImg.color = new Color(0, 0, 0, 0);
            vImg.raycastTarget = true;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
        }

        var content = viewport.Find("Content") as RectTransform;
        if (content == null)
        {
            var cGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            cGo.transform.SetParent(viewport, false);
            content = cGo.transform as RectTransform;
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fit = content.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        feedScrollRect = scrollRect;
        feedContent = content;
    }

    private void EnsureWindowFocusHandler()
    {
        var root = transform as RectTransform;
        var panel = FindRect("FloatingPanel");
        if (panel == null || root == null) return;

        var img = panel.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        var focus = panel.GetComponent<WindowFocusOnClick>();
        if (focus == null) focus = panel.gameObject.AddComponent<WindowFocusOnClick>();
        focus.SetTarget(root);
    }

    private List<PostData> BuildDisplayFeed(List<PostData> approvedPosts)
    {
        var feed = new List<PostData>(GenerateFillerPosts());
        if (approvedPosts != null && approvedPosts.Count > 0)
            feed.InsertRange(0, approvedPosts);
        return feed;
    }

    private List<PostData> GenerateFillerPosts()
    {
        var posts = new List<PostData>();
        string[] texts =
        {
            "Gym update: finally hit a new PR today, let's go.",
            "Anyone know a good cafe near downtown with outlets and fast wifi?",
            "New game patch dropped and it fixed so many issues.",
            "Rainy day playlist recommendations? Need chill tracks.",
            "Tried making homemade ramen tonight and it actually worked.",
            "Weekend plans: movie night and catching up on sleep."
        };

        for (int i = 0; i < texts.Length; i++)
        {
            var post = new PostData
            {
                id = $"demo_{i}",
                authorUserId = $"user{i + 1}",
                text = texts[i],
                timestampLabel = $"{(i + 1) * 7}m",
                likes = 150 + (i * 83),
                shares = 40 + (i * 21),
                comments = 18 + (i * 9),
                category = PostCategory.Harmless,
                severity = 0,
                isPublished = true
            };
            // Use the same seeded contextual pipeline as real moderated posts.
            PostManager.AssignDefaultBranches(post, new System.Random(10_000 + i));
            PostManager.ApplyDecisionReaction(post, playerChoseApprove: true, users: null);
            posts.Add(post);
            PostManager.RefreshEngagementLabel(post);
        }

        return posts;
    }

    private void CreateFeedCard(PostData post, UserProfileData user)
    {
        var card = new GameObject($"FeedCard_{post.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card.transform.SetParent(feedContent, false);
        card.transform.localScale = Vector3.one;
        var cardRt = card.transform as RectTransform;
        cardRt.anchorMin = new Vector2(0f, 1f);
        cardRt.anchorMax = new Vector2(1f, 1f);
        cardRt.pivot = new Vector2(0.5f, 1f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(0f, 0f);

        var cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.14f, 0.15f, 0.18f, 0.96f);

        var cardLayout = card.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(14, 14, 12, 12);
        cardLayout.spacing = 8;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        var cardFit = card.GetComponent<ContentSizeFitter>();
        cardFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        cardFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var cardLE = card.AddComponent<LayoutElement>();
        cardLE.minHeight = 120f;

        var topRow = CreateHorizontalRow("TopRow", card.transform as RectTransform, 6);
        string display = user != null
            ? $"{user.displayName}  @{user.username}"
            : $"@{post.authorUserId}";
        CreateTMP("AuthorText", topRow, display, 17, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.96f, 0.98f, 1f));

        var tag = CreateTMP("CategoryTag", topRow, CategoryLabel(post.category), 12, TextAlignmentOptions.MidlineRight, CategoryColor(post.category));
        var tagLE = tag.gameObject.AddComponent<LayoutElement>();
        tagLE.preferredWidth = 140;

        CreateTMP("BodyText", card.transform as RectTransform, SanitizeForTMP(post.text), 18, TextAlignmentOptions.TopLeft, Color.white).textWrappingMode = TextWrappingModes.Normal;

        var metaRow = CreateHorizontalRow("MetaRow", card.transform as RectTransform, 8);
        CreateTMP("EngagementText", metaRow, post.EngagementDisplay, 14, TextAlignmentOptions.MidlineLeft, new Color(0.76f, 0.83f, 0.97f, 1f));
        CreateTMP("TimeText", metaRow, post.timestampLabel, 14, TextAlignmentOptions.MidlineRight, new Color(0.72f, 0.72f, 0.76f, 1f));

        if (!string.IsNullOrEmpty(post.engagementLabel))
        {
            bool trending = string.Equals(post.engagementLabel, "TRENDING", System.StringComparison.Ordinal);
            var chipColor = trending ? new Color(1f, 0.62f, 0.28f, 1f) : new Color(0.65f, 0.72f, 0.82f, 1f);
            CreateTMP("EngagementLabel", card.transform as RectTransform, post.engagementLabel, 12, TextAlignmentOptions.MidlineLeft, chipColor);
        }

        string state = BuildStateLabel(post, user);
        if (!string.IsNullOrEmpty(state))
            CreateTMP("StateText", card.transform as RectTransform, state, 13, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.76f, 0.35f, 1f));

        if (post.commentPreview != null && post.commentPreview.Count > 0)
        {
            var commentsToggle = CreateActionButton(card.transform as RectTransform, $"Comments ({post.commentPreview.Count})");

            var commentsPanel = new GameObject("CommentsPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            commentsPanel.transform.SetParent(card.transform, false);
            commentsPanel.transform.localScale = Vector3.one;
            var commentsPanelRt = commentsPanel.transform as RectTransform;
            commentsPanelRt.anchorMin = new Vector2(0f, 1f);
            commentsPanelRt.anchorMax = new Vector2(1f, 1f);
            commentsPanelRt.pivot = new Vector2(0.5f, 1f);
            commentsPanelRt.sizeDelta = Vector2.zero;

            var commentsLayout = commentsPanel.GetComponent<VerticalLayoutGroup>();
            commentsLayout.padding = new RectOffset(8, 8, 6, 6);
            commentsLayout.spacing = 4;
            commentsLayout.childAlignment = TextAnchor.UpperLeft;
            commentsLayout.childControlWidth = true;
            commentsLayout.childControlHeight = true;
            commentsLayout.childForceExpandWidth = true;
            commentsLayout.childForceExpandHeight = false;

            commentsPanel.SetActive(false);
            commentsToggle.onClick.AddListener(() => commentsPanel.SetActive(!commentsPanel.activeSelf));

            CreateTMP("CommentHeader", commentsPanelRt, "Top comments", 13, TextAlignmentOptions.MidlineLeft, new Color(0.70f, 0.76f, 0.88f, 1f));
            int show = Mathf.Min(3, post.commentPreview.Count);
            for (int i = 0; i < show; i++)
            {
                var c = post.commentPreview[i];
                string commenter = c.authorUserId;
                var commentUser = GameDatabase.Instance != null ? GameDatabase.Instance.GetUser(c.authorUserId) : null;
                if (commentUser != null && !string.IsNullOrEmpty(commentUser.username))
                    commenter = $"@{commentUser.username}";

                CreateTMP(
                    $"Comment_{i}",
                    commentsPanelRt,
                    $"{commenter}: {SanitizeForTMP(c.text)}",
                    13,
                    TextAlignmentOptions.TopLeft,
                    new Color(0.88f, 0.90f, 0.95f, 1f)
                ).textWrappingMode = TextWrappingModes.Normal;
            }
        }
    }

    private static Button CreateActionButton(RectTransform parent, string label)
    {
        var go = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.19f, 0.24f, 1f);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 28f;
        le.flexibleWidth = 0f;

        var btn = go.GetComponent<Button>();
        var txt = CreateTMP("Label", go.transform as RectTransform, label, 12, TextAlignmentOptions.Center, new Color(0.79f, 0.88f, 0.98f, 1f));
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.raycastTarget = false;
        var txtRt = txt.transform as RectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        return btn;
    }

    private static RectTransform CreateHorizontalRow(string name, RectTransform parent, int spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var row = go.GetComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(0, 0, 0, 0);
        row.spacing = spacing;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return go.transform as RectTransform;
    }

    private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, string text, int size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static string BuildStateLabel(PostData post, UserProfileData user)
    {
        if (post.wasRewrittenByAlgorithm) return "Rewritten by algorithm";
        if (user != null && user.isShadowBanned) return "Author visibility limited";
        if (post.isShadowBanned) return "Post visibility limited";
        if (post.isRemoved) return "Removed from public feed";
        return string.Empty;
    }

    private static string CategoryLabel(PostCategory category)
    {
        return category switch
        {
            PostCategory.Harmless => "Harmless",
            PostCategory.Violation => "Violation",
            PostCategory.Misinformation => "Misinformation",
            PostCategory.GrayArea => "Gray Area",
            PostCategory.Narrative => "Narrative",
            PostCategory.AlgorithmManipulation => "Meta",
            _ => "Post"
        };
    }

    private static Color CategoryColor(PostCategory category)
    {
        return category switch
        {
            PostCategory.Harmless => new Color(0.54f, 0.88f, 0.56f, 1f),
            PostCategory.Violation => new Color(0.96f, 0.43f, 0.43f, 1f),
            PostCategory.Misinformation => new Color(0.99f, 0.64f, 0.31f, 1f),
            PostCategory.GrayArea => new Color(0.95f, 0.84f, 0.35f, 1f),
            PostCategory.Narrative => new Color(0.64f, 0.77f, 1f, 1f),
            PostCategory.AlgorithmManipulation => new Color(0.88f, 0.56f, 0.95f, 1f),
            _ => Color.white
        };
    }

    private void AutoBindByName()
    {
        feedStatsText ??= FindTMP("FeedStatsText");
        // Bind strictly to FeedScroll to avoid accidentally targeting another "Content" in scene.
        feedScrollRect ??= FindRect("FloatingPanel/Body/FeedScroll")?.GetComponent<ScrollRect>();
        feedScrollRect ??= FindRect("Body/FeedScroll")?.GetComponent<ScrollRect>();
        feedScrollRect ??= FindScrollRect("FeedScroll");

        if (feedScrollRect != null)
        {
            feedContent ??= feedScrollRect.content;
            if (feedContent == null)
            {
                var t = feedScrollRect.transform.Find("Viewport/Content");
                if (t != null) feedContent = t as RectTransform;
            }
        }

        refreshButton ??= FindButton("RefreshButton");
        refreshButton ??= FindButtonByLabel("Refresh");
    }

    private void WireButtonsIfPresent()
    {
        if (refreshButton == null) return;
        refreshButton.onClick.RemoveAllListeners();
        refreshButton.onClick.AddListener(RefreshFeedNow);
    }

    private TMP_Text FindTMP(string name)
    {
        foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.name == name) return t;
        return null;
    }

    private RectTransform FindRect(string path)
    {
        var t = transform.Find(path);
        return t as RectTransform;
    }

    private ScrollRect FindScrollRect(string name)
    {
        foreach (var s in GetComponentsInChildren<ScrollRect>(true))
            if (s.name == name) return s;
        return null;
    }

    private Button FindButton(string name)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
        return null;
    }

    private Button FindButtonByLabel(string labelText)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && string.Equals(label.text?.Trim(), labelText, StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }

    private static string SanitizeForTMP(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (char.IsSurrogate(ch))
            {
                if (i + 1 < value.Length && char.IsSurrogatePair(value[i], value[i + 1]))
                    i++;
                continue;
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
