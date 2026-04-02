using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.UI;

/// <summary>
/// One-time scene builder for the Content Moderator / Work Dashboard UI.
/// Generates normal uGUI objects you can freely edit afterward.
/// </summary>
public static class WorkDashboardBuilder
{
    [MenuItem("Glitch In The System/UI/Create Work Dashboard Window", false, 10)]
    public static void CreateWorkDashboardWindow()
    {
        var parent = Selection.activeTransform;
        if (parent == null)
        {
            EditorUtility.DisplayDialog(
                "Create Work Dashboard Window",
                "Select a parent Transform first (usually your Canvas or DesktopRoot), then run this again.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Work Dashboard Window");

        // Root container (full screen, holds the floating window)
        var root = CreatePanel("WorkDashboardWindow", parent, new Color(0, 0, 0, 0));
        Stretch(root);
        root.gameObject.SetActive(false);
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null) rootImg.raycastTarget = false;

        // Floating panel (draggable, centered) — size matches parent
        var parentRect = parent as RectTransform;
        var size = parentRect != null ? parentRect.rect.size : new Vector2(1200, 800);
        var floatingPanel = CreatePanel("FloatingPanel", root, new Color(0.10f, 0.11f, 0.13f, 0.98f));
        floatingPanel.anchorMin = new Vector2(0.5f, 0.5f);
        floatingPanel.anchorMax = new Vector2(0.5f, 0.5f);
        floatingPanel.pivot = new Vector2(0.5f, 0.5f);
        floatingPanel.anchoredPosition = Vector2.zero;
        floatingPanel.sizeDelta = size;

        var vlg = Undo.AddComponent<VerticalLayoutGroup>(floatingPanel.gameObject);
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 0;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // TopBar (draggable handle)
        var topBar = CreatePanel("TopBar", floatingPanel, new Color(0.08f, 0.09f, 0.11f, 1f));
        SetPreferredHeight(topBar, 56);
        var topH = Undo.AddComponent<HorizontalLayoutGroup>(topBar.gameObject);
        topH.padding = new RectOffset(16, 16, 10, 10);
        topH.spacing = 12;
        topH.childAlignment = TextAnchor.MiddleLeft;
        topH.childControlWidth = true;
        topH.childControlHeight = true;
        topH.childForceExpandWidth = false;
        topH.childForceExpandHeight = true;

        var title = CreateTMP("AppTitle", topBar, "CONTENT MODERATOR", 26, TextAlignmentOptions.MidlineLeft);
        SetFlexibleWidth(title.rectTransform, 1);

        CreateTMP("DayInfo", topBar, "Day 1 — Posts: 0/10", 18, TextAlignmentOptions.MidlineRight);
        CreateTMP("Timer", topBar, "00:00", 18, TextAlignmentOptions.MidlineRight);

        var closeBtn = CreateButton(topBar, "CloseButton", "X", new Color(0.22f, 0.22f, 0.26f, 1f));
        SetSize(closeBtn.GetComponent<RectTransform>(), 44, 30);

        var dragPanel = Undo.AddComponent<DragPanel>(topBar.gameObject);
        var so = new SerializedObject(dragPanel);
        so.FindProperty("target").objectReferenceValue = floatingPanel;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Body
        var body = CreateEmpty("Body", floatingPanel);
        SetFlexibleHeight(body, 1);
        var bodyH = Undo.AddComponent<HorizontalLayoutGroup>(body.gameObject);
        bodyH.padding = new RectOffset(16, 16, 16, 16);
        bodyH.spacing = 16;
        bodyH.childAlignment = TextAnchor.UpperLeft;
        bodyH.childControlWidth = true;
        bodyH.childControlHeight = true;
        bodyH.childForceExpandWidth = true;
        bodyH.childForceExpandHeight = true;

        // LeftPanel
        var left = CreatePanel("LeftPanel", body, new Color(0.12f, 0.13f, 0.15f, 1f));
        SetPreferredWidth(left, 340);
        var leftV = Undo.AddComponent<VerticalLayoutGroup>(left.gameObject);
        leftV.padding = new RectOffset(14, 14, 14, 14);
        leftV.spacing = 12;
        leftV.childAlignment = TextAnchor.UpperLeft;
        leftV.childControlWidth = true;
        leftV.childControlHeight = true;
        leftV.childForceExpandWidth = true;
        leftV.childForceExpandHeight = false;

        CreateSectionHeader(left, "PERSON");
        BuildProfileCard(left);
        BuildStatsCard(left);
        BuildHistoryCard(left);

        // RightPanel
        var right = CreatePanel("RightPanel", body, new Color(0.12f, 0.13f, 0.15f, 1f));
        SetFlexibleWidth(right, 1);
        var rightV = Undo.AddComponent<VerticalLayoutGroup>(right.gameObject);
        rightV.padding = new RectOffset(14, 14, 14, 14);
        rightV.spacing = 12;
        rightV.childAlignment = TextAnchor.UpperLeft;
        rightV.childControlWidth = true;
        rightV.childControlHeight = true;
        rightV.childForceExpandWidth = true;
        rightV.childForceExpandHeight = true;

        BuildQueueRow(right);
        BuildPostCard(right);
        BuildFlagsRow(right);
        BuildDecisionHistory(right);

        // BottomBar
        var bottom = CreatePanel("BottomBar", floatingPanel, new Color(0.08f, 0.09f, 0.11f, 1f));
        SetPreferredHeight(bottom, 84);
        var bottomH = Undo.AddComponent<HorizontalLayoutGroup>(bottom.gameObject);
        bottomH.padding = new RectOffset(16, 16, 16, 16);
        bottomH.spacing = 12;
        bottomH.childAlignment = TextAnchor.MiddleLeft;
        bottomH.childControlWidth = true;
        bottomH.childControlHeight = true;
        bottomH.childForceExpandWidth = false;
        bottomH.childForceExpandHeight = true;

        var approve = CreateButton(bottom, "ApproveButton", "APPROVE", new Color(0.15f, 0.55f, 0.25f, 1f));
        SetSize(approve.GetComponent<RectTransform>(), 180, 52);
        var decline = CreateButton(bottom, "DeclineButton", "DECLINE", new Color(0.65f, 0.18f, 0.18f, 1f));
        SetSize(decline.GetComponent<RectTransform>(), 180, 52);

        var hint = CreateTMP("DecisionHint", bottom, "A = Approve    D = Decline", 16, TextAlignmentOptions.MidlineLeft);
        SetFlexibleWidth(hint.rectTransform, 1);

        CreateTMP("DecisionResultText", bottom, "—", 16, TextAlignmentOptions.MidlineRight);

        // Select the root for convenience.
        Selection.activeObject = root.gameObject;

        Undo.CollapseUndoOperations(group);
    }

