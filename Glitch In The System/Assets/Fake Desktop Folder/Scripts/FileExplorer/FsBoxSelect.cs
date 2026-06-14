using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Batch 5 — Box / drag-selection rectangle for File Explorer.
///
/// Attach to FileScrollView (the full content area).
/// Draws a semi-transparent selection rectangle while the user drags on empty space.
/// On release, selects all FsItemView rows whose screen rect intersects the box.
///
/// INPUT CONFLICT HANDLING:
///   • Only activates when FsItemView.IsDragging == false (file drag takes full priority).
///   • Only responds to LEFT mouse button.
///   • PointerDown on a file row is consumed by FsItemView first (higher sibling order),
///     so the box-select never starts when a file row is the hit target.
///   • Right-click is completely ignored.
///   • ESC cancels the box without changing selection.
///   • No per-frame allocations: all containers are pre-allocated.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class FsBoxSelect : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // ── Injected by FileExplorerApp ───────────────────────────────────────
    // These are set once after construction — no Inspector serialization needed.
    [System.NonSerialized] public List<FsItemView>  Items;       // live reference to _items
    /// <summary>Fires on release. bool = true when Ctrl held (additive selection).</summary>
    [System.NonSerialized] public System.Action<List<FsItemView>, bool> OnBoxSelection;

    // ── Internal state ────────────────────────────────────────────────────
    private bool          _active;
    private Vector2       _startScreen;   // screen-space start point
    private Vector2       _currentScreen; // screen-space current drag point

    // ── Visual ────────────────────────────────────────────────────────────
    private RectTransform _boxRT;   // the selection rect visual
    private Image         _boxImg;
    private Canvas        _rootCanvas;
    private RectTransform _rootCanvasRT;

    // Intersection scratch — reused every frame, no alloc
    private readonly List<FsItemView> _inside = new(32);

    // ── Visual constants ──────────────────────────────────────────────────
    private static readonly Color BoxFill   = new Color(0.28f, 0.52f, 0.88f, 0.12f);
    private static readonly Color BoxBorder = new Color(0.35f, 0.60f, 1.00f, 0.75f);

    // ── Init ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        _rootCanvas   = GetComponentInParent<Canvas>();
        _rootCanvasRT = _rootCanvas != null ? _rootCanvas.transform as RectTransform : null;

        BuildBoxVisual();
    }

    private void BuildBoxVisual()
    {
        // Box fill
        var go = new GameObject("__BoxSelectRect",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_rootCanvasRT ?? transform, false);

        _boxRT = go.GetComponent<RectTransform>();
        // Pivot bottom-left so anchoredPosition = bottom-left corner of box
        _boxRT.anchorMin = _boxRT.anchorMax = new Vector2(0.5f, 0.5f);
        _boxRT.pivot     = new Vector2(0f, 0f);
        _boxRT.sizeDelta = Vector2.zero;

        _boxImg       = go.GetComponent<Image>();
        _boxImg.color = BoxFill;
        _boxImg.raycastTarget = false;

        // Border via Outline effect
        var outline = go.AddComponent<Outline>();
        outline.effectColor    = BoxBorder;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        go.SetActive(false);
    }

    // ── Pointer events ────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData e)
    {
        // Only left-click on empty space (not on a file row — those consume the event first)
        if (e.button != PointerEventData.InputButton.Left) return;
        if (FsItemView.IsDragging) return; // file drag already in progress

        _startScreen   = e.position;
        _currentScreen = e.position;
        _active        = true;

        UpdateBoxVisual();
        if (_boxRT != null) _boxRT.gameObject.SetActive(true);
        if (_boxRT != null) _boxRT.transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_active) return;
        if (FsItemView.IsDragging) { Cancel(); return; } // abort if file drag started

        _currentScreen = e.position;
        UpdateBoxVisual();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_active) return;
        if (e.button != PointerEventData.InputButton.Left) return;

        _active = false;
        if (_boxRT != null) _boxRT.gameObject.SetActive(false);

        CommitSelection();
    }

    // ── ESC cancel ────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_active) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Cancel();
    }

    private void Cancel()
    {
        _active = false;
        if (_boxRT != null) _boxRT.gameObject.SetActive(false);
    }

    // ── Box visual ────────────────────────────────────────────────────────
    private void UpdateBoxVisual()
    {
        if (_boxRT == null || _rootCanvasRT == null) return;

        // Convert both corners to canvas-local space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvasRT, _startScreen,   _rootCanvas.worldCamera, out var a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvasRT, _currentScreen, _rootCanvas.worldCamera, out var b);

        float left   = Mathf.Min(a.x, b.x);
        float bottom = Mathf.Min(a.y, b.y);
        float w      = Mathf.Abs(b.x - a.x);
        float h      = Mathf.Abs(b.y - a.y);

        // Pivot = (0,0) = bottom-left, so anchoredPosition is the bottom-left corner
        _boxRT.anchoredPosition = new Vector2(left, bottom);
        _boxRT.sizeDelta        = new Vector2(w, h);
    }

    // ── Commit selection ──────────────────────────────────────────────────
    private void CommitSelection()
    {
        if (Items == null || Items.Count == 0) return;

        // Build screen-space selection rect from the two corners
        float minX = Mathf.Min(_startScreen.x, _currentScreen.x);
        float maxX = Mathf.Max(_startScreen.x, _currentScreen.x);
        float minY = Mathf.Min(_startScreen.y, _currentScreen.y);
        float maxY = Mathf.Max(_startScreen.y, _currentScreen.y);

        // Ignore tiny box (accidental click-drag)
        if ((maxX - minX) < 4f && (maxY - minY) < 4f) return;

        var selectRect = new Rect(minX, minY, maxX - minX, maxY - minY);

        _inside.Clear();
        foreach (var view in Items)
        {
            if (view == null || !view.gameObject.activeSelf) continue;
            var rowRT = view.GetComponent<RectTransform>();
            if (rowRT == null) continue;

            // Get the row's screen-space corners
            var corners = new Vector3[4];
            rowRT.GetWorldCorners(corners);

            // In Screen Space Overlay, world corners == screen pixels
            float rMinX = Mathf.Min(corners[0].x, corners[2].x);
            float rMaxX = Mathf.Max(corners[0].x, corners[2].x);
            float rMinY = Mathf.Min(corners[0].y, corners[2].y);
            float rMaxY = Mathf.Max(corners[0].y, corners[2].y);
            var   rowRect = new Rect(rMinX, rMinY, rMaxX - rMinX, rMaxY - rMinY);

            if (selectRect.Overlaps(rowRect))
                _inside.Add(view);
        }

        if (_inside.Count > 0)
        {
            bool additive = Keyboard.current != null &&
                            (Keyboard.current.leftCtrlKey.isPressed ||
                             Keyboard.current.rightCtrlKey.isPressed);
            OnBoxSelection?.Invoke(_inside, additive);
        }
    }

    private void OnDestroy()
    {
        if (_boxRT != null)
            Destroy(_boxRT.gameObject);
    }
}
