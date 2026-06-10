using System.Collections;
using UnityEngine;

/// <summary>
/// Snipping Tool app controller. Attach to SnippingToolAppWindow.
/// Wire closeButton, newScreenshotButton, snippingOverlay, snippingCapture in Inspector.
/// </summary>
public sealed class SnippingToolApp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [SerializeField] private UnityEngine.UI.Button newScreenshotButton;
    [SerializeField] private SnippingOverlay       snippingOverlay;
    [SerializeField] private SnippingCapture       snippingCapture;

    private SimpleAppWindow _window;
    private WindowAnimator  _animator;
    private Canvas          _canvas; // FakeDesktop canvas — passed to SnippingCapture

    private void Awake()
    {
        _window   = GetComponent<SimpleAppWindow>();
        _animator = GetComponent<WindowAnimator>();
        _canvas   = GetComponentInParent<Canvas>();
        Debug.Log($"[SnippingToolApp] Awake — window={_window != null}  animator={_animator != null}  canvas={_canvas?.name}");

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseWindow);

        if (newScreenshotButton != null)
            newScreenshotButton.onClick.AddListener(OnNewScreenshot);

        if (snippingOverlay != null)
        {
            snippingOverlay.onSnipComplete  = OnSnipComplete;
            snippingOverlay.onSnipCancelled = OnSnipCancelled;
            snippingOverlay.gameObject.SetActive(false);
        }
    }

    // ── Button handlers ────────────────────────────────────────────────────────
    private void CloseWindow()
    {
        if (_window != null) _window.Close();
        else gameObject.SetActive(false);
    }

    private void OnNewScreenshot()
    {
        if (snippingOverlay == null)
        {
            Debug.LogWarning("[SnippingTool] SnippingOverlay not assigned.");
            return;
        }

        // Chain BeginSnip off the close animation's onComplete callback.
        //
        // WHY NOT _window.Close() + coroutine:
        //   SimpleAppWindow.Close() calls AnimateClose() without a callback, so we
        //   can't know when the animation finishes. A frame-count wait is unreliable
        //   (animation = ~0.13 s = ~8 frames at 60 fps; 2-frame wait means the window
        //   is still mid-animation when snip mode starts).
        //
        // HOW THIS WORKS:
        //   AnimateClose(callback) fires the callback synchronously from inside the
        //   close coroutine, immediately after SnapClosedVisuals() calls SetActive(false)
        //   but before Unity processes the deactivation for this coroutine frame. So the
        //   callback runs and calls snippingOverlay.BeginSnip(). The overlay lives on
        //   FakeDesktop level (always active) — safe to call even after window deactivates.
        if (_animator != null)
        {
            _animator.AnimateClose(() => snippingOverlay.BeginSnip());
        }
        else
        {
            // Fallback: no animator — hide instantly, wait 2 frames for rendering to settle.
            if (_window != null) _window.Close();
            else gameObject.SetActive(false);
            StartCoroutine(BeginSnipFallback());
        }
    }

    /// Fallback path when WindowAnimator is absent. Not expected in normal setup.
    private IEnumerator BeginSnipFallback()
    {
        yield return null;
        yield return null;
        snippingOverlay.BeginSnip();
    }

    // ── Overlay callbacks ──────────────────────────────────────────────────────
    private void OnSnipComplete(Rect canvasLocalRect)
    {
        Debug.Log($"[SnippingToolApp] OnSnipComplete — rect=({canvasLocalRect.x:F1},{canvasLocalRect.y:F1},{canvasLocalRect.width:F1},{canvasLocalRect.height:F1})  canvas={_canvas?.name}  scaleFactor={_canvas?.scaleFactor}  windowActive={gameObject.activeSelf}");
        // ORDER MATTERS:
        //
        // 1. RestoreWindow() FIRST:
        //    By the time the user finishes a snip, the window's close animation
        //    (0.13 s) has long completed and SetActive(false) was called.
        //    SnippingCapture.StartCoroutine() silently fails on inactive GameObjects.
        //    RestoreWindow() → OpenIfClosed() → EnsureActive() → SetActive(true) +
        //    AnimateOpen() which immediately sets CanvasGroup.alpha = 0.
        //    The window is now ACTIVE (so StartCoroutine works) but INVISIBLE
        //    (alpha = 0, set synchronously before the first yield in OpenRoutine).
        //
        // 2. CaptureRegion() SECOND:
        //    Starts the capture coroutine. WaitForEndOfFrame fires in the same frame.
        //    At EndOfFrame, the canvas renders with window alpha = 0 → not in screenshot.
        //    Overlay is already inactive (EndSnip called before this callback fires).
        RestoreWindow();

        if (snippingCapture != null)
            snippingCapture.CaptureRegion(canvasLocalRect, _canvas);
        else
            Debug.LogWarning("[SnippingTool] SnippingCapture not assigned.");
    }

    private void OnSnipCancelled()
    {
        RestoreWindow();
    }

    private void RestoreWindow()
    {
        if (_window != null)
            _window.OpenIfClosed();
    }
}