    /// <summary>
    /// Converts an existing Work Dashboard to the floating/draggable layout in place.
    /// Select the WorkDashboardWindow (or any child) in the Hierarchy, then run this.
    /// </summary>
    [MenuItem("Glitch In The System/UI/Update Work Dashboard to Floating", false, 20)]
    public static void UpdateWorkDashboardToFloating()
    {
        var root = GetSelectedWorkDashboardRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog(
                "Update Work Dashboard to Floating",
                "Select the WorkDashboardWindow in the Hierarchy, then run this again.",
                "OK");
            return;
        }

        // Already has FloatingPanel?
        var existingFloating = root.Find("FloatingPanel");
        if (existingFloating != null)
        {
            EditorUtility.DisplayDialog(
                "Update Work Dashboard to Floating",
                "This dashboard is already using the floating layout.",
                "OK");
            return;
        }

        var topBar = root.Find("TopBar") as RectTransform;
        var body = root.Find("Body") as RectTransform;
        var bottomBar = root.Find("BottomBar") as RectTransform;

        if (topBar == null || body == null || bottomBar == null)
        {
            EditorUtility.DisplayDialog(
                "Update Work Dashboard to Floating",
                "Could not find TopBar, Body, or BottomBar. Make sure this is a Work Dashboard created by the builder.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update Work Dashboard to Floating");

        // Make root a transparent container
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null)
        {
            Undo.RecordObject(rootImg, "Update root");
            rootImg.color = new Color(0, 0, 0, 0);
            rootImg.raycastTarget = false;
        }

        // Remove root's VerticalLayoutGroup (we'll put one on FloatingPanel)
        var rootVlg = root.GetComponent<VerticalLayoutGroup>();
        if (rootVlg != null)
            Undo.DestroyObjectImmediate(rootVlg);

