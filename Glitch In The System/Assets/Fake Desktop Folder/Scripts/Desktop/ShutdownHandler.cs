using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the Shutdown Button: fades to black over 3 seconds then quits.
/// Attach to DesktopRoot. Wire Shutdown Button → TriggerShutdown().
/// </summary>
public sealed class ShutdownHandler : MonoBehaviour
{
    private const float FadeDuration = 3f;
    private bool _shutdownStarted;

    public void TriggerShutdown()
    {
        if (_shutdownStarted) return;
        _shutdownStarted = true;
        StartCoroutine(FadeAndQuit());
    }

    private IEnumerator FadeAndQuit()
    {
        // Build a fullscreen black overlay on top of everything in the canvas
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        var overlayGo = new GameObject("ShutdownOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));

        overlayGo.transform.SetParent(canvas.transform, false);
        overlayGo.transform.SetAsLastSibling();

        var rt = overlayGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = overlayGo.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;   // block all input during fade

        var cg = overlayGo.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = true;

        // Fade in
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / FadeDuration);
            yield return null;
        }

        cg.alpha = 1f;

        // Quit
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
