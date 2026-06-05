using UnityEngine;

/// <summary>
/// Watches DrawingArea's rect size and tells PaintApp to grow its Texture2D
/// when the window is resized larger. Shrinking never destroys pixels — the
/// DrawingCanvas stays at texture size and DrawingArea's RectMask2D clips the view.
///
/// Attach to DrawingCanvas (same GameObject as PaintApp).
/// Wire drawingArea in Inspector, or it auto-finds the parent at Start().
///
/// Behaviour:
///   - LateUpdate polls DrawingArea.rect size (cheap, just reads cached layout value).
///   - Texture ONLY GROWS — when viewport shrinks, texture stays full-size.
///   - DrawingCanvas.sizeDelta always equals texture size (not viewport size).
///   - RectMask2D on DrawingArea clips the visible portion automatically.
///   - When viewport grows beyond texture, ResizeCanvas() expands the texture.
///   - Does nothing if the window is not open (inactive hierarchy = no LateUpdate).
/// </summary>
[RequireComponent(typeof(PaintApp))]
public sealed class PaintCanvasResizer : MonoBehaviour
{
    [Tooltip("The DrawingArea RectTransform. Auto-found as parent if left empty.")]
    [SerializeField] private RectTransform drawingArea;

    private PaintApp      _paintApp;
    private RectTransform _canvasRect;   // DrawingCanvas RectTransform
    private Vector2       _lastAreaSize;

    // How many pixels the DrawingArea must change before we trigger a resize.
    // 1 px avoids constant micro-resizes from sub-pixel layout fluctuations.
    private const float ResizeThreshold = 1f;

    private void Awake()
    {
        _paintApp   = GetComponent<PaintApp>();
        _canvasRect = GetComponent<RectTransform>();

        if (drawingArea == null)
            drawingArea = transform.parent as RectTransform;
    }

    private void Start()
    {
        if (drawingArea == null)
        {
            Debug.LogWarning("[PaintCanvasResizer] DrawingArea not found. Resizer disabled.", this);
            enabled = false;
            return;
        }

        // Record the starting size so the first LateUpdate doesn't immediately trigger
        _lastAreaSize = new Vector2(
            Mathf.Round(drawingArea.rect.width),
            Mathf.Round(drawingArea.rect.height));
    }

    private void LateUpdate()
    {
        if (drawingArea == null || _paintApp == null) return;

        // Round to nearest integer pixel — avoids thrashing from sub-pixel layout jitter
        float w = Mathf.Round(drawingArea.rect.width);
        float h = Mathf.Round(drawingArea.rect.height);

        if (Mathf.Abs(w - _lastAreaSize.x) < ResizeThreshold &&
            Mathf.Abs(h - _lastAreaSize.y) < ResizeThreshold)
            return;

        _lastAreaSize = new Vector2(w, h);

        // Virtual canvas: the backing texture only ever GROWS, never shrinks.
        // When the window shrinks, DrawingCanvas stays at texture size and
        // DrawingArea's RectMask2D clips the visible portion — no pixels are lost.
        // When the window grows beyond the current texture, we expand to cover.
        int growW = Mathf.Max((int)w, _paintApp.TexW);
        int growH = Mathf.Max((int)h, _paintApp.TexH);

        if (growW != _paintApp.TexW || growH != _paintApp.TexH)
            _paintApp.ResizeCanvas(growW, growH);

        // DrawingCanvas.sizeDelta always equals TEXTURE size, not viewport size.
        // RectMask2D on DrawingArea clips to the visible viewport automatically.
        if (_canvasRect != null)
            _canvasRect.sizeDelta = new Vector2(_paintApp.TexW, _paintApp.TexH);
    }
}
