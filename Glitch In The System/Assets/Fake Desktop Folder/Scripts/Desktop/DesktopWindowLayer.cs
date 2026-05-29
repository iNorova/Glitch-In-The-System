using UnityEngine;

/// <summary>
/// Keeps app window shells full-screen under the desktop canvas so opened windows are visible.
/// </summary>
public static class DesktopWindowLayer
{
    public static void PrepareWindowRoot(GameObject windowRoot)
    {
        if (windowRoot == null) return;

        var canvas = windowRoot.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Transform shell = windowRoot.transform;
        while (shell.parent != null && shell.parent != canvas.transform)
            shell = shell.parent;

        var shellRect = shell as RectTransform;
        if (shellRect == null)
        {
            var windowRect = windowRoot.transform as RectTransform;
            if (windowRect == null) return;

            windowRect.SetParent(canvas.transform, false);
            StretchFullscreen(windowRect);
            DesktopUiStackOrder.BringAppShellForward(windowRect);
            return;
        }

        DesktopHierarchy.EnsureActive(shell.gameObject);
        StretchFullscreen(shellRect);
        DesktopUiStackOrder.BringAppShellForward(shell);
    }

    private static void StretchFullscreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        // Leave localScale to WindowAnimator so open/close scale tweens are not cancelled.
        if (rect.GetComponent<WindowAnimator>() == null)
            rect.localScale = Vector3.one;
    }
}
