using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.Interruptions;
using GlitchInTheSystem.Social;
using GlitchInTheSystem.UI;

/// <summary>
/// Renders a social-media style feed from GameDatabase posts.
/// Edit mode: one <c>EditorFeedPost_Template</c> under Content for your design (images, panels).
/// Play mode: clones that template per post; live text comes from GameDatabase.
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

    [Header("Post design template")]
    [Tooltip("At play: clone EditorFeedPost_Template for each feed post (your images and panel design).")]
    [SerializeField] private bool usePostDesignTemplate = true;
    [SerializeField] private RectTransform postDesignTemplate;

    [Header("Edit mode preview")]
    [Tooltip("While not playing: show the design template under Content for editing.")]
    [SerializeField] private bool keepWindowVisibleInEditMode = true;
    [Tooltip("Expand comment panels on the template so comment lines are visible to edit.")]
    [SerializeField] private bool expandCommentPanelsInEditMode = true;

    private float _nextRefreshAt;
    private string _lastSignature;
    private bool _feedScrollInitialized;
    private ScrollRect _feedScrollRectUsedForInit;
    private Coroutine _restoreScrollRoutine;
    private SocialMediaFeedPlatformChrome _platformChrome;

#if UNITY_EDITOR
    public bool IsEditModeFreeformLayout =>
        GetComponentInChildren<SocialMediaFeedFreeformLayout>(true) != null;
    private bool _editLayoutBusy;