        // Create FloatingPanel — preserve current size
        var currentSize = root.rect.size;
        var floatingPanel = CreatePanel("FloatingPanel", root, new Color(0.10f, 0.11f, 0.13f, 0.98f));
        Undo.RecordObject(floatingPanel, "Setup FloatingPanel");
        floatingPanel.anchorMin = new Vector2(0.5f, 0.5f);
        floatingPanel.anchorMax = new Vector2(0.5f, 0.5f);
        floatingPanel.pivot = new Vector2(0.5f, 0.5f);
        floatingPanel.anchoredPosition = Vector2.zero;
        floatingPanel.sizeDelta = currentSize;

        var vlg = Undo.AddComponent<VerticalLayoutGroup>(floatingPanel.gameObject);
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 0;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // Reparent TopBar, Body, BottomBar into FloatingPanel (preserves references)
        Undo.SetTransformParent(topBar, floatingPanel, "Reparent TopBar");
        topBar.SetSiblingIndex(0);
        Undo.SetTransformParent(body, floatingPanel, "Reparent Body");
        body.SetSiblingIndex(1);
        Undo.SetTransformParent(bottomBar, floatingPanel, "Reparent BottomBar");
        bottomBar.SetSiblingIndex(2);

        // Add DragPanel to TopBar
        if (topBar.GetComponent<DragPanel>() == null)
        {
            var dragPanel = Undo.AddComponent<DragPanel>(topBar.gameObject);
            var so = new SerializedObject(dragPanel);
            so.FindProperty("target").objectReferenceValue = floatingPanel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.CollapseUndoOperations(group);
        EditorUtility.DisplayDialog(
            "Update Work Dashboard to Floating",
            "Dashboard updated. The window is now draggable by the title bar.",
            "OK");
    }

    private static RectTransform GetSelectedWorkDashboardRoot()
    {
        var go = Selection.activeGameObject;
        if (go == null) return null;
        var rt = go.transform as RectTransform;
        if (rt == null) return null;
        if (go.name == "WorkDashboardWindow") return rt;
        var parent = go.transform.parent;
        while (parent != null)
        {
            if (parent.name == "WorkDashboardWindow")
                return parent as RectTransform;
            parent = parent.parent;
        }
        return null;
    }

    private static void BuildProfileCard(RectTransform parent)
    {
        var card = CreatePanel("ProfileCard", parent, new Color(0.14f, 0.15f, 0.18f, 1f));
        var h = Undo.AddComponent<HorizontalLayoutGroup>(card.gameObject);
        h.padding = new RectOffset(10, 10, 10, 10);
        h.spacing = 10;
        h.childAlignment = TextAnchor.UpperLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var avatar = CreateImage("Avatar", card, new Color(0.25f, 0.26f, 0.30f, 1f));
        SetSize(avatar.rectTransform, 64, 64);

        var nameStack = CreateEmpty("NameStack", card);
        SetFlexibleWidth(nameStack, 1);
        var v = Undo.AddComponent<VerticalLayoutGroup>(nameStack.gameObject);
        v.padding = new RectOffset(0, 0, 0, 0);
        v.spacing = 4;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        CreateTMP("UsernameText", nameStack, "@username", 20, TextAlignmentOptions.MidlineLeft);
        CreateTMP("DisplayNameText", nameStack, "Display Name", 18, TextAlignmentOptions.MidlineLeft);
        CreateTMP("AccountAgeText", nameStack, "Account age: 2y", 16, TextAlignmentOptions.MidlineLeft);
    }

    private static void BuildStatsCard(RectTransform parent)
    {
        var card = CreatePanel("StatsCard", parent, new Color(0.14f, 0.15f, 0.18f, 1f));
        var v = Undo.AddComponent<VerticalLayoutGroup>(card.gameObject);
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        CreateTMP("FollowersText", card, "Followers: 12,400", 16, TextAlignmentOptions.MidlineLeft);
        CreateTMP("FollowingText", card, "Following: 310", 16, TextAlignmentOptions.MidlineLeft);
        CreateTMP("StrikesText", card, "Strikes: 1", 16, TextAlignmentOptions.MidlineLeft);
        CreateTMP("ReputationText", card, "Reputation: Neutral", 16, TextAlignmentOptions.MidlineLeft);
        CreateTMP("RiskText", card, "Risk: Medium", 16, TextAlignmentOptions.MidlineLeft);
    }

