using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Captures a region of the screen into a Texture2D and displays it in a RawImage.
/// Saves screenshots to the game FS (persistentDataPath) and registers them in FileSystemManager.
///
/// Coordinate system (verified at runtime):
///   SnippingOverlay is a plain child of FakeDesktop (no own Canvas).
///   ScreenPointToLocalPointInRectangle against _overlayRT (pivot 0.5,0.5) returns
///   CENTER-ORIGIN coordinates: (0,0) = screen centre, (-960,-540) = bottom-left corner.
///   ReadPixels uses BOTTOM-LEFT origin in screen pixels.
///   Conversion: screenX = localX * scaleFactor + Screen.width  / 2
///               screenY = localY * scaleFactor + Screen.height / 2
///
/// Attach to SnippingToolAppWindow (or any active MonoBehaviour in the scene).
/// Call CaptureRegion(canvasLocalRect, sourceCanvas) from SnippingToolApp.OnSnipComplete.
/// </summary>
public sealed class SnippingCapture : MonoBehaviour
{
    [Header("Preview")]
    [Tooltip("RawImage inside the Snipping Tool window that shows the captured region.")]
    [SerializeField] private RawImage previewImage;

    [Tooltip("AspectRatioFitter on previewImage — keeps crop undistorted (optional).")]
    [SerializeField] private AspectRatioFitter aspectFitter;

    [Header("Actions")]
    [SerializeField] private UnityEngine.UI.Button saveButton;
    [SerializeField] private UnityEngine.UI.Button deleteButton;

    public Texture2D LastCapture { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (saveButton   != null) saveButton.onClick.AddListener(SaveCapture);
        if (deleteButton != null) deleteButton.onClick.AddListener(DeleteCapture);
        SetActionButtonsInteractable(false);
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void CaptureRegion(Rect canvasLocalRect, Canvas sourceCanvas)
    {
        StartCoroutine(CaptureRoutine(canvasLocalRect, sourceCanvas));
    }

    // ── Internal ──────────────────────────────────────────────────────────
    private IEnumerator CaptureRoutine(Rect canvasLocalRect, Canvas sourceCanvas)
    {
        yield return new WaitForEndOfFrame();

        float scaleFactor = sourceCanvas != null ? sourceCanvas.scaleFactor : 1f;

        Debug.Log($"[SnippingCapture] INPUT rect=({canvasLocalRect.x:F1},{canvasLocalRect.y:F1},{canvasLocalRect.width:F1},{canvasLocalRect.height:F1})  scaleFactor={scaleFactor}  Screen={Screen.width}x{Screen.height}");

        int screenX = Mathf.RoundToInt(canvasLocalRect.x * scaleFactor + Screen.width  * 0.5f);
        int screenY = Mathf.RoundToInt(canvasLocalRect.y * scaleFactor + Screen.height * 0.5f);
        int capW    = Mathf.RoundToInt(canvasLocalRect.width  * scaleFactor);
        int capH    = Mathf.RoundToInt(canvasLocalRect.height * scaleFactor);

        screenX = Mathf.Clamp(screenX, 0, Screen.width);
        screenY = Mathf.Clamp(screenY, 0, Screen.height);
        capW    = Mathf.Clamp(capW, 1, Screen.width  - screenX);
        capH    = Mathf.Clamp(capH, 1, Screen.height - screenY);

        Debug.Log($"[SnippingCapture] CAPTURE screenX={screenX} screenY={screenY} capW={capW} capH={capH}");

        if (capW < 1 || capH < 1)
        {
            Debug.LogWarning("[SnippingCapture] Selection too small to capture.");
            yield break;
        }

        if (LastCapture != null) { Destroy(LastCapture); LastCapture = null; }

        var tex = new Texture2D(capW, capH, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(screenX, screenY, capW, capH), 0, 0, false);
        tex.Apply(false);

        LastCapture = tex;
        SetActionButtonsInteractable(true);

        // Auto-save to game FS
        SaveToFileSystem(tex);

        if (previewImage != null)
        {
            previewImage.texture = tex;
            previewImage.gameObject.SetActive(true);
            if (aspectFitter != null)
            {
                aspectFitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
                aspectFitter.aspectRatio = (float)capW / capH;
            }
        }

        Debug.Log($"[SnippingCapture] Captured {capW}x{capH} at screen ({screenX},{screenY}).");
    }

    private void SaveToFileSystem(Texture2D texture)
    {
        string baseName = "Screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder   = Path.Combine(Application.persistentDataPath, "Screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, baseName + ".png"), texture.EncodeToPNG());
        Debug.Log($"[SnippingCapture] Saved to: {Path.Combine(folder, baseName + ".png")}");

        var entry = FileSystemManager.Instance?.RegisterScreenshot(baseName);
        if (entry != null)
            Debug.Log($"[SnippingCapture] Registered in Pictures/Screenshots: {entry.name}");
        else
            Debug.LogWarning("[SnippingCapture] FileSystemManager not found or folder missing.");
    }

    // ── Save / Delete ─────────────────────────────────────────────────────
    public void SaveCapture()
    {
        if (LastCapture == null)
        {
            Debug.LogWarning("[SnippingCapture] SaveCapture called with no capture.");
            return;
        }

        try
        {
            string baseName = "Snip_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            byte[] png      = LastCapture.EncodeToPNG();

            string folder   = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, baseName + ".png"), png);

            var entry = FileSystemManager.Instance?.RegisterScreenshot(baseName);
            if (entry != null)
                Debug.Log($"[SnippingCapture] SaveCapture → registered '{entry.name}' in Pictures/Screenshots");
            else
                Debug.LogWarning("[SnippingCapture] SaveCapture — FileSystemManager not found or folder missing.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SnippingCapture] SaveCapture failed: {ex.Message}");
        }
    }

    public void DeleteCapture()
    {
        if (previewImage != null)
        {
            previewImage.texture = null;
            previewImage.gameObject.SetActive(false);
        }

        if (aspectFitter != null) aspectFitter.aspectRatio = 1f;

        if (LastCapture != null) { Destroy(LastCapture); LastCapture = null; }

        SetActionButtonsInteractable(false);
        Debug.Log("[SnippingCapture] Capture deleted.");
    }

    private void SetActionButtonsInteractable(bool interactable)
    {
        if (saveButton   != null) saveButton.interactable   = interactable;
        if (deleteButton != null) deleteButton.interactable = interactable;
    }

    private void OnDestroy()
    {
        if (LastCapture != null) { Destroy(LastCapture); LastCapture = null; }
    }
}
