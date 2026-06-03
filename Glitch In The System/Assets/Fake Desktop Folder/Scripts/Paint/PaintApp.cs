using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight MS-Paint-style scribble app for the fake desktop.
/// Attach to DrawingCanvas. Wire drawingCanvas + canvasRect in Inspector.
/// </summary>
public sealed class PaintApp : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RawImage drawingCanvas;
    [SerializeField] private RectTransform canvasRect;

    private Texture2D _tex;
    private Color _currentColor = Color.black;
    private bool _isPainting;
    private Vector2 _lastPixel;
    private bool _isErasing;

    private const int TexW = 740;
    private const int TexH = 460;
    private const int BrushRadius = 4;
    private const int EraserRadius = 10;

    private void Awake()
    {
        _tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Point;
        ClearToWhite();
        if (drawingCanvas != null)
            drawingCanvas.texture = _tex;
    }

    // ── Tool switching ────────────────────────────────────────────────────────
    public void SetToolPencil() => _isErasing = false;
    public void SetToolEraser() => _isErasing = true;

    // ── Color buttons ─────────────────────────────────────────────────────────
    public void SetColorBlack()  { _isErasing = false; _currentColor = Color.black; }
    public void SetColorRed()    { _isErasing = false; _currentColor = new Color(0.85f, 0.07f, 0.07f); }
    public void SetColorBlue()   { _isErasing = false; _currentColor = new Color(0.07f, 0.25f, 0.85f); }
    public void SetColorGreen()  { _isErasing = false; _currentColor = new Color(0.07f, 0.60f, 0.15f); }
    public void SetColorYellow() { _isErasing = false; _currentColor = new Color(0.95f, 0.85f, 0.05f); }
    public void SetColor(Color c){ _isErasing = false; _currentColor = c; }

    // ── Clear ─────────────────────────────────────────────────────────────────
    public void ClearCanvas() => ClearToWhite();

    // ── Drawing ───────────────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData e)
    {
        if (!IsOnCanvas(e)) return;
        _isPainting = true;
        var px = PointerToPixel(e);
        _lastPixel = px;
        PaintCircle(px);
        _tex.Apply();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_isPainting) return;
        var px = PointerToPixel(e);
        PaintLine(_lastPixel, px);
        _lastPixel = px;
        _tex.Apply();
    }

    public void OnPointerUp(PointerEventData e) => _isPainting = false;

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool IsOnCanvas(PointerEventData e)
    {
        if (canvasRect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(
            canvasRect, e.position, e.pressEventCamera);
    }

    private Vector2 PointerToPixel(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, e.position, e.pressEventCamera, out var local);
        var rect = canvasRect.rect;
        float u = (local.x - rect.xMin) / rect.width;
        float v = (local.y - rect.yMin) / rect.height;
        return new Vector2(
            Mathf.Clamp(u * TexW, 0, TexW - 1),
            Mathf.Clamp(v * TexH, 0, TexH - 1));
    }

    private void PaintLine(Vector2 from, Vector2 to)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to)));
        for (int i = 0; i <= steps; i++)
            PaintCircle(Vector2.Lerp(from, to, (float)i / steps));
    }

    private void PaintCircle(Vector2 center)
    {
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        Color paintColor = _isErasing ? Color.white : _currentColor;
        int radius = _isErasing ? EraserRadius : BrushRadius;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int px = cx + dx, py = cy + dy;
            if (px < 0 || px >= TexW || py < 0 || py >= TexH) continue;
            _tex.SetPixel(px, py, paintColor);
        }
    }

    private void ClearToWhite()
    {
        var fill = new Color32[TexW * TexH];
        for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(255, 255, 255, 255);
        _tex.SetPixels32(fill);
        _tex.Apply();
    }
}