    private static void BuildHistoryCard(RectTransform parent)
    {
        var card = CreatePanel("HistoryCard", parent, new Color(0.14f, 0.15f, 0.18f, 1f));
        SetPreferredHeight(card, 190);

        var v = Undo.AddComponent<VerticalLayoutGroup>(card.gameObject);
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 8;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = true;

        CreateTMP("HistoryTitle", card, "Recent Activity", 16, TextAlignmentOptions.MidlineLeft);

        var scroll = CreateEmpty("HistoryScroll", card);
        SetFlexibleHeight(scroll, 1);
        var scrollRect = Undo.AddComponent<ScrollRect>(scroll.gameObject);
        scrollRect.horizontal = false;

        var viewport = CreatePanel("Viewport", scroll, new Color(0, 0, 0, 0));
        var mask = Undo.AddComponent<Mask>(viewport.gameObject);
        mask.showMaskGraphic = false;
        var viewportImage = viewport.GetComponent<Image>();
        viewportImage.raycastTarget = true;
        scrollRect.viewport = viewport;

        var content = CreateEmpty("Content", viewport);
        scrollRect.content = content;

        var contentV = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        contentV.padding = new RectOffset(0, 0, 0, 0);
        contentV.spacing = 6;
        contentV.childAlignment = TextAnchor.UpperLeft;
        contentV.childControlWidth = true;
        contentV.childControlHeight = true;
        contentV.childForceExpandWidth = true;
        contentV.childForceExpandHeight = false;

        var fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < 8; i++)
            CreateTMP($"HistoryItem_{i + 1}", content, "• Shared a post about breaking news", 14, TextAlignmentOptions.MidlineLeft);

