using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.Social;
using GlitchInTheSystem.UI;

/// <summary>
/// Builder for Social Media window: freeform panels + visible editor feed cards to lay out in Scene view.
/// </summary>
public static class SocialMediaAppBuilder
{
    private const int DefaultEditorPostCount = 3;

    [MenuItem("Glitch In The System/UI/Social Media/Create App Window", false, 12)]
    public static void CreateSocialMediaAppWindow()
    {
        var parent = Selection.activeTransform;
        if (parent == null)
        {
            EditorUtility.DisplayDialog(
                "Create Social Media App Window",
                "Select a parent Transform first (usually your Canvas or DesktopRoot), then run this again.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Social Media App Window");

        var root = CreatePanel("SocialMediaAppWindow", parent, new Color(0f, 0f, 0f, 0f));
        Stretch(root);
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null) rootImg.raycastTarget = false;

        var parentRect = parent as RectTransform;
        Vector2 parentSize = parentRect != null ? parentRect.rect.size : new Vector2(1920, 1080);
        Vector2 size = new Vector2(
            Mathf.Clamp(parentSize.x * 0.82f, 900f, 1300f),
            Mathf.Clamp(parentSize.y * 0.82f, 640f, 900f));

        var floatingPanel = CreatePanel("FloatingPanel", root, new Color(0.11f, 0.12f, 0.15f, 0.98f));
        floatingPanel.anchorMin = new Vector2(0.5f, 0.5f);
        floatingPanel.anchorMax = new Vector2(0.5f, 0.5f);
        floatingPanel.pivot = new Vector2(0.5f, 0.5f);
        floatingPanel.anchoredPosition = Vector2.zero;
        floatingPanel.sizeDelta = size;

        var rootLayout = Undo.AddComponent<VerticalLayoutGroup>(floatingPanel.gameObject);
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        var topBar = CreatePanel("TopBar", floatingPanel, new Color(0.08f, 0.09f, 0.11f, 1f));
        SetPreferredHeight(topBar, 58);
        var topLayout = Undo.AddComponent<HorizontalLayoutGroup>(topBar.gameObject);
        topLayout.padding = new RectOffset(14, 14, 10, 10);
        topLayout.spacing = 10;
        topLayout.childAlignment = TextAnchor.MiddleLeft;
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;

        CreateTMP("AppTitle", topBar, "SOCIAL FEED", 24, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.96f, 0.98f, 1f));
        SetFlexibleWidth(FindChild(topBar, "AppTitle"), 1);

        var refreshBtn = CreateButton(topBar, "RefreshButton", "Refresh", new Color(0.24f, 0.33f, 0.48f, 1f));
        SetSize(refreshBtn.GetComponent<RectTransform>(), 110, 34);
        var closeBtn = CreateButton(topBar, "CloseButton", "X", new Color(0.26f, 0.20f, 0.22f, 1f));
        SetSize(closeBtn.GetComponent<RectTransform>(), 44, 30);

        var drag = Undo.AddComponent<DragPanel>(topBar.gameObject);
        var dragSO = new SerializedObject(drag);
        dragSO.FindProperty("target").objectReferenceValue = floatingPanel;
        dragSO.ApplyModifiedPropertiesWithoutUndo();

        var statsBar = CreatePanel("StatsBar", floatingPanel, new Color(0.12f, 0.14f, 0.17f, 1f));
        SetPreferredHeight(statsBar, 40);
        CreateTMP("FeedStatsText", statsBar, "Edit EditorFeedPost cards below — Play builds live feed", 15, TextAlignmentOptions.MidlineLeft, new Color(0.80f, 0.87f, 0.98f, 1f));

        var body = CreateEmpty("Body", floatingPanel);
        SetFlexibleHeight(body, 1f);
        var bodyLayout = Undo.AddComponent<VerticalLayoutGroup>(body.gameObject);
        bodyLayout.padding = new RectOffset(12, 12, 12, 12);
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = true;

