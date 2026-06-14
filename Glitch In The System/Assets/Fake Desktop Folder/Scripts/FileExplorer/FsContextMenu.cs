using System;
using System.Collections;
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
    private bool          _panelBuilt;
    private CanvasGroup   _panelCG;    // for open animation (alpha)
    private Coroutine     _openAnim;   // stop/restart guard
    private Vector2       _cursorLocal;

    private readonly List<(string label, Action action)> _items = new();

    // Pooled item rows — reused across ShowAt calls to avoid Destroy/Instantiate churn.
    // Index matches _items order; separators and regular items share the pool.
    private readonly List<GameObject> _itemPool = new(8);

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

        // Convert screen position → FsContextMenu local space.
        // _windowRect is this GO's own RT (full-stretch inside the window).
        // Its local space has origin at the RT pivot (0.5,0.5 = centre of window).
        // Use _panel.parent RT — the exact space _panel.anchoredPosition lives in.
        // Using any other RT (_windowRect, canvas root, etc.) causes coordinate offset.
        var panelParentRT = _panel.parent as RectTransform;
        if (panelParentRT == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelParentRT, screenPos, _canvas.worldCamera, out var local);

        // Windows-like cursor offset: open 6px right, 4px below cursor
        // The panel anchor/pivot is (0,1) = top-left corner.
        // In this local space: +x is right, +y is up.
        // Cursor point is where we want the top-left of the menu to appear.
        _cursorLocal = local;
        _panel.anchoredPosition = local;
        _backdrop.gameObject.SetActive(true);
        _panel.gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _panel.SetAsLastSibling();
        _open = true;
        ClampPanelToWindow();

        // Stop any in-progress open animation before starting a new one.
        if (_openAnim != null) { StopCoroutine(_openAnim); _openAnim = null; }
        if (_panelCG != null) _openAnim = StartCoroutine(PlayOpenAnimation());
    }

    public void Hide()
    {
        if (_panel    != null) _panel.gameObject.SetActive(false);
        if (_backdrop != null) _backdrop.gameObject.SetActive(false);
        _open = false;
    }

    public bool IsOpen => _open;

    // ── Open animation ────────────────────────────────────────────────────
    // Subtle ease-out scale (0.97→1) + alpha (0→1) over 0.10 s.
    // Uses unscaled time — safe during paused/scaled game time.
    // No Update() — coroutine fires once per ShowAt() call.
    private IEnumerator PlayOpenAnimation()
    {
        if (_panelCG == null || _panel == null) yield break;

        const float Duration = 0.10f;
        var   startScale     = new Vector3(0.97f, 0.97f, 1f);
        float elapsed        = 0f;

        _panelCG.alpha    = 0f;
        _panel.localScale = startScale;

        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / Duration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad

            _panelCG.alpha    = eased;
            _panel.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, eased);
            yield return null;
        }

        _panelCG.alpha    = 1f;
        _panel.localScale = Vector3.one;
        _openAnim         = null;
    }

    // ── Screen boundary clamping ──────────────────────────────────────────
    // Panel constants — must match BuildPanel() and BuildMenuItem()/BuildSeparator().
    private const float PanelWidth    = 160f;
    private const float ItemHeight    = 28f;
    private const float SepHeight     = 9f;
    private const float PanelPadding  = 6f;  // VLG top+bottom padding (3+3)

    private void ClampPanelToWindow()
    {
        if (_windowRect == null || _panel == null) return;

        float panelHeight = PanelPadding;
        foreach (var item in _items)
            panelHeight += item.label == "---" ? SepHeight : ItemHeight;
        if (_items.Count > 1) panelHeight += _items.Count - 1;

        float halfW = _windowRect.rect.width  * 0.5f;
        float halfH = _windowRect.rect.height * 0.5f;
        const float safetyPad = 2f;

        // 1px from cursor — almost touching
        float posX = _cursorLocal.x + 1f;
        float posY = _cursorLocal.y - 1f;

        // Flip around cursor before placing
        if (posX + PanelWidth > halfW - safetyPad)
            posX = _cursorLocal.x - PanelWidth - 1f;

        if (posY - panelHeight < -halfH + safetyPad)
            posY = _cursorLocal.y + panelHeight + 1f;

        // Safety clamp
        posX = Mathf.Clamp(posX, -halfW + safetyPad, halfW - PanelWidth - safetyPad);
        posY = Mathf.Clamp(posY, -halfH + panelHeight + safetyPad, halfH - safetyPad);

        _panel.anchoredPosition = new Vector2(posX, posY);
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
        // anchor=(0.5,0.5): anchoredPosition lives in center-relative space,
        // matching ScreenPointToLocalPointInRectangle output. pivot=(0,1) keeps
        // the top-left of the panel at the cursor point (menu opens down-right).
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
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
        // CanvasGroup drives the open animation (alpha); blockRaycasts stays true.
        _panelCG = go.AddComponent<CanvasGroup>();
        _panelCG.alpha = 1f;
    }

    private void RebuildMenuItems()
    {
        // Reuse pooled GOs instead of Destroy+Instantiate on every ShowAt.
        // Grow pool only when needed; hide surplus items.
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (i < _itemPool.Count)
            {
                // Reuse existing row — update content in place
                UpdatePooledItem(_itemPool[i], item.label, item.action);
                _itemPool[i].SetActive(true);
            }
            else
            {
                // Pool is smaller than needed — build a new row and add it
                GameObject go = item.label == "---"
                    ? BuildSeparator()
                    : BuildMenuItem(item.label, null); // action wired by UpdatePooledItem
                _itemPool.Add(go);
                UpdatePooledItem(go, item.label, item.action);
            }
        }
        // Hide unused pool rows (don't destroy — keep for next open)
        for (int i = _items.Count; i < _itemPool.Count; i++)
            _itemPool[i].SetActive(false);
    }

    private void UpdatePooledItem(GameObject go, string label, Action action)
    {
        bool isSep = label == "---";
        // Separators and menu items have different structures — swap if type changed
        bool wasSep = go.GetComponent<Button>() == null;
        if (isSep != wasSep)
        {
            // Type mismatch (rare — only if menu structure changes between opens).
            // Destroy and rebuild this slot; replace in pool.
            int idx = _itemPool.IndexOf(go);
            Destroy(go);
            var rebuilt = isSep ? BuildSeparator() : BuildMenuItem(label, null);
            if (idx >= 0 && idx < _itemPool.Count) _itemPool[idx] = rebuilt;
            go = rebuilt;
        }

        if (isSep) return;

        // FIX-3: reset hover visuals before reuse — OnPointerExit won't fire on reopen
        var hoverComp = go.GetComponent<ContextMenuItemHover>();
        if (hoverComp != null) hoverComp.ResetVisuals();

        // Update label
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) tmp.text = label;

        // Re-wire button — clear old listener, add new
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            if (action != null)
            {
                var capturedAction = action;
                btn.onClick.AddListener(() => { Hide(); capturedAction.Invoke(); });
            }
        }
    }

    private GameObject BuildMenuItem(string label, Action onClick)
    {
        var go = new GameObject("MI_" + label,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_panel, false);
        go.GetComponent<LayoutElement>().preferredHeight = 28f;

        var img   = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);

        var btn    = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        go.AddComponent<ContextMenuItemHover>();
        // HOVER_INJECT_DONE

        var lblGO = new GameObject("L",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var rt = lblGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(28f, 0f); rt.offsetMax = new Vector2(-8f, 0f);
        var tmp   = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 12;
        tmp.color         = new Color(0.90f, 0.88f, 0.84f, 1f);
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;

        return go;
    }

    private GameObject BuildSeparator()
    {
        var go = new GameObject("Sep",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(_panel, false);
        go.GetComponent<LayoutElement>().preferredHeight = 9f;
        // Lower opacity + slightly warmer tone — Windows-like thin separator feel
        go.GetComponent<Image>().color = new Color(0.55f, 0.53f, 0.50f, 0.14f);
        return go;
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

/// <summary>Windows-style hover for context menu items.</summary>
internal sealed class ContextMenuItemHover : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler,
    UnityEngine.EventSystems.IPointerUpHandler
{
    private static readonly Color BgHover   = new Color(0.22f, 0.40f, 0.72f, 0.30f);
    private static readonly Color TxtHover  = new Color(0.95f, 0.97f, 1.00f, 1f);
    private static readonly Color TxtNormal = new Color(0.90f, 0.88f, 0.84f, 1f);
    private static readonly Color BgPressed  = new Color(0.16f, 0.30f, 0.56f, 0.45f);
    private static readonly Color TxtPressed = Color.white;

    private UnityEngine.UI.Image    _bg;
    private TMPro.TextMeshProUGUI   _lbl;

    private void Awake()
    {
        _bg  = GetComponent<UnityEngine.UI.Image>();
        _lbl = GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        if (_bg  != null) _bg.color  = BgHover;
        if (_lbl != null) _lbl.color = TxtHover;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => ResetVisuals();

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
    {
        if (_bg  != null) _bg.color  = BgPressed;
        if (_lbl != null) _lbl.color = TxtPressed;
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e)
    {
        // Revert to hover (pointer still over item after mouse-up)
        if (_bg  != null) _bg.color  = BgHover;
        if (_lbl != null) _lbl.color = TxtHover;
    }

    /// <summary>FIX-3: explicit reset — called by UpdatePooledItem on reuse.</summary>
    public void ResetVisuals()
    {
        if (_bg  != null) _bg.color  = new Color(1f,1f,1f,0f);
        if (_lbl != null) _lbl.color = TxtNormal;
    }
}
