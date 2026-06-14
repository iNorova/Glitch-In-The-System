using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Captures a region of the screen into a Texture2D and displays it in a RawImage.
/// Saves screenshots to the game FS (persistentDataPath) and registers them in FileExplorerManager.
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

    // Cached PNG bytes from the most recent capture.
    // Encoded once on a background thread immediately after ReadPixels.
    // SaveCapture() reuses this — zero re-encode on manual save.
    // Cleared on DeleteCapture() and replaced on each new capture.
    private byte[] _lastPngBytes;

    // Name of a screenshot whose FileExplorerManager registration is pending
    // (set by background thread, consumed on main thread in LateUpdate).
    private volatile string _pendingRegistration;

    // Absolute path of the screenshot to pre-register in FsTextureCache.
    // Set by background thread alongside _pendingRegistration; consumed in LateUpdate.
    // Using absolute path so it matches the exact key FsAppRouter uses on first open.
    private volatile string _pendingCacheKey;

    private void LateUpdate()
    {
        // Pre-register texture in cache BEFORE registering in FS manager,
        // so the File Explorer can open it instantly from cache with zero disk read.
        var cacheKey = _pendingCacheKey;
        if (cacheKey != null && LastCapture != null)
        {
            _pendingCacheKey = null;
            // owned: false — SnippingCapture.LastCapture lifetime is managed here
            // (Destroy in DeleteCapture/OnDestroy). Cache must never destroy it.
            FsTextureCache.Set(cacheKey, LastCapture, owned: false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SnippingCapture] Pre-cached texture → {cacheKey}");
#endif
        }

        var pending = _pendingRegistration;
        if (pending == null) return;
        _pendingRegistration = null; // consume before registering (idempotent)

        var entry = FileExplorerManager.Instance?.RegisterScreenshot(pending);

        // FIX: braces required so 'else' stays valid when #if block is stripped in non-editor builds.
        // Without braces, the preprocessor leaves 'if (entry != null) <nothing> else ...' which is CS8641.
        if (entry != null)
        {
            FsStatusToast.ShowGlobal($"Screenshot saved — {entry.name}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SnippingCapture] Registered in Pictures/Screenshots: {entry.name}");
#endif
        }
        else
            Debug.LogWarning("[SnippingCapture] RegisterScreenshot: FileExplorerManager not found or folder missing.");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (saveButton   != null) saveButton.onClick.AddListener(SaveCapture);
        if (deleteButton != null) deleteButton.onClick.AddListener(DeleteCapture);
        SetActionButtonsInteractable(false);

        // Clear texture cache on quit so owned disk-loaded textures are destroyed cleanly.
        // Only owned=true textures (FsAppRouter-loaded) are destroyed; LastCapture is owned=false
        // and will be destroyed by OnDestroy below.
        Application.quitting += OnApplicationQuitting;
    }

    private static void OnApplicationQuitting()
    {
        FsTextureCache.Clear();
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SnippingCapture] INPUT rect=({canvasLocalRect.x:F1},{canvasLocalRect.y:F1},{canvasLocalRect.width:F1},{canvasLocalRect.height:F1})  scaleFactor={scaleFactor}  Screen={Screen.width}x{Screen.height}");
#endif

        int screenX = Mathf.RoundToInt(canvasLocalRect.x * scaleFactor + Screen.width  * 0.5f);
        int screenY = Mathf.RoundToInt(canvasLocalRect.y * scaleFactor + Screen.height * 0.5f);
        int capW    = Mathf.RoundToInt(canvasLocalRect.width  * scaleFactor);
        int capH    = Mathf.RoundToInt(canvasLocalRect.height * scaleFactor);

        screenX = Mathf.Clamp(screenX, 0, Screen.width);
        screenY = Mathf.Clamp(screenY, 0, Screen.height);
        capW    = Mathf.Clamp(capW, 1, Screen.width  - screenX);
        capH    = Mathf.Clamp(capH, 1, Screen.height - screenY);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SnippingCapture] CAPTURE screenX={screenX} screenY={screenY} capW={capW} capH={capH}");
#endif

        if (capW < 1 || capH < 1)
        {
            Debug.LogWarning("[SnippingCapture] Selection too small to capture.");
            yield break;
        }

        if (LastCapture != null) { Destroy(LastCapture); LastCapture = null; }

        var tex = new Texture2D(capW, capH, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(screenX, screenY, capW, capH), 0, 0, false);
        tex.Apply(false);

        LastCapture    = tex;
        _lastPngBytes  = null; // clear stale bytes before background encode starts
        SetActionButtonsInteractable(true);

        // Show preview immediately — no encode stall before user sees the result.
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SnippingCapture] Captured {capW}x{capH} at screen ({screenX},{screenY}).");
#endif

        // EncodeToPNG must run on the main thread (Unity API reads native texture data).
        // Do it here — this is the only encode in the entire capture pipeline.
        // The resulting byte[] is then handed to a background thread for the disk write,
        // so File.WriteAllBytes never blocks the main thread.
        string autoBaseName = "Screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        byte[] pngBytes = tex.EncodeToPNG(); // main thread — required; happens once
        _lastPngBytes   = pngBytes;          // cache so SaveCapture() reuses, never re-encodes

        // Cache before Task.Run — Application.persistentDataPath is a Unity API,
        // forbidden on worker threads.
        string saveFolder = Path.Combine(Application.persistentDataPath, "Screenshots");

        _ = Task.Run(() =>
        {
            try
            {
                // Only pure IO here — no Unity API calls.
                Directory.CreateDirectory(saveFolder);
                File.WriteAllBytes(Path.Combine(saveFolder, autoBaseName + ".png"), pngBytes);

                // FileExplorerManager.RegisterScreenshot must run on the main thread.
                // Signal LateUpdate to call it next frame.
                _pendingRegistration = autoBaseName;

                // Also signal LateUpdate to pre-register the texture in FsTextureCache.
                // Key = absolute path matching what FsAppRouter uses as its cache key.
                _pendingCacheKey = System.IO.Path.Combine(saveFolder, autoBaseName + ".png");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[SnippingCapture] Background file write failed: {ex.Message}");
            }
        });
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
            // Reuse bytes cached by the background encode — zero re-encode on main thread.
            // Fall back to encoding now only if the background task hasn't finished yet
            // (extremely unlikely: user would have to click Save within ~50ms of capture).
            byte[] png = _lastPngBytes ?? LastCapture.EncodeToPNG();

            string folder   = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, baseName + ".png"), png);

            var entry = FileExplorerManager.Instance?.RegisterScreenshot(baseName);

            // FIX: braces required so 'else' stays valid when #if block is stripped in non-editor builds.
            if (entry != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SnippingCapture] SaveCapture → registered '{entry.name}' in Pictures/Screenshots");
#endif
            }
            else
                Debug.LogWarning("[SnippingCapture] SaveCapture — FileExplorerManager not found or folder missing.");
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
        _lastPngBytes = null;

        SetActionButtonsInteractable(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SnippingCapture] Capture deleted.");
#endif
    }

    private void SetActionButtonsInteractable(bool interactable)
    {
        if (saveButton   != null) saveButton.interactable   = interactable;
        if (deleteButton != null) deleteButton.interactable = interactable;
    }

    private void OnDestroy()
    {
        Application.quitting -= OnApplicationQuitting;
        // Evict our own (unowned) cache entry so the dead texture reference is removed.
        if (_pendingCacheKey != null) FsTextureCache.Evict(_pendingCacheKey);
        if (LastCapture != null) { Destroy(LastCapture); LastCapture = null; }
    }
}
