using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds four corner drag-resize handles to a UI window RectTransform.
/// Attach to PaintAppWindow. No Update() polling — purely event-driven.
///
/// ROOT-CAUSE NOTES (why the original invisible-handle approach failed):
///   1. Unity's GraphicRaycaster skips Image components whose rendered alpha is 0,
///      regardless of raycastTarget=true. Handles with color=(0,0,0,0) were never hit.
///   2. eventData.Use() in OnPointerDown consumed the event and prevented the
///      EventSystem from starting a drag sequence for that pointer.
/// FIXES applied here:
///   1. Handle Image uses a very-low-opacity white so alpha > 0 and raycasts land.
///      This simultaneously gives the subtle corner indicator the design requires.
///   2. eventData.Use() removed from OnPointerDown.
///   3. Canvas is cached from the handle itself (more robust than from windowRect).
/// </summary>
public sealed class ResizableWindow : MonoBehaviour
{
    internal enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }

    [Header("Resize settings")]
    [Tooltip("The RectTransform whose sizeDelta will be changed. Defaults to this GameObject.")]
    [SerializeField] private RectTransform windowRect;

    [Tooltip("Minimum allowed window width in pixels.")]
    [SerializeField] private float minWidth = 400f;

    [Tooltip("Minimum allowed window height in pixels.")]
    [SerializeField] private float minHeight = 300f;

    [Tooltip("Size of the corner hit-area / visual indicator in pixels.")]
    [SerializeField] private float handleSize = 20f;

    // Subtle white — just visible enough to hint at resizability.
    // Alpha MUST be > 0 for Unity's GraphicRaycaster to register hits.
    private static readonly Color HandleColor = new Color(1f, 1f, 1f, 0.18f);

    private void Awake()
    {
        if (windowRect == null)
            windowRect = GetComponent<RectTransform>();

        CreateHandle(Corner.BottomLeft);
        CreateHandle(Corner.BottomRight);
        CreateHandle(Corner.TopLeft);
        CreateHandle(Corner.TopRight);
    }

    private void CreateHandle(Corner corner)
    {
        var go = new GameObject($"ResizeHandle_{corner}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CornerDragHandle));

        go.transform.SetParent(windowRect, false);
        go.transform.SetAsLastSibling(); // above FloatingPanel in draw order

        // FIX 1: alpha must be > 0 so GraphicRaycaster registers hits on this Image.
        // 0.18 alpha gives a barely-there white corner glow — functional + decorative.
        var img = go.GetComponent<Image>();
        img.color = HandleColor;
        img.raycastTarget = true;

        // Point-anchor the handle to its exact corner so it tracks during resize
        var rt = go.GetComponent<RectTransform>();
        Vector2 anchor = CornerAnchor(corner);
        rt.anchorMin         = anchor;
        rt.anchorMax         = anchor;
        rt.pivot             = anchor;
        rt.sizeDelta         = new Vector2(handleSize, handleSize);
        rt.anchoredPosition  = Vector2.zero;

        var handler = go.GetComponent<CornerDragHandle>();
        handler.Initialize(windowRect, corner, minWidth, minHeight);
    }

    private static Vector2 CornerAnchor(Corner corner) => corner switch
    {
        Corner.BottomLeft  => new Vector2(0f, 0f),
        Corner.BottomRight => new Vector2(1f, 0f),
        Corner.TopLeft     => new Vector2(0f, 1f),
        Corner.TopRight    => new Vector2(1f, 1f),
        _                  => Vector2.zero
    };
}