        var feedScroll = CreatePanel("FeedScroll", body, new Color(0.09f, 0.10f, 0.12f, 1f));
        SetFlexibleHeight(feedScroll, 1f);
        var scrollRect = Undo.AddComponent<ScrollRect>(feedScroll.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewport = CreatePanel("Viewport", feedScroll, new Color(0, 0, 0, 0));
        var viewportMask = Undo.AddComponent<Mask>(viewport.gameObject);
        viewportMask.showMaskGraphic = false;
        viewport.GetComponent<Image>().raycastTarget = true;
        scrollRect.viewport = viewport;

        var content = CreateEmpty("Content", viewport);
        scrollRect.content = content;

        var contentLayout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        contentLayout.spacing = 10;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        var contentFitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Stretch(viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var previewPosts = SocialMediaFeedPreviewData.CreatePreviewPosts(DefaultEditorPostCount);
        for (int i = 0; i < previewPosts.Count; i++)
            SocialMediaFeedEditorUtility.CreatePreviewCardOnContent(content, $"EditorFeedPost_{i}", previewPosts[i]);

        LayoutRebuilder.ForceRebuildLayoutImmediate(floatingPanel);
        foreach (var editorPost in content.GetComponentsInChildren<SocialMediaFeedEditorPost>(true))
            LayoutRebuilder.ForceRebuildLayoutImmediate(editorPost.transform as RectTransform);

        RemoveFreeformLayoutDriversUnder(floatingPanel, preserveFeedContentLayout: true);
        // Keep layout on EditorFeedPost cards so Scene view shows full post/comment stacks.

        var controller = Undo.AddComponent<SocialMediaFeedController>(root.gameObject);
        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("feedStatsText").objectReferenceValue = FindChildTMP(root, "FeedStatsText");
        controllerSO.FindProperty("feedContent").objectReferenceValue = content;
        controllerSO.FindProperty("feedScrollRect").objectReferenceValue = scrollRect;
        controllerSO.FindProperty("refreshButton").objectReferenceValue = refreshBtn;
        controllerSO.ApplyModifiedPropertiesWithoutUndo();

        var appWindow = root.GetComponent<SimpleAppWindow>();
        if (appWindow == null)
            appWindow = Undo.AddComponent<SimpleAppWindow>(root.gameObject);

        Undo.RecordObject(closeBtn, "Wire close button");
        closeBtn.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(closeBtn.onClick, appWindow.Close);

        Selection.activeObject = root.gameObject;
        Undo.CollapseUndoOperations(group);
        controller.PrepareEditModeLayout();
    }

    /// <summary>
    /// Creates editor posts (if needed), turns the window on, and refreshes layout — Play can stay off.
    /// Does not change canvas render mode (avoids blue/broken Game view).
    /// </summary>
    [MenuItem("Glitch In The System/UI/Social Media/Create Design Template Post", false, 13)]
    public static void CreateDesignTemplatePostMenu()
    {
        var controller = ResolveSocialMediaController();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Social Media", "No SocialMediaAppWindow in this scene.", "OK");
            return;
        }

        SocialMediaCanvasSceneViewEditor.RestoreAll();
        SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(controller, rebuildAll: true, selectTemplate: true);
        controller.PrepareEditModeLayout();
    }

    [MenuItem("Glitch In The System/UI/Social Media/Reset Design Template Post", false, 14)]
    public static void ResetDesignTemplatePostMenu()
    {
        var controller = ResolveSocialMediaController();
        if (controller == null)
        {
            EditorUtility.DisplayDialog(
                "Social Media",
                "No SocialMediaAppWindow in this scene.",
                "OK");
            return;
        }

        SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(controller, rebuildAll: true, selectTemplate: true);
        controller.PrepareEditModeLayout();
    }

    [MenuItem("Glitch In The System/UI/Social Media/Unlock Layout (Edit Pos/Size)", false, 15)]
    public static void UnlockFreeformLayoutMenu() => UnlockLayoutForFreeformEditing(ResolveSocialMediaController());

    public static void UnlockLayoutForFreeformEditing(SocialMediaFeedController controller)
    {
        if (controller == null)
        {
            EditorUtility.DisplayDialog(
                "Unlock layout",
                "Select SocialMediaAppWindow in the Hierarchy first.",
                "OK");
            return;
        }

        SocialMediaCanvasSceneViewEditor.RestoreAll();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Unlock social feed layout");

        controller.PrepareEditModeLayout();

        var floatingPanel = controller.transform.Find("FloatingPanel") as RectTransform;
        var scope = floatingPanel != null ? floatingPanel : controller.transform as RectTransform;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scope);

