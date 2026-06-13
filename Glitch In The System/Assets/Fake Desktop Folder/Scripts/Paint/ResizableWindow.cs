using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds eight resize handles to a UI window RectTransform:
///   Four corners  (diagonal resize)  — CornerDragHandle
///   Four edges    (single-axis resize) — EdgeDragHandle
///
/// All handles are invisible hitboxes (alpha>0 required for GraphicRaycaster).
/// Visual accent shown only on hover/active via a non-raycast child Image.
///
/// ROOT-CAUSE NOTES (why invisible-handle approach originally failed):
///   1. Unity's GraphicRaycaster skips alpha=0 Images — handles need alpha > 0.
///   2. eventData.Use() in OnPointerDown prevented drag sequences.
/// Both fixed: handles use alpha=0.004 (above threshold), Use() removed.
/// </summary>
public sealed class ResizableWindow : MonoBehaviour
{
    internal enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }
    internal enum Edge   { Left, Right, Bottom, Top }

    [Header("Resize settings")]
    [Tooltip("The RectTransform whose sizeDelta will be changed. Defaults to this GameObject.")]
    [SerializeField] internal RectTransform windowRect;

    [Tooltip("Minimum allowed window width in pixels.")]
    [SerializeField] internal float minWidth  = 400f;

    [Tooltip("Minimum allowed window height in pixels.")]
    [SerializeField] internal float minHeight = 300f;

    [Tooltip("Corner hit-area size in pixels.")]
    [SerializeField] private float cornerSize = 20f;

    [Tooltip("Edge hit-area thickness in pixels.")]
    [SerializeField] private float edgeThickness = 8f;

    // Minimum alpha for GraphicRaycaster to register — just above zero
    private static readonly Color HitboxColor = new Color(1f, 1f, 1f, 0.004f);

    private void Awake()
    {
        if (windowRect == null)
            windowRect = GetComponent<RectTransform>();

        // Four corners (diagonal)
        CreateCorner(Corner.BottomLeft);
        CreateCorner(Corner.BottomRight);
        CreateCorner(Corner.TopLeft);
        CreateCorner(Corner.TopRight);

        // Four edges (single-axis)
        CreateEdge(Edge.Left);
        CreateEdge(Edge.Right);
        CreateEdge(Edge.Bottom);
        CreateEdge(Edge.Top);
    }

    // ── Corner handles ────────────────────────────────────────────────────
    private void CreateCorner(Corner corner)
    {
        var go = new GameObject($"ResizeHandle_{corner}",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement), typeof(CornerDragHandle));
        go.transform.SetParent(windowRect, false);
        go.transform.SetAsLastSibling();
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        SetupHitboxImage(go.GetComponent<Image>());

        Vector2 anchor = CornerAnchor(corner);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = anchor;
        rt.sizeDelta        = new Vector2(cornerSize, cornerSize);
        rt.anchoredPosition = Vector2.zero;

        go.GetComponent<CornerDragHandle>().Initialize(this, corner);
    }

    // ── Edge handles ──────────────────────────────────────────────────────
    private void CreateEdge(Edge edge)
    {
        var go = new GameObject($"ResizeHandle_{edge}",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement), typeof(EdgeDragHandle));
        go.transform.SetParent(windowRect, false);
        go.transform.SetAsLastSibling();
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        SetupHitboxImage(go.GetComponent<Image>());

        var rt = go.GetComponent<RectTransform>();
        SetEdgeRect(rt, edge);

        go.GetComponent<EdgeDragHandle>().Initialize(this, edge);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SetupHitboxImage(Image img)
    {
        img.color         = HitboxColor; // > 0 so GraphicRaycaster registers hits
        img.raycastTarget = true;
    }

    /// <summary>
    /// Edge handle anchors: stretches along one axis, thin strip on the other.
    /// Inset by cornerSize so corners take priority at intersections.
    /// </summary>
    private void SetEdgeRect(RectTransform rt, Edge edge)
    {
        float c = cornerSize;
        float t = edgeThickness;

        switch (edge)
        {
            case Edge.Left:
                rt.anchorMin = new Vector2(0f, 0f);  rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(0f,  c);   rt.offsetMax = new Vector2(t,  -c);
                break;
            case Edge.Right:
                rt.anchorMin = new Vector2(1f, 0f);  rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 0.5f);
                rt.offsetMin = new Vector2(-t, c);    rt.offsetMax = new Vector2(0f, -c);
                break;
            case Edge.Bottom:
                rt.anchorMin = new Vector2(0f, 0f);  rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot     = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(c,  0f);   rt.offsetMax = new Vector2(-c,  t);
                break;
            case Edge.Top:
                rt.anchorMin = new Vector2(0f, 1f);  rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(c, -t);    rt.offsetMax = new Vector2(-c, 0f);
                break;
        }
    }

    internal static Vector2 CornerAnchor(Corner corner) => corner switch
    {
        Corner.BottomLeft  => new Vector2(0f, 0f),
        Corner.BottomRight => new Vector2(1f, 0f),
        Corner.TopLeft     => new Vector2(0f, 1f),
        Corner.TopRight    => new Vector2(1f, 1f),
        _                  => Vector2.zero
    };
}