/// <summary>
/// Drag handler for one resize corner. Event-driven, no Update().
///
/// Math: PaintAppWindow has pivot (0.5, 0.5) + point anchor in parent.
///   Dragging a corner changes sizeDelta by (sign * delta).
///   Since the pivot is centered, this also shifts the geometric center by half
///   the size change, so anchoredPosition must move by (sign * delta / 2)
///   to keep the opposite corner stationary.
///
/// Screen-boundary clamping: after applying resize, world corners are checked
///   against 0–1920 (x) and 0–1080 (y). Any overshoot on the dragged edge
///   shrinks the size by that amount and compensates position to keep the
///   opposite edge stationary. No Update() loop — purely reactive in OnDrag.
/// </summary>
[RequireComponent(typeof(RectTransform))]
internal sealed class CornerDragHandle : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform _windowRect;
    private float         _minWidth;
    private float         _minHeight;
    private float         _signX;   // +1 = right edge,  -1 = left edge
    private float         _signY;   // +1 = top edge,    -1 = bottom edge
    private Canvas        _canvas;
    private Image         _image;

    // Game world bounds — matches the 1920x1080 camera setup
    private const float ScreenW = 1920f;
    private const float ScreenH = 1080f;

    // Visual feedback colors
    private static readonly Color NormalColor  = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color HoverColor   = new Color(1f, 1f, 1f, 0.45f);
    private static readonly Color ActiveColor  = new Color(1f, 1f, 1f, 0.65f);

    internal void Initialize(RectTransform windowRect,
                              ResizableWindow.Corner corner,
                              float minWidth, float minHeight)
    {
        _windowRect = windowRect;
        _minWidth   = minWidth;
        _minHeight  = minHeight;

        _signX = (corner == ResizableWindow.Corner.BottomRight ||
                  corner == ResizableWindow.Corner.TopRight)  ? 1f : -1f;
        _signY = (corner == ResizableWindow.Corner.TopLeft    ||
                  corner == ResizableWindow.Corner.TopRight)  ? 1f : -1f;
    }

    private void Start()
    {
        // FIX 3: cache Canvas from this handle's own hierarchy — more reliable
        // than walking from windowRect, and works correctly in all nesting cases.
        _canvas = GetComponentInParent<Canvas>();
        _image  = GetComponent<Image>();
    }

    // FIX 2: Do NOT call eventData.Use() here.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_windowRect != null)
            _windowRect.SetAsLastSibling();

        if (_image != null)
            _image.color = ActiveColor;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_windowRect == null || _canvas == null) return;

        float scaleFactor = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        Vector2 delta = eventData.delta / scaleFactor;

        Vector2 size = _windowRect.sizeDelta;

        float newW = size.x + _signX * delta.x;
        float newH = size.y + _signY * delta.y;

        float clampedW = Mathf.Max(_minWidth,  newW);
        float clampedH = Mathf.Max(_minHeight, newH);

        // Actual applied delta after min-size clamping
        float actualDX = (clampedW - size.x) * _signX;
        float actualDY = (clampedH - size.y) * _signY;

        _windowRect.sizeDelta        = new Vector2(clampedW, clampedH);
        _windowRect.anchoredPosition += new Vector2(actualDX * 0.5f, actualDY * 0.5f);

        // ── Screen-boundary clamp ─────────────────────────────────────────────
        // GetWorldCorners order: 0=BottomLeft, 1=TopLeft, 2=TopRight, 3=BottomRight
        var wc = new Vector3[4];
        _windowRect.GetWorldCorners(wc);

        // Measure how far the dragged edge has gone past the screen boundary.
        // overX/overY are positive magnitudes; sign tells us which edge.
        float overX = _signX > 0f
            ? Mathf.Max(0f, wc[2].x - ScreenW)   // right edge past 1920
            : Mathf.Max(0f, -wc[0].x);            // left  edge past 0

        float overY = _signY > 0f
            ? Mathf.Max(0f, wc[1].y - ScreenH)   // top   edge past 1080
            : Mathf.Max(0f, -wc[0].y);            // bottom edge past 0

        // Shrink the dragged edge back by the overshoot amount and move
        // the center toward the dragged edge so the OPPOSITE edge stays put.
        if (overX > 0f)
        {
            float shrink = overX;
            _windowRect.sizeDelta = new Vector2(
                Mathf.Max(_minWidth, _windowRect.sizeDelta.x - shrink),
                _windowRect.sizeDelta.y);
            _windowRect.anchoredPosition -= new Vector2(_signX * shrink * 0.5f, 0f);
        }
        if (overY > 0f)
        {
            float shrink = overY;
            _windowRect.sizeDelta = new Vector2(
                _windowRect.sizeDelta.x,
                Mathf.Max(_minHeight, _windowRect.sizeDelta.y - shrink));
            _windowRect.anchoredPosition -= new Vector2(0f, _signY * shrink * 0.5f);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_image != null)
            _image.color = NormalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_image != null)
            _image.color = HoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_image != null)
            _image.color = NormalColor;
    }
}