        var content = GetSceneFeedContent(controller.transform);
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        var template = controller.GetPostDesignTemplate();
        if (template != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(template);

        RemoveFreeformLayoutDriversUnder(scope, preserveFeedContentLayout: false);

        var marker = scope.GetComponent<SocialMediaFeedFreeformLayout>();
        if (marker == null)
            Undo.AddComponent<SocialMediaFeedFreeformLayout>(scope.gameObject);

        SocialMediaFeedEditorUtility.EnsurePostsVisibleOnly(controller);

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(controller);
        if (controller.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        EditorUtility.DisplayDialog(
            "Layout unlocked",
            "Current positions and sizes are kept.\n\n" +
            "Use the Rect Tool on any object (posts, BodyText, CommentsPanel) and edit Rect Transform: Pos X/Y, Width, Height.\n\n" +
            "Layout groups will not override your values anymore.",
            "OK");
    }

    public static void ShowEditorFeedPosts(SocialMediaFeedController controller, bool addExtraPosts = false)
    {
        if (controller == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Show editor feed posts");

        if (addExtraPosts)
            AddEditorFeedPosts(controller);
        else
            SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(controller, rebuildAll: true, selectTemplate: false);

        SocialMediaCanvasSceneViewEditor.RestoreAll();
        controller.PrepareEditModeLayout();

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(controller);
        if (controller.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    /// <summary>Legacy name for inspector / scripts.</summary>
    public static void SetupForSceneEditing(SocialMediaFeedController controller, bool addExtraPosts, bool showBriefDialog = false)
        => ShowEditorFeedPosts(controller, addExtraPosts);

    public static void EnsureEditorFeedPostsInScene(SocialMediaFeedController controller)
    {
        SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(controller);
    }

    public static RectTransform GetSceneFeedContent(Transform controllerRoot)
        => SocialMediaFeedEditorUtility.GetSceneFeedContent(controllerRoot);

    private static void AddEditorFeedPosts(SocialMediaFeedController controller)
    {
        var posts = SocialMediaFeedPreviewData.CreatePreviewPosts(3);
        for (int i = 0; i < posts.Count; i++)
            SocialMediaFeedEditorUtility.AddPreviewPost(controller, posts[i]);
    }

    public static void ConvertSocialMediaToFreeform()
        => UnlockLayoutForFreeformEditing(ResolveSocialMediaController());

    private static void RemoveFreeformLayoutDriversUnder(Transform scope, bool preserveFeedContentLayout)
    {
        if (scope == null) return;

        void DestroyAll<T>() where T : Component
        {
            foreach (var c in scope.GetComponentsInChildren<T>(true))
            {
                if (c == null) continue;
                if (preserveFeedContentLayout && IsFeedContentLayoutDriver(c)) continue;
                Undo.DestroyObjectImmediate(c);
            }
        }

        DestroyAll<LayoutGroup>();
        DestroyAll<ContentSizeFitter>();
        DestroyAll<LayoutElement>();
        DestroyAll<AspectRatioFitter>();
    }

    private static bool IsFeedContentLayoutDriver(Component c)
    {
        if (c == null || c.transform.name != "Content") return false;
        var scroll = c.transform.parent != null ? c.transform.parent.parent : null;
        return scroll != null && scroll.name == "FeedScroll";
    }

    private static RectTransform GetSelectedSocialMediaRoot()
    {
        var go = Selection.activeGameObject;
        if (go == null) return null;
        if ( go.name == "SocialMediaAppWindow") return go.transform as RectTransform;
        var parent = go.transform.parent;
        while (parent != null)
        {
            if (parent.name == "SocialMediaAppWindow")
                return parent as RectTransform;
            parent = parent.parent;
        }
        return null;
    }

    private static SocialMediaFeedController GetSelectedSocialMediaController() => ResolveSocialMediaController();

    private static SocialMediaFeedController ResolveSocialMediaController()
    {
        var root = GetSelectedSocialMediaRoot();
        if (root != null)
        {
            var c = root.GetComponent<SocialMediaFeedController>();
            if (c != null) return c;
        }

        if (Selection.activeGameObject != null)
        {
            var fromSelection = Selection.activeGameObject.GetComponentInParent<SocialMediaFeedController>();
            if (fromSelection != null) return fromSelection;
        }

        return Object.FindFirstObjectByType<SocialMediaFeedController>(FindObjectsInactive.Include);
    }

    private static RectTransform FindFloatingPanel(Transform root) => root?.Find("FloatingPanel") as RectTransform;

    private static RectTransform FindChild(RectTransform parent, string childName) => parent.Find(childName) as RectTransform;

    private static TMP_Text FindChildTMP(RectTransform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.name == name) return t;
        return null;
    }

    private static RectTransform CreateEmpty(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        go.GetComponent<Image>().color = bg;
        return rt;
    }

    private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, string text, int size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        var labelTMP = CreateTMP("Label", go.GetComponent<RectTransform>(), label, 14, TextAlignmentOptions.Center, Color.white);
        Stretch(labelTMP.rectTransform);
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetPreferredHeight(RectTransform rt, float height)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.preferredHeight = height;
    }

    private static void SetFlexibleHeight(RectTransform rt, float flex)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.flexibleHeight = flex;
    }

    private static void SetFlexibleWidth(RectTransform rt, float flex)
    {
        if (rt == null) return;
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.flexibleWidth = flex;
    }

    private static void SetSize(RectTransform rt, float w, float h)
    {
        if (rt != null) rt.sizeDelta = new Vector2(w, h);
    }
}