// ═════════════════════════════════════════════════════════════════════════════
// Shared resize math — used by both CornerDragHandle and EdgeDragHandle
// ═════════════════════════════════════════════════════════════════════════════
internal static class ResizeMath
{
    private const float ScreenW = 1920f;
    private const float ScreenH = 1080f;

    /// <summary>
    /// Apply a resize delta to windowRect.
    /// signX: +1=right edge, -1=left edge, 0=no horizontal resize.
    /// signY: +1=top  edge, -1=bottom edge, 0=no vertical resize.
    /// </summary>
    internal static void Apply(RectTransform windowRect, Canvas canvas,
                               float signX, float signY,
                               Vector2 screenDelta,
                               float minWidth, float minHeight)
    {
        if (windowRect == null || canvas == null) return;

        float sf = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 delta = screenDelta / sf;

        Vector2 size = windowRect.sizeDelta;

        float clampedW = signX != 0f ? Mathf.Max(minWidth,  size.x + signX * delta.x) : size.x;
        float clampedH = signY != 0f ? Mathf.Max(minHeight, size.y + signY * delta.y) : size.y;

        float actualDX = (clampedW - size.x) * signX;
        float actualDY = (clampedH - size.y) * signY;

        windowRect.sizeDelta        = new Vector2(clampedW, clampedH);
        windowRect.anchoredPosition += new Vector2(actualDX * 0.5f, actualDY * 0.5f);

        // Screen-boundary clamp
        var wc = new Vector3[4];
        windowRect.GetWorldCorners(wc);

        if (signX != 0f)
        {
            float overX = signX > 0f ? Mathf.Max(0f, wc[2].x - ScreenW) : Mathf.Max(0f, -wc[0].x);
            if (overX > 0f)
            {
                windowRect.sizeDelta = new Vector2(
                    Mathf.Max(minWidth, windowRect.sizeDelta.x - overX),
                    windowRect.sizeDelta.y);
                windowRect.anchoredPosition -= new Vector2(signX * overX * 0.5f, 0f);
            }
        }
        if (signY != 0f)
        {
            float overY = signY > 0f ? Mathf.Max(0f, wc[1].y - ScreenH) : Mathf.Max(0f, -wc[0].y);
            if (overY > 0f)
            {
                windowRect.sizeDelta = new Vector2(
                    windowRect.sizeDelta.x,
                    Mathf.Max(minHeight, windowRect.sizeDelta.y - overY));
                windowRect.anchoredPosition -= new Vector2(0f, signY * overY * 0.5f);
            }
        }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// Shared accent visuals helper
// ═════════════════════════════════════════════════════════════════════════════
internal static class ResizeAccent
{
    internal static readonly Color Normal = new Color(0.55f, 0.65f, 0.80f, 0.00f);
    internal static readonly Color Hover  = new Color(0.55f, 0.65f, 0.90f, 0.55f);
    internal static readonly Color Active = new Color(0.70f, 0.80f, 1.00f, 0.80f);

    internal static Image Build(Transform parent, Vector2 size)
    {
        var go = new GameObject("ResizeAccent",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color         = Normal;
        img.raycastTarget = false;
        return img;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// Corner drag handle (diagonal resize)
// ═════════════════════════════════════════════════════════════════════════════
[RequireComponent(typeof(RectTransform))]
internal sealed class CornerDragHandle : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform _windowRect;
    private float         _minWidth, _minHeight;
    private float         _signX, _signY;
    private Canvas        _canvas;
    private Image         _accent;

    internal void Initialize(ResizableWindow owner, ResizableWindow.Corner corner)
    {
        _windowRect = owner.windowRect;
        _minWidth   = owner.minWidth;
        _minHeight  = owner.minHeight;
        _signX = (corner == ResizableWindow.Corner.BottomRight ||
                  corner == ResizableWindow.Corner.TopRight)   ?  1f : -1f;
        _signY = (corner == ResizableWindow.Corner.TopLeft     ||
                  corner == ResizableWindow.Corner.TopRight)   ?  1f : -1f;
    }

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        _accent = ResizeAccent.Build(transform, new Vector2(6f, 6f));
    }

    public void OnPointerDown(PointerEventData e)
    {
        _windowRect?.SetAsLastSibling();
        if (_accent) _accent.color = ResizeAccent.Active;
    }

    public void OnDrag(PointerEventData e) =>
        ResizeMath.Apply(_windowRect, _canvas, _signX, _signY, e.delta, _minWidth, _minHeight);

    public void OnPointerUp(PointerEventData e)   { if (_accent) _accent.color = ResizeAccent.Normal; }
    public void OnPointerEnter(PointerEventData e) { if (_accent) _accent.color = ResizeAccent.Hover;  }
    public void OnPointerExit(PointerEventData e)  { if (_accent) _accent.color = ResizeAccent.Normal; }
}

// ═════════════════════════════════════════════════════════════════════════════
// Edge drag handle (single-axis resize)
// ═════════════════════════════════════════════════════════════════════════════
[RequireComponent(typeof(RectTransform))]
internal sealed class EdgeDragHandle : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform _windowRect;
    private float         _minWidth, _minHeight;
    private float         _signX, _signY;
    private Canvas        _canvas;
    private Image         _accent;

    internal void Initialize(ResizableWindow owner, ResizableWindow.Edge edge)
    {
        _windowRect = owner.windowRect;
        _minWidth   = owner.minWidth;
        _minHeight  = owner.minHeight;

        // Edges only resize on one axis
        switch (edge)
        {
            case ResizableWindow.Edge.Left:   _signX = -1f; _signY = 0f; break;
            case ResizableWindow.Edge.Right:  _signX =  1f; _signY = 0f; break;
            case ResizableWindow.Edge.Bottom: _signX =  0f; _signY = -1f; break;
            case ResizableWindow.Edge.Top:    _signX =  0f; _signY =  1f; break;
        }
    }

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        // Thin accent bar: horizontal for top/bottom edges, vertical for left/right
        Vector2 accentSize = (_signX == 0f) ? new Vector2(40f, 2f) : new Vector2(2f, 40f);
        _accent = ResizeAccent.Build(transform, accentSize);
    }

    public void OnPointerDown(PointerEventData e)
    {
        _windowRect?.SetAsLastSibling();
        if (_accent) _accent.color = ResizeAccent.Active;
    }

    public void OnDrag(PointerEventData e) =>
        ResizeMath.Apply(_windowRect, _canvas, _signX, _signY, e.delta, _minWidth, _minHeight);

    public void OnPointerUp(PointerEventData e)    { if (_accent) _accent.color = ResizeAccent.Normal; }
    public void OnPointerEnter(PointerEventData e)  { if (_accent) _accent.color = ResizeAccent.Hover;  }
    public void OnPointerExit(PointerEventData e)   { if (_accent) _accent.color = ResizeAccent.Normal; }
}