        Stretch(viewport);
        Stretch(content);
    }

    private static void BuildQueueRow(RectTransform parent)
    {
        var row = CreateEmpty("QueueRow", parent);
        var h = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        h.padding = new RectOffset(0, 0, 0, 0);
        h.spacing = 10;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var queue = CreateTMP("QueueText", row, "Queue: 1 / 10", 16, TextAlignmentOptions.MidlineLeft);
        SetFlexibleWidth(queue.rectTransform, 1);

        CreateTMP("PolicyTag", row, "Policy: v1", 14, TextAlignmentOptions.MidlineRight);
        CreateTMP("SeverityTag", row, "Severity: —", 14, TextAlignmentOptions.MidlineRight);
    }

    private static void BuildPostCard(RectTransform parent)
    {
        var card = CreatePanel("PostCard", parent, new Color(0.14f, 0.15f, 0.18f, 1f));
        SetFlexibleHeight(card, 1);

        var v = Undo.AddComponent<VerticalLayoutGroup>(card.gameObject);
        v.padding = new RectOffset(12, 12, 12, 12);
        v.spacing = 10;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var header = CreateEmpty("PostHeader", card);
        var h = Undo.AddComponent<HorizontalLayoutGroup>(header.gameObject);
        h.padding = new RectOffset(0, 0, 0, 0);
        h.spacing = 10;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var user = CreateTMP("PostUserText", header, "@postername", 18, TextAlignmentOptions.MidlineLeft);
        SetFlexibleWidth(user.rectTransform, 1);
        CreateTMP("TimestampText", header, "2h", 14, TextAlignmentOptions.MidlineRight);

        var postText = CreateTMP("PostText", card,
            "Post content goes here. This area should wrap and feel like a real social feed post.",
            18,
            TextAlignmentOptions.TopLeft);
        postText.textWrappingMode = TMPro.TextWrappingModes.Normal;
        SetPreferredHeight(postText.rectTransform, 140);

        var media = CreatePanel("MediaContainer", card, new Color(0.10f, 0.10f, 0.12f, 1f));
        SetPreferredHeight(media, 220);
        var mediaLabel = CreateTMP("MediaLabel", media, "MEDIA (optional)", 16, TextAlignmentOptions.Center);
        Stretch(mediaLabel.rectTransform);

        CreateTMP("EngagementRow", card, "Likes 1.2k  •  Shares 340  •  Comments 88", 14, TextAlignmentOptions.MidlineLeft);
    }

    private static void BuildFlagsRow(RectTransform parent)
    {
        var row = CreateEmpty("FlagsRow", parent);
        SetPreferredHeight(row, 34);

        var h = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        h.padding = new RectOffset(0, 0, 0, 0);
        h.spacing = 10;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        CreateToggle(row, "MisinformationToggle", "Misinformation");
        CreateToggle(row, "HarassmentToggle", "Harassment");
        CreateToggle(row, "HateSpeechToggle", "Hate Speech");
    }

    private static void CreateSectionHeader(RectTransform parent, string text)
    {
        var header = CreateEmpty("SectionHeader", parent);
        var v = Undo.AddComponent<VerticalLayoutGroup>(header.gameObject);
        v.padding = new RectOffset(0, 0, 0, 0);
        v.spacing = 8;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        CreateTMP("SectionTitle", header, text, 16, TextAlignmentOptions.MidlineLeft);

        var divider = CreateImage("Divider", header, new Color(1, 1, 1, 0.10f));
        SetPreferredHeight(divider.rectTransform, 2);
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;

        var btn = go.GetComponent<Button>();

        var text = CreateTMP("Label", go.GetComponent<RectTransform>(), label, 16, TextAlignmentOptions.Center);
        text.color = Color.white;
        Stretch(text.rectTransform);
        text.raycastTarget = false;

        return btn;
    }

    private static void CreateToggle(RectTransform parent, string name, string label)
    {
        var root = CreateEmpty(name, parent);
        SetPreferredHeight(root, 28);

        var bg = CreateImage("Background", root, new Color(0.18f, 0.19f, 0.22f, 1f));
        Stretch(bg.rectTransform);

        var toggle = Undo.AddComponent<Toggle>(root.gameObject);
        toggle.targetGraphic = bg;

        var check = CreateImage("Checkmark", bg.rectTransform, new Color(0.20f, 0.70f, 0.35f, 1f));
        var checkRt = check.rectTransform;
        checkRt.anchorMin = new Vector2(0, 0.5f);
        checkRt.anchorMax = new Vector2(0, 0.5f);
        checkRt.pivot = new Vector2(0, 0.5f);
        checkRt.sizeDelta = new Vector2(18, 18);
        checkRt.anchoredPosition = new Vector2(6, 0);
        toggle.graphic = check;

        var txt = CreateTMP("Text", root, label, 14, TextAlignmentOptions.MidlineLeft);
        txt.rectTransform.offsetMin = new Vector2(28, 0);
        txt.rectTransform.offsetMax = new Vector2(0, 0);
        Stretch(txt.rectTransform);
        txt.raycastTarget = false;
    }

    private static void BuildDecisionHistory(RectTransform parent)
    {
        var card = CreatePanel("DecisionHistory", parent, new Color(0.14f, 0.15f, 0.18f, 1f));
        SetPreferredHeight(card, 220);

        var v = Undo.AddComponent<VerticalLayoutGroup>(card.gameObject);
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 8;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = true;

        CreateTMP("DecisionHistoryTitle", card, "Decision History", 16, TextAlignmentOptions.MidlineLeft);

        var scroll = CreateEmpty("Scroll", card);
        SetFlexibleHeight(scroll, 1);
        var scrollRect = Undo.AddComponent<ScrollRect>(scroll.gameObject);
        scrollRect.horizontal = false;

        var viewport = CreatePanel("Viewport", scroll, new Color(0, 0, 0, 0));
        var mask = Undo.AddComponent<Mask>(viewport.gameObject);
        mask.showMaskGraphic = false;
        viewport.GetComponent<Image>().raycastTarget = true;
        scrollRect.viewport = viewport;

        var content = CreateEmpty("Content", viewport);
        scrollRect.content = content;

        var contentV = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        contentV.padding = new RectOffset(0, 0, 0, 0);
        contentV.spacing = 6;
        contentV.childAlignment = TextAnchor.UpperLeft;
        contentV.childControlWidth = true;
        contentV.childControlHeight = true;
        contentV.childForceExpandWidth = true;
        contentV.childForceExpandHeight = false;

        var fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Stretch(viewport);
        Stretch(content);
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

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, string text, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        tmp.raycastTarget = false;
        return tmp;
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

    private static void SetPreferredWidth(RectTransform rt, float width)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.preferredWidth = width;
        le.flexibleWidth = 0;
    }

    private static void SetFlexibleWidth(RectTransform rt, float flex)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.flexibleWidth = flex;
    }

    private static void SetFlexibleHeight(RectTransform rt, float flex)
    {
        var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
        le.flexibleHeight = flex;
    }

    private static void SetSize(RectTransform rt, float w, float h)
    {
        rt.sizeDelta = new Vector2(w, h);
    }
}

