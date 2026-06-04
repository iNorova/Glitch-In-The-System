using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Lightweight MS-Paint-style scribble app for the fake desktop.
/// Attach to DrawingCanvas. Wire drawingCanvas + canvasRect in Inspector.
/// </summary>
public sealed class PaintApp : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RawImage      drawingCanvas;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private GameObject    colorPalettePanel; // assigned at runtime

    private Texture2D _tex;
    private Color     _currentColor = Color.black;
    private bool      _isPainting;
    private Vector2   _lastPixel;
    private bool      _isErasing;
    private bool      _isFilling;

    private const int TexW         = 740;
    private const int TexH         = 460;
    private const int BrushRadius  = 4;
    private const int EraserRadius = 10;

    // ── Palette colors — add new entries here to expand the palette ───────────
    public static readonly (string name, Color color)[] PaletteColors = new[]
    {
        ("Black",  Color.black),
        ("White",  Color.white),
        ("Red",    new Color(0.85f, 0.07f, 0.07f)),
        ("Blue",   new Color(0.07f, 0.25f, 0.85f)),
        ("Green",  new Color(0.07f, 0.60f, 0.15f)),
        ("Yellow", new Color(0.95f, 0.85f, 0.05f)),
    };

    private void Awake()
    {
        _tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Point;
        ClearToWhite();
        if (drawingCanvas != null)
            drawingCanvas.texture = _tex;
    }

    // ── Tool switching ────────────────────────────────────────────────────────
    public void SetToolPencil() { _isErasing = false; _isFilling = false; }
    public void SetToolEraser() { _isErasing = true;  _isFilling = false; }
    public void SetToolFill()   { _isErasing = false; _isFilling = true;  }

    // ── Color palette ─────────────────────────────────────────────────────────
    public void ToggleColorPalette()
    {
        if (colorPalettePanel == null) return;
        colorPalettePanel.SetActive(!colorPalettePanel.activeSelf);
    }

    public void SetColorPalettePanel(GameObject panel) => colorPalettePanel = panel;

    /// <summary>Select a color by index into PaletteColors. Called by swatch buttons.</summary>
    public void SelectColor(int index)
    {
        if (index < 0 || index >= PaletteColors.Length) return;
        _currentColor = PaletteColors[index].color;
        _isErasing    = false;
        _isFilling    = false;
        if (colorPalettePanel != null) colorPalettePanel.SetActive(false);
    }

    // Legacy named setters kept for backward compat
    public void SetColorBlack()  => SelectColor(0);
    public void SetColorWhite()  => SelectColor(1);
    public void SetColorRed()    => SelectColor(2);
    public void SetColorBlue()   => SelectColor(3);
    public void SetColorGreen()  => SelectColor(4);
    public void SetColorYellow() => SelectColor(5);
    public void SetColor(Color c){ _isFilling = false; _isErasing = false; _currentColor = c; }

    // ── Clear ─────────────────────────────────────────────────────────────────
    public void ClearCanvas() => ClearToWhite();

    // ── Drawing ───────────────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData e)
    {
        if (!IsOnCanvas(e)) return;
        var px = PointerToPixel(e);
        if (_isFilling) { FloodFill(px); return; }
        _isPainting = true;
        _lastPixel  = px;
        PaintCircle(px);
        _tex.Apply();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_isPainting || _isFilling) return;
        var px = PointerToPixel(e);
        PaintLine(_lastPixel, px);
        _lastPixel = px;
        _tex.Apply();
    }

    public void OnPointerUp(PointerEventData e) => _isPainting = false;

    // ── Flood fill ────────────────────────────────────────────────────────────
    private void FloodFill(Vector2 pixel)
    {
        int startX = Mathf.RoundToInt(pixel.x);
        int startY = Mathf.RoundToInt(pixel.y);
        Color32[] pixels     = _tex.GetPixels32();
        Color32 targetColor  = pixels[startY * TexW + startX];
        Color32 fillColor    = _currentColor;
        if (targetColor.r == fillColor.r && targetColor.g == fillColor.g &&
            targetColor.b == fillColor.b && targetColor.a == fillColor.a) return;
        var queue = new Queue<int>();
        queue.Enqueue(startY * TexW + startX);
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            if (pixels[idx].r != targetColor.r || pixels[idx].g != targetColor.g ||
                pixels[idx].b != targetColor.b || pixels[idx].a != targetColor.a) continue;
            pixels[idx] = fillColor;
            int x = idx % TexW, y = idx / TexW;
            if (x > 0)        queue.Enqueue(idx - 1);
            if (x < TexW - 1) queue.Enqueue(idx + 1);
            if (y > 0)        queue.Enqueue(idx - TexW);
            if (y < TexH - 1) queue.Enqueue(idx + TexW);
        }
        _tex.SetPixels32(pixels);
        _tex.Apply();
    }

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
        int   radius     = _isErasing ? EraserRadius : BrushRadius;
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
