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

    // RESIZE: TexW/TexH are now instance fields so ResizeCanvas() can update them.
    // All internal logic continues to reference them by name — no behaviour change.
    // _visited is initialised in Awake() (was a field initialiser using the old consts).
    private int _texW = InitialTexW;
    private int _texH = InitialTexH;

    /// <summary>Current texture width. Read by PaintCanvasResizer.</summary>
    public int TexW => _texW;
    /// <summary>Current texture height. Read by PaintCanvasResizer.</summary>
    public int TexH => _texH;

    // PERF B5: pre-allocated visited buffer — reused across fills.
    // Initialised in Awake(); re-allocated only when canvas grows in ResizeCanvas().
    private bool[] _visited;
    // Stack reused across fills — avoids Queue<int> GC churn.
    private readonly Stack<FillSegment> _fillStack = new Stack<FillSegment>(512);

    // PERF: pre-allocated white pixel buffer — reused every ClearToWhite() call.
    // Eliminates the ~1.3MB GC allocation that previously occurred on every Clear press.
    // Re-allocated in ResizeCanvas() when the canvas grows, matching the _visited pattern.
    private Color32[] _whitePixels;

    private const int InitialTexW    = 740;
    private const int InitialTexH    = 460;
    private const int BrushRadius    = 4;
    private const int EraserRadius   = 10;

    // ── Palette colors ────────────────────────────────────────────────────────
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
        // Initialise visited buffer here (was field initialiser before TexW/TexH became fields)
        _visited = new bool[_texW * _texH];

        // PERF: initialise white pixel buffer once — reused by every ClearToWhite() call.
        _whitePixels = new Color32[_texW * _texH];
        for (int i = 0; i < _whitePixels.Length; i++)
            _whitePixels[i] = new Color32(255, 255, 255, 255);

        _tex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Point;
        ClearToWhite();
        if (drawingCanvas != null)
            drawingCanvas.texture = _tex;
    }

    // ── Canvas resize ─────────────────────────────────────────────────────────
    /// <summary>
    /// Grow the drawing canvas to newW x newH.
    /// Existing pixels are copied 1:1 into the top-left of the new texture.
    /// New area is filled white. Clamps to a minimum of 1x1.
    /// Called by PaintCanvasResizer; never called during a stroke.
    /// </summary>
    public void ResizeCanvas(int newW, int newH)
    {
        newW = Mathf.Max(1, newW);
        newH = Mathf.Max(1, newH);

        if (newW == _texW && newH == _texH) return;

        // ── Build new pixel array, filled white ──────────────────────────────
        int newSize = newW * newH;
        var newPixels = new Color32[newSize];
        for (int i = 0; i < newSize; i++)
            newPixels[i] = new Color32(255, 255, 255, 255);

        // ── Copy existing pixels into top-left of new canvas ─────────────────
        Color32[] oldPixels = _tex.GetPixels32();
        int copyW = Mathf.Min(_texW, newW);
        int copyH = Mathf.Min(_texH, newH);

        for (int y = 0; y < copyH; y++)
        {
            int oldBase = y * _texW;
            int newBase = y * newW;
            for (int x = 0; x < copyW; x++)
                newPixels[newBase + x] = oldPixels[oldBase + x];
        }

        // ── Create new texture ───────────────────────────────────────────────
        var newTex = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
        newTex.filterMode = FilterMode.Point;
        newTex.SetPixels32(newPixels);
        newTex.Apply();

        // ── Destroy old texture to free GPU memory ───────────────────────────
        if (_tex != null)
            Destroy(_tex);
        _tex = newTex;

        // ── Update dimensions ────────────────────────────────────────────────
        _texW = newW;
        _texH = newH;

        // ── Resize _visited buffer ───────────────────────────────────────────
        if (_visited == null || _visited.Length < newSize)
            _visited = new bool[newSize];
        else
            System.Array.Clear(_visited, 0, _visited.Length);

        // ── Resize _whitePixels buffer ───────────────────────────────────────
        if (_whitePixels == null || _whitePixels.Length < newSize)
        {
            _whitePixels = new Color32[newSize];
            for (int i = 0; i < newSize; i++)
                _whitePixels[i] = new Color32(255, 255, 255, 255);
        }

        // ── Reassign texture to RawImage ─────────────────────────────────────
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

    // ── Batch 7: Save to File Explorer ───────────────────────────────────────
    /// <summary>
    /// Registers the current drawing as a .png entry in /Pictures/Screenshots in
    /// the virtual file system. Does NOT write actual image bytes — the virtual FS
    /// stores display-only file entries for the prototype.
    ///
    /// Call this from a "Save" button on the Paint UI.
    /// Returns the new file name (e.g. "screenshot_20240115_143022.png"), or null
    /// if FileSystemManager is not available.
    ///
    /// Drawing logic, canvas, and texture are completely unchanged.
    /// </summary>
    public string SaveToExplorer()
    {
        var fs = FileSystemManager.Instance;
        if (fs == null)
        {
            Debug.LogWarning("[PaintApp] SaveToExplorer: FileSystemManager not available.");
            return null;
        }

        string baseName = "screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var entry = fs.RegisterScreenshot(baseName);

        if (entry == null)
        {
            Debug.LogWarning("[PaintApp] SaveToExplorer: Could not create screenshot entry. " +
                             "Ensure /Pictures/Screenshots exists in the virtual FS.");
            return null;
        }

        Debug.Log($"[PaintApp] Saved to File Explorer: {entry.fullPath}");
        return entry.name;
    }

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

    // ── Flood fill (scanline) ─────────────────────────────────────────────────
    private void FloodFill(Vector2 pixel)
    {
        int startX = Mathf.RoundToInt(pixel.x);
        int startY = Mathf.RoundToInt(pixel.y);

        startX = Mathf.Clamp(startX, 0, _texW - 1);
        startY = Mathf.Clamp(startY, 0, _texH - 1);

        Color32[] pixels    = _tex.GetPixels32();
        Color32 targetColor = pixels[startY * _texW + startX];
        Color32 fillColor   = (Color32)_currentColor;

        if (targetColor.r == fillColor.r && targetColor.g == fillColor.g &&
            targetColor.b == fillColor.b && targetColor.a == fillColor.a)
            return;

        _fillStack.Clear();

        int lx = startX, rx = startX;
        while (lx > 0 && ColorMatches(pixels[(startY * _texW) + lx - 1], targetColor)) lx--;
        while (rx < _texW - 1 && ColorMatches(pixels[(startY * _texW) + rx + 1], targetColor)) rx++;

        _fillStack.Push(new FillSegment(startY, lx, rx, 0));

        while (_fillStack.Count > 0)
        {
            var seg = _fillStack.Pop();
            int y   = seg.Y;
            int x1  = seg.X1;
            int x2  = seg.X2;

            int rowBase = y * _texW;
            while (x1 > 0 && ColorMatches(pixels[rowBase + x1 - 1], targetColor)) x1--;
            while (x2 < _texW - 1 && ColorMatches(pixels[rowBase + x2 + 1], targetColor)) x2++;

            for (int x = x1; x <= x2; x++)
            {
                int idx = rowBase + x;
                pixels[idx]   = fillColor;
                _visited[idx] = true;
            }

            ScanRow(pixels, y + 1, x1, x2, targetColor, +1);
            ScanRow(pixels, y - 1, x1, x2, targetColor, -1);
        }

        for (int i = 0; i < pixels.Length; i++)
            if (_visited[i]) { _visited[i] = false; }

        _tex.SetPixels32(pixels);
        _tex.Apply();
    }

    private void ScanRow(Color32[] pixels, int y, int x1, int x2, Color32 targetColor, int dy)
    {
        if (y < 0 || y >= _texH) return;

        int rowBase   = y * _texW;
        bool inRun    = false;
        int  runStart = 0;

        for (int x = x1; x <= x2; x++)
        {
            int idx = rowBase + x;
            bool matches = !_visited[idx] && ColorMatches(pixels[idx], targetColor);

            if (matches && !inRun)
            {
                runStart = x;
                inRun    = true;
            }
            else if (!matches && inRun)
            {
                _fillStack.Push(new FillSegment(y, runStart, x - 1, dy));
                inRun = false;
            }
        }

        if (inRun)
            _fillStack.Push(new FillSegment(y, runStart, x2, dy));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool ColorMatches(Color32 a, Color32 b)
        => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool IsOnCanvas(PointerEventData e)
    {
        if (canvasRect == null) return false;
        var viewportRect = canvasRect.parent as RectTransform;
        if (viewportRect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(
            viewportRect, e.position, e.pressEventCamera);
    }

    private Vector2 PointerToPixel(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, e.position, e.pressEventCamera, out var local);
        var rect = canvasRect.rect;
        float u = (local.x - rect.xMin) / rect.width;
        float v = (local.y - rect.yMin) / rect.height;
        return new Vector2(
            Mathf.Clamp(u * _texW, 0, _texW - 1),
            Mathf.Clamp(v * _texH, 0, _texH - 1));
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
            if (px < 0 || px >= _texW || py < 0 || py >= _texH) continue;
            _tex.SetPixel(px, py, paintColor);
        }
    }

    private void ClearToWhite()
    {
        // PERF: reuse pre-allocated _whitePixels buffer — zero GC allocation.
        // Buffer is initialised in Awake() and kept in sync with canvas size in ResizeCanvas().
        _tex.SetPixels32(_whitePixels, 0);
        _tex.Apply();
    }

    // ── Scanline fill segment ─────────────────────────────────────────────────
    private readonly struct FillSegment
    {
        public readonly int Y;
        public readonly int X1;
        public readonly int X2;
        public readonly int DY;
        public FillSegment(int y, int x1, int x2, int dy) { Y = y; X1 = x1; X2 = x2; DY = dy; }
    }
}
