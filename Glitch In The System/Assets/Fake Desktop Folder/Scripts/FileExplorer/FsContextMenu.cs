using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Runtime-built right-click context menu for File Explorer.
/// Audit fixes:
///   - BuildPanel() is now guarded — called only once, not every OnEnable
///   - Context menu position clamped to window bounds (no off-screen overflow)
///   - [CRITICAL-3] Update() polling removed — replaced with a fullscreen transparent
///     backdrop that catches outside clicks via IPointerClickHandler. Zero per-frame
///     cost whether the menu is open or closed.
/// </summary>
public sealed class FsContextMenu : MonoBehaviour
{
    private RectTransform _panel;
    private RectTransform _backdrop;
    private Canvas        _canvas;
    private RectTransform _windowRect; // for boundary clamping
    private bool          _open;
    private bool          _panelBuilt; // guard against rebuilding on every Init() call

    private readonly List<(string label, Action action)> _items = new();

    // ── Init (called by FileExplorerApp.OnEnable) ─────────────────────────
    // Guarded — only builds panel once. Safe to call every OnEnable.
    public void Init(Canvas canvas)
    {
        _canvas     = canvas;
        _windowRect = GetComponentInParent<RectTransform>();

        if (!_panelBuilt)
        {
            BuildPanel();
            _panelBuilt = true;
        }

        Hide();
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void ShowAt(Vector2 screenPos, List<(string label, Action action)> menuItems)
    {
        if (_panel == null || _canvas == null) return;

        _items.Clear();
        _items.AddRange(menuItems);
        RebuildMenuItems();

        // Convert screen → canvas local
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            screenPos, _canvas.worldCamera, out var local);

        _panel.anchoredPosition = local;

        // Backdrop behind panel — catches all outside clicks
        _backdrop.gameObject.SetActive(true);
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling(); // panel always renders above backdrop
        _open = true;

        ClampPanelToWindow();
    }

    public void Hide()
    {
        if (_panel    != null) _panel.gameObject.SetActive(false);
        if (_backdrop != null) _backdrop.gameObject.SetActive(false);
        _open = false;
    }

    public bool IsOpen => _open;

    // ── Screen boundary clamping ──────────────────────────────────────────
    private void ClampPanelToWindow()
    {
        if (_windowRect == null || _panel == null) return;

        Canvas.ForceUpdateCanvases();

        var panelPos  = _panel.anchoredPosition;
        var panelSize = _panel.rect.size;
        var winSize   = _windowRect.rect.size;

        float rightEdge = panelPos.x + panelSize.x;
        if (rightEdge > winSize.x * 0.5f)
            panelPos.x -= panelSize.x;

        float bottomEdge = panelPos.y - panelSize.y;
        if (bottomEdge < -winSize.y * 0.5f)
            panelPos.y += panelSize.y;

        _panel.anchoredPosition = panelPos;
    }

    // ── Build ─────────────────────────────────────────────────────────────
    private void BuildPanel()
    {
        // ── Backdrop (built first = lower sibling = renders behind panel) ─
        // Fills the FsContextMenu rect (which is stretched to fill the canvas).
        // Invisible but raycast-blocking. Catches any click outside the panel
        // and calls Hide() — replaces Update() polling entirely.
        var bdGO = new GameObject("FsContextMenuBackdrop",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(ContextMenuBackdrop));
        bdGO.transform.SetParent(transform, false);

        _backdrop            = bdGO.GetComponent<RectTransform>();
        _backdrop.anchorMin  = Vector2.zero;
        _backdrop.anchorMax  = Vector2.one;
        _backdrop.offsetMin  = Vector2.zero;
        _backdrop.offsetMax  = Vector2.zero;

        var bdImg = bdGO.GetComponent<Image>();
        bdImg.color          = Color.clear; // fully transparent
        bdImg.raycastTarget  = true;        // must be true to catch clicks

        bdGO.GetComponent<ContextMenuBackdrop>().OnOutsideClick = Hide;

        // ── Panel ─────────────────────────────────────────────────────────
        var go = new GameObject("FsContextMenuPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(transform, false);

        _panel           = go.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.pivot     = new Vector2(0f, 1f);
        _panel.sizeDelta = new Vector2(160f, 0f);

        var img   = go.GetComponent<Image>();
        img.color = new Color(0.13f, 0.12f, 0.11f, 0.97f);

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(3, 3, 3, 3);
        vlg.spacing              = 1;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = go.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Stops clicks on panel items from falling through to the backdrop
        go.AddComponent<ContextMenuClickBlocker>();
    }

    private void RebuildMenuItems()
    {
        for (int i = _panel.childCount - 1; i >= 0; i--)
            Destroy(_panel.GetChild(i).gameObject);

        foreach (var item in _items)
        {
            if (item.label == "---") { BuildSeparator(); continue; }
            var action = item.action;
            BuildMenuItem(item.label, () => { Hide(); action?.Invoke(); });
        }
    }

    private void BuildMenuItem(string label, Action onClick)
    {
        var go = new GameObject("MI_" + label,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_panel, false);
        go.GetComponent<LayoutElement>().preferredHeight = 28f;

        var img   = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);

        var btn    = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.13f);
        colors.pressedColor     = new Color(1f, 1f, 1f, 0.22f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick());

        var lblGO = new GameObject("L",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var rt = lblGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 0f); rt.offsetMax = new Vector2(-6f, 0f);
        var tmp   = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 12;
        tmp.color         = new Color(0.90f, 0.88f, 0.84f, 1f);
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
    }

    private void BuildSeparator()
    {
        var go = new GameObject("Sep",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(_panel, false);
        go.GetComponent<LayoutElement>().preferredHeight = 5f;
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
    }
}

/// <summary>
/// Fullscreen transparent backdrop behind the context menu panel.
/// Catches any click outside the panel and fires the Hide callback.
/// Built once in BuildPanel(); activated/deactivated with the menu.
/// </summary>
internal sealed class ContextMenuBackdrop : MonoBehaviour, IPointerClickHandler
{
    internal Action OnOutsideClick;
    public void OnPointerClick(PointerEventData e) => OnOutsideClick?.Invoke();
}

/// <summary>
/// Attached to the panel itself. Stops pointer clicks on menu items from
/// propagating down to the backdrop, preventing an immediate self-close.
/// </summary>
internal sealed class ContextMenuClickBlocker : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData e) { }
}
