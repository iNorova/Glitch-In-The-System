using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.UI;

/// <summary>
/// Builder for a separate Social Media app window with scrollable feed.
/// </summary>
public static class SocialMediaAppBuilder
{
    [MenuItem("Glitch In The System/UI/Create Social Media App Window", false, 12)]
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
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.spacing = 0;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
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
        topLayout.childForceExpandWidth = false;
        topLayout.childForceExpandHeight = true;

        var title = CreateTMP("AppTitle", topBar, "SOCIAL FEED", 24, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.96f, 0.98f, 1f));
        SetFlexibleWidth(title.rectTransform, 1);

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
        var statsLayout = Undo.AddComponent<HorizontalLayoutGroup>(statsBar.gameObject);
        statsLayout.padding = new RectOffset(14, 14, 8, 8);
        statsLayout.spacing = 8;
        statsLayout.childAlignment = TextAnchor.MiddleLeft;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = true;

        CreateTMP("FeedStatsText", statsBar, "Feed loading...", 15, TextAlignmentOptions.MidlineLeft, new Color(0.80f, 0.87f, 0.98f, 1f));

        var body = CreateEmpty("Body", floatingPanel);
        SetFlexibleHeight(body, 1f);
        var bodyLayout = Undo.AddComponent<VerticalLayoutGroup>(body.gameObject);
        bodyLayout.padding = new RectOffset(12, 12, 12, 12);
        bodyLayout.spacing = 8;
        bodyLayout.childAlignment = TextAnchor.UpperLeft;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = true;

        var feedScroll = CreatePanel("FeedScroll", body, new Color(0.09f, 0.10f, 0.12f, 1f));
        SetFlexibleHeight(feedScroll, 1f);
        var scrollRect = Undo.AddComponent<ScrollRect>(feedScroll.gameObject);
        scrollRect.horizontal = false;

        var viewport = CreatePanel("Viewport", feedScroll, new Color(0, 0, 0, 0));
        var viewportMask = Undo.AddComponent<Mask>(viewport.gameObject);
        viewportMask.showMaskGraphic = false;
        viewport.GetComponent<Image>().raycastTarget = true;
        scrollRect.viewport = viewport;

        var content = CreateEmpty("Content", viewport);
        scrollRect.content = content;

        var contentLayout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.spacing = 10;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        var contentFitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Stretch(viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

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
    }

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
        var img = go.GetComponent<Image>();
        img.color = bg;
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
        var img = go.GetComponent<Image>();
        img.color = bg;
        var button = go.GetComponent<Button>();

        var labelTMP = CreateTMP("Label", go.GetComponent<RectTransform>(), label, 14, TextAlignmentOptions.Center, Color.white);
        Stretch(labelTMP.rectTransform);
        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetPreferredHeight(RectTransform rt, float height)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.preferredHeight = height;
        le.flexibleHeight = 0;
    }

    private static void SetFlexibleHeight(RectTransform rt, float flex)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.flexibleHeight = flex;
    }

    private static void SetFlexibleWidth(RectTransform rt, float flex)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.flexibleWidth = flex;
    }

    private static void SetSize(RectTransform rt, float w, float h)
    {
        rt.sizeDelta = new Vector2(w, h);
    }
}