#endif

    public bool UsePostDesignTemplate => usePostDesignTemplate;

    private void Reset() => AutoBindByName();

    private void Awake()
    {
        AutoBindByName();
        AutoBindPostDesignTemplate();
        WireButtonsIfPresent();
        EnsureWindowFocusHandler();
    }

    private void OnEnable()
    {
        AutoBindByName();
        WireButtonsIfPresent();
        EnsureWindowFocusHandler();
        EnsurePlatformChrome();

        if (Application.isPlaying)
        {
            AlgorithmPostAlteredNotifier.PostAltered += OnFeedPostAltered;
            SetRuntimeFeedHostVisible(forceRuntimeFeedHost);
            SetDesignTemplateVisible(false);
            RefreshFeed(force: true);
            StartCoroutine(RefreshNextFrame());
            _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.2f, autoRefreshSeconds);
            NotifyInterruptionEligibleAppOpened();
        }
    }

    private void OnDisable()
    {
        if (_restoreScrollRoutine != null)
        {
            StopCoroutine(_restoreScrollRoutine);
            _restoreScrollRoutine = null;
        }

        if (Application.isPlaying)
            AlgorithmPostAlteredNotifier.PostAltered -= OnFeedPostAltered;
    }

    private void OnFeedPostAltered(PostData post, bool rewrite)
    {
        if (post == null || !isActiveAndEnabled) return;

        if (TryUpdateFeedCardInPlace(post))
            StartCoroutine(FlashFeedCardGlitchAfterLayout(post, rewrite));
    }

    private bool TryUpdateFeedCardInPlace(PostData post)
    {
        if (feedContent == null || post == null || GameDatabase.Instance == null) return false;

        var card = feedContent.Find($"FeedCard_{post.id}");
        if (card == null) return false;

        var user = GameDatabase.Instance.GetUser(post.authorUserId);
        SocialMediaFeedCardBinder.Apply(card, post, user, expandComments: false);
        RebuildFeedCardLayout(card as RectTransform);
        return true;
    }

    private IEnumerator FlashFeedCardGlitchAfterLayout(PostData post, bool emphasizeRewrite)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (feedContent == null || post == null) yield break;

        var card = feedContent.Find($"FeedCard_{post.id}");
        if (card == null) yield break;

        SocialMediaFeedCardBinder.FlashAlterationGlitch(card, post, emphasizeRewrite);
    }

    private void ScheduleRestoreFeedScroll(float normalizedPosition)
    {
        if (_restoreScrollRoutine != null)
            StopCoroutine(_restoreScrollRoutine);
        _restoreScrollRoutine = StartCoroutine(RestoreFeedScrollAfterRebuild(normalizedPosition));
    }

    private IEnumerator RestoreFeedScrollAfterRebuild(float normalizedPosition)
    {
        float clamped = Mathf.Clamp01(normalizedPosition);
        yield return null;
        RebuildFeedLayout();
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (feedContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(feedContent);
        yield return new WaitForEndOfFrame();

        if (feedScrollRect != null)
        {
            feedScrollRect.StopMovement();
            feedScrollRect.verticalNormalizedPosition = clamped;
        }

        _restoreScrollRoutine = null;
    }

    private void EnsurePlatformChrome()
    {
        var panel = FindRect("FloatingPanel");
        if (panel == null) return;
        _platformChrome ??= GetComponent<SocialMediaFeedPlatformChrome>();
        if (_platformChrome == null)
            _platformChrome = gameObject.AddComponent<SocialMediaFeedPlatformChrome>();
        if (Application.isPlaying)
            _platformChrome.EnsureChrome(panel);
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        RefreshFeed(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
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
        if (Application.isPlaying)
            RefreshFeed(force: true);
        else
            PrepareEditModeLayout();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Keeps scene feed cards visible for editing. Does not destroy or rebuild layout objects.
    /// </summary>
    public void PrepareEditModeLayout()
    {
        if (Application.isPlaying || _editLayoutBusy) return;

        _editLayoutBusy = true;
        try
        {
            PrepareEditModeLayoutInternal();
        }
        finally
        {
            _editLayoutBusy = false;
        }
    }

    private void PrepareEditModeLayoutInternal()
    {
        if (keepWindowVisibleInEditMode)
            EnsureWindowVisibleForSceneEditing();

        SetRuntimeFeedHostVisible(false);
        AutoBindByName();

        if (feedContent == null)
        {
            if (feedStatsText != null) feedStatsText.text = "Assign FeedScroll → Viewport → Content";
            return;
        }

        EnsureEditModeFeedScrollLayout();
        EnsureEditModeFeedViewportVisible();
        RemoveStrayRuntimeFeedCards();
        SetDesignTemplateVisible(true);
        if (expandCommentPanelsInEditMode)
            ExpandTemplatePanelsForSceneEditing();

#if UNITY_EDITOR
        SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(this);
        EnsureFeedContentLayout();
        if (!IsEditModeFreeformLayout)
            SocialMediaFeedEditorUtility.ApplyEditModeTemplatePresentation(this, refreshLayout: false);
        else
            SocialMediaFeedEditorUtility.EnsurePostsVisibleOnly(this);
#endif

        if (feedStatsText != null)
        {
            bool hasTemplate = GetPostDesignTemplate() != null;
            feedStatsText.text = hasTemplate
                ? IsEditModeFreeformLayout
                    ? "Design template  |  freeform  |  edit images + panels on EditorFeedPost_Template"
                    : "Design template  |  edit EditorFeedPost_Template — Play clones it for every post"
                : "Inspector → Create design template post";
        }

        RebuildFeedLayout();
    }

    private void EnsureEditModeFeedViewportVisible()
    {
        var feedScroll = FindRect("FloatingPanel/Body/FeedScroll");
        if (feedScroll == null) return;

        var viewport = feedScroll.Find("Viewport");
        if (viewport == null) return;

        var legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
            legacyMask.enabled = false;

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        var vpImg = viewport.GetComponent<Image>();
        if (vpImg != null)
            vpImg.enabled = true;
    }

    private void EnsureEditModeFeedScrollLayout()
    {
        var feedScroll = FindRect("FloatingPanel/Body/FeedScroll");
        if (feedScroll == null) return;

        feedScroll.anchorMin = Vector2.zero;
        feedScroll.anchorMax = Vector2.one;
        feedScroll.pivot = new Vector2(0.5f, 0.5f);
        feedScroll.anchoredPosition = Vector2.zero;
        feedScroll.sizeDelta = Vector2.zero;
        feedScroll.offsetMin = Vector2.zero;
        feedScroll.offsetMax = Vector2.zero;
        feedScroll.localScale = Vector3.one;

        var viewport = feedScroll.Find("Viewport") as RectTransform;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.localScale = Vector3.one;
        }

        if (feedContent != null)
        {
            feedContent.anchorMin = new Vector2(0f, 1f);
            feedContent.anchorMax = new Vector2(1f, 1f);
            feedContent.pivot = new Vector2(0.5f, 1f);
            feedContent.anchoredPosition = Vector2.zero;
            feedContent.sizeDelta = new Vector2(0f, 0f);
        }

        if (feedScrollRect != null)
            feedScrollRect.verticalNormalizedPosition = 1f;
    }

    private void EnsureWindowVisibleForSceneEditing()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ActivateHierarchyPath(
            "FloatingPanel",
            "FloatingPanel/Body",
            "FloatingPanel/Body/FeedScroll",
            "FloatingPanel/Body/FeedScroll/Viewport",
            "FloatingPanel/Body/FeedScroll/Viewport/Content");

        var panel = FindRect("FloatingPanel");
        if (panel != null)
        {
            var img = panel.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }
    }

    private void ActivateHierarchyPath(params string[] paths)
    {
        foreach (var path in paths)
        {
            var t = transform.Find(path);
            if (t != null && !t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
    }

    private void ExpandTemplatePanelsForSceneEditing()
    {
        var template = GetPostDesignTemplate();
        if (template == null) return;

        template.gameObject.SetActive(true);

        var panel = template.Find("CommentsPanel");
        if (panel == null)
            panel = template.Find("CommentsSection/CommentsPanel");
        if (panel != null)
        {
            panel.gameObject.SetActive(true);
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.enabled = true;
                tmp.gameObject.SetActive(true);
            }
        }

        foreach (var tmp in template.GetComponentsInChildren<TMP_Text>(true))
        {
            tmp.enabled = true;
            tmp.gameObject.SetActive(true);
        }
    }

    public RectTransform GetPostDesignTemplate()
    {
        AutoBindPostDesignTemplate();
        return postDesignTemplate;
    }

    private void AutoBindPostDesignTemplate()
    {
        if (postDesignTemplate != null) return;

        var marker = GetComponentInChildren<SocialMediaFeedPostTemplate>(true);
        if (marker != null)
            postDesignTemplate = marker.transform as RectTransform;
        else if (feedContent != null)
        {
            var t = feedContent.Find(SocialMediaFeedPostTemplate.TemplateObjectName);
            if (t != null)
                postDesignTemplate = t as RectTransform;
        }
    }

    private void SetDesignTemplateVisible(bool visible)
    {
        var template = GetPostDesignTemplate();
        if (template != null)
            template.gameObject.SetActive(visible);
    }

    /// <summary>Optional: fill sample text without changing RectTransforms.</summary>
    public void SyncEditorLayoutPostText()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        SocialMediaFeedEditorUtility.SyncTemplatePreviewText(this, rebuildLayout: false);
#endif
    }

    public void RefreshDesignTemplateLayout()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        SocialMediaFeedEditorUtility.SyncTemplatePreviewText(this, rebuildLayout: true);
        PrepareEditModeLayout();
#endif
    }
#endif

    private void RefreshFeed(bool force)
    {
        if (!Application.isPlaying) return;

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

        EnsureFeedContentLayout();

        string signature = FeedManager.BuildSignature(posts);
        if (!force && signature == _lastSignature) return;
        _lastSignature = signature;

        bool preserveScroll = feedScrollRect != null
            && _feedScrollInitialized
            && _feedScrollRectUsedForInit == feedScrollRect;
        float savedScroll = preserveScroll ? feedScrollRect.verticalNormalizedPosition : 1f;

        RebuildEntries(posts, id => GameDatabase.Instance.GetUser(id));

        if (feedStatsText != null)
        {
            int day = GameDatabase.Instance.Config != null ? GameDatabase.Instance.Config.currentDay : 1;
            int rendered = feedContent != null ? feedContent.childCount : 0;
            string quirk = day == 3 ? "  |  Sync: unstable" : "";
            feedStatsText.text = $"Day {day}{quirk}  |  Live: {approvedPosts.Count}  |  Feed: {posts.Count}  |  Rendered: {rendered}";
        }

        if (feedScrollRect != null)
        {
            if (!preserveScroll)
            {
                RebuildFeedLayout();
                feedScrollRect.verticalNormalizedPosition = 1f;
                _feedScrollInitialized = true;
                _feedScrollRectUsedForInit = feedScrollRect;
            }
            else
                ScheduleRestoreFeedScroll(savedScroll);
        }
        else
            RebuildFeedLayout();
    }

    private void RebuildEntries(IReadOnlyList<PostData> posts, Func<string, UserProfileData> getUser)
    {
        ClearFeedChildren(skipTemplateAndEditorPosts: true);

        if (posts.Count == 0)
        {
            CreateEmptyStateCard();
            return;
        }

        var template = usePostDesignTemplate ? GetPostDesignTemplate() : null;
        if (template != null)
        {
            for (int i = 0; i < posts.Count; i++)
                CreateFeedCardFromTemplate(template, posts[i], getUser?.Invoke(posts[i].authorUserId));
            return;
        }

        foreach (var post in posts)
            CreateFeedCard(post, getUser?.Invoke(post.authorUserId));
    }

    private void CreateFeedCardFromTemplate(RectTransform template, PostData post, UserProfileData user)
    {
        var clone = Instantiate(template.gameObject, feedContent);
        clone.name = $"FeedCard_{post.id}";
        clone.transform.SetAsLastSibling();
        clone.SetActive(true);

        var marker = clone.GetComponent<SocialMediaFeedPostTemplate>();
        if (marker != null) Destroy(marker);
        var legacyEditor = clone.GetComponent<SocialMediaFeedEditorPost>();
        if (legacyEditor != null) Destroy(legacyEditor);

        var cardRt = clone.transform as RectTransform;
        if (cardRt != null)
            PrepareClonedCardForFeedLayout(cardRt);

        SocialMediaFeedCardBinder.Apply(clone.transform, post, user, expandComments: false);
        SocialMediaFeedLayoutConstraints.PrepareRuntimeFeedCard(clone.transform as RectTransform);
        WireRuntimeCommentsToggle(clone.transform, post);
    }

    private void WireRuntimeCommentsToggle(Transform cardRoot, PostData post)
    {
        int commentCount = post?.commentPreview?.Count ?? 0;
        if (commentCount <= 0) return;

        var panelTransform = cardRoot.Find("CommentsPanel");
        if (panelTransform == null)
            panelTransform = cardRoot.Find("CommentsSection/CommentsPanel");
        if (panelTransform == null) return;

        var panelGo = panelTransform.gameObject;
        panelGo.SetActive(false);

        Button toggleButton = null;
        foreach (var button in cardRoot.GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;
            if (button.name != "ActionButton" && button.name != "CommentsToggle") continue;
            toggleButton = button;
            break;
        }

        if (toggleButton == null) return;

        toggleButton.onClick.RemoveAllListeners();
        toggleButton.onClick.AddListener(() =>
        {
            bool show = !panelGo.activeSelf;
            panelGo.SetActive(show);
            if (!show) return;

            var panelRt = panelTransform as RectTransform;
            SocialMediaFeedLayoutConstraints.PrepareRuntimeCommentsPanel(panelRt);
            RebuildFeedCardLayout(cardRoot as RectTransform);
        });
    }

    private void RebuildFeedCardLayout(RectTransform cardRt)
    {
        if (cardRt == null) return;
        Canvas.ForceUpdateCanvases();
        var panel = cardRt.Find("CommentsPanel") as RectTransform
            ?? cardRt.Find("CommentsSection/CommentsPanel") as RectTransform;
        if (panel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRt);
        if (feedContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(feedContent);
    }

    private static void PrepareClonedCardForFeedLayout(RectTransform cardRt)
    {
        cardRt.localScale = Vector3.one;
        cardRt.anchorMin = new Vector2(0f, 1f);
        cardRt.anchorMax = new Vector2(1f, 1f);
        cardRt.pivot = new Vector2(0.5f, 1f);
        cardRt.anchoredPosition = Vector2.zero;

        float h = cardRt.sizeDelta.y;
        if (h < 40f)
            h = 280f;

        var le = cardRt.GetComponent<LayoutElement>();
        if (le == null)
            le = cardRt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleWidth = 1f;
    }

    private void ClearFeedChildren(bool skipTemplateAndEditorPosts)
    {
        if (feedContent == null) return;

        for (int i = feedContent.childCount - 1; i >= 0; i--)
        {
            var child = feedContent.GetChild(i);
            if (child == null) continue;
            if (skipTemplateAndEditorPosts && IsDesignTemplateOrLegacyEditorPost(child))
                continue;
            DestroyFeedObject(child.gameObject);
        }
    }

    private static bool IsDesignTemplateOrLegacyEditorPost(Transform child)
    {
        if (child == null) return false;
        if (child.GetComponent<SocialMediaFeedPostTemplate>() != null) return true;
        if (child.GetComponent<SocialMediaFeedEditorPost>() != null) return true;
        return child.name == SocialMediaFeedPostTemplate.TemplateObjectName;
    }

    private void RemoveStrayRuntimeFeedCards()
    {
        if (feedContent == null) return;

        for (int i = feedContent.childCount - 1; i >= 0; i--)
        {
            var child = feedContent.GetChild(i);
            if (child == null) continue;
            if (IsDesignTemplateOrLegacyEditorPost(child)) continue;
            string n = child.name;
            if (n.StartsWith("FeedCard_", StringComparison.Ordinal) || n == "EmptyState")
                DestroyFeedObject(child.gameObject);
        }
    }

    private void SetRuntimeFeedHostVisible(bool visible)
    {
        var host = FindRect("FloatingPanel/RuntimeFeedHost");
        if (host != null)
            host.gameObject.SetActive(visible);
    }


    private static void DestroyFeedObject(GameObject go)
    {
        if (go == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(go);
        else
#endif
            Destroy(go);
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

        card.GetComponent<Image>().color = new Color(0.14f, 0.15f, 0.18f, 0.96f);

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
        string display = user != null ? $"{user.displayName}  @{user.username}" : $"@{post.authorUserId}";
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
            bool trending = string.Equals(post.engagementLabel, "TRENDING", StringComparison.Ordinal);
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
        go.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.24f, 1f);
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

    private void EnsureFeedContentLayout()
    {
        if (feedContent == null) return;

        feedContent.anchorMin = new Vector2(0f, 1f);
        feedContent.anchorMax = new Vector2(1f, 1f);
        feedContent.pivot = new Vector2(0.5f, 1f);
        feedContent.anchoredPosition = Vector2.zero;
        feedContent.sizeDelta = new Vector2(0f, feedContent.sizeDelta.y);

        var layout = feedContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = feedContent.gameObject.AddComponent<VerticalLayoutGroup>();
        bool editTemplateOnly = !Application.isPlaying && GetPostDesignTemplate() != null;
        layout.padding = editTemplateOnly ? new RectOffset(8, 8, 10, 16) : new RectOffset(0, 0, 0, 0);
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
    }

    private void RebuildFeedLayout()
    {
        if (feedContent == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(feedContent);
    }

    private void EnsureRuntimeFeedTree()
    {
        if (feedContent != null && feedContent.gameObject.activeInHierarchy && feedScrollRect != null)
            return;

        AutoBindByName();
        if (feedContent != null && feedScrollRect != null) return;

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
            feedGo.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.12f, 1f);
            var le = feedGo.GetComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.flexibleWidth = 1f;
            le.minHeight = 200f;
            sr = feedGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
        }

        if (!feedScroll.gameObject.activeSelf) feedScroll.gameObject.SetActive(true);

        feedScroll.anchorMin = Vector2.zero;
        feedScroll.anchorMax = Vector2.one;
        feedScroll.offsetMin = Vector2.zero;
        feedScroll.offsetMax = Vector2.zero;
        feedScroll.pivot = new Vector2(0.5f, 0.5f);

        var scrollLE = feedScroll.GetComponent<LayoutElement>() ?? feedScroll.gameObject.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1f;
        scrollLE.flexibleWidth = 1f;
        if (scrollLE.minHeight < 200f) scrollLE.minHeight = 200f;

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

        if (feedScroll.rect.width < 20f || feedScroll.rect.height < 20f)
        {
            feedScroll.anchorMin = new Vector2(0f, 0f);
            feedScroll.anchorMax = new Vector2(1f, 1f);
            feedScroll.offsetMin = Vector2.zero;
            feedScroll.offsetMax = Vector2.zero;
            feedScroll.sizeDelta = Vector2.zero;
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
    }

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
            host.SetAsLastSibling();
        }

        if (!host.gameObject.activeSelf) host.gameObject.SetActive(true);
        var hostLE = host.GetComponent<LayoutElement>() ?? host.gameObject.AddComponent<LayoutElement>();
        hostLE.ignoreLayout = true;

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
            sGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.08f);
            sGo.GetComponent<Image>().raycastTarget = true;
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

    private List<PostData> BuildDisplayFeed(List<PostData> approvedPosts)
    {
        // Player-approved posts first (algorithm may have altered them — personal stakes).
        var feed = new List<PostData>();
        if (approvedPosts != null && approvedPosts.Count > 0)
            feed.AddRange(approvedPosts);

        if (GameDatabase.Instance != null && GameDatabase.Instance.Users.Count > 0)
        {
            var users = GameDatabase.Instance.Users;
            var rng = new System.Random(90210);
            for (int i = 0; i < 3; i++)
            {
                var author = users[rng.Next(users.Count)];
                feed.Add(ModerationContentPools.BuildAmbientFeedPost(author, i, rng));
            }
        }

        var filler = GenerateFillerPosts();
        int fillerCap = Mathf.Min(4, filler.Count);
        for (int f = 0; f < fillerCap; f++)
            feed.Add(filler[f]);

        return feed;
    }

    private List<PostData> GenerateFillerPosts()
    {
        var posts = SocialMediaFeedPreviewData.CreatePlayFillerPosts();
        var rng = new System.Random(4242);
        foreach (var p in posts)
            OrganicEngagementUtility.ApplyToPost(p, rng, p.category);
        return posts;
    }

    private static void NotifyInterruptionEligibleAppOpened()
    {
        var manager = FindFirstObjectByType<InterruptionManager>();
        manager?.OnEligibleAppOpened();
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

    private void AutoBindByName()
    {
        feedStatsText ??= FindTMP("FeedStatsText");

        if (Application.isPlaying)
        {
            feedScrollRect ??= FindScrollRect("FeedScrollRT");
            feedScrollRect ??= FindRect("FloatingPanel/Body/FeedScroll")?.GetComponent<ScrollRect>();
            feedScrollRect ??= FindScrollRect("FeedScroll");
        }
        else
        {
            // Edit mode: always use the scene scroll under Body, not the empty RuntimeFeedHost copy.
            feedScrollRect = FindRect("FloatingPanel/Body/FeedScroll")?.GetComponent<ScrollRect>();
            if (feedScrollRect == null)
                feedScrollRect = FindRect("Body/FeedScroll")?.GetComponent<ScrollRect>();
            if (feedScrollRect == null)
                feedScrollRect = FindScrollRect("FeedScroll");
        }

        if (feedScrollRect != null)
        {
            feedContent = feedScrollRect.content;
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

    private static string BuildStateLabel(PostData post, UserProfileData user)
    {
        if (post.wasRewrittenByAlgorithm) return "Rewritten by algorithm";
        if (user != null && user.isShadowBanned) return "Author visibility limited";
        if (post.isShadowBanned) return "Post visibility limited";
        if (post.isRemoved) return "Removed from public feed";
        return string.Empty;
    }

    private static string CategoryLabel(PostCategory category) => SocialMediaFeedPresentation.CategoryLabel(category);
    private static Color CategoryColor(PostCategory category) => SocialMediaFeedPresentation.CategoryColor(category);
    private static string SanitizeForTMP(string value) => SocialMediaFeedPresentation.SanitizeForTMP(value);

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
}
