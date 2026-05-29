using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single place that wires desktop/start-menu launchers and opens app windows.
/// </summary>
public static class DesktopLauncherHub
{
    private static DesktopAppWindow _workDashboard;
    private static SimpleAppWindow _socialMedia;

    public static void EnsureInitialized()
    {
        CacheWindows();
        EnsureDesktopCanvas();
        DesktopLaunchBootstrap.PrepareAppWindows();
        WireButtons();
    }

    public static void OpenWorkDashboard()
    {
        EnsureInitialized();
        if (_workDashboard == null)
            _workDashboard = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator", "WorkDashboard");
        _workDashboard?.OpenFromLauncher();
    }

    public static void OpenSocialMedia()
    {
        if (DesktopTutorialScope.IsContentModeratorOnly) return;

        EnsureInitialized();
        if (_socialMedia == null)
            _socialMedia = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
        _socialMedia?.OpenFromLauncher();
    }

    private static void CacheWindows()
    {
        if (_workDashboard == null)
            _workDashboard = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator", "WorkDashboard");
        if (_socialMedia == null)
            _socialMedia = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
    }

    private static void EnsureDesktopCanvas()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
            if (!canvas.gameObject.name.Contains("FakeDesktop")) continue;

            var rt = canvas.transform as RectTransform;
            if (rt != null && rt.localScale == Vector3.zero)
                rt.localScale = Vector3.one;

            if (canvas.GetComponent<DesktopCanvasPlayModeFix>() == null)
                canvas.gameObject.AddComponent<DesktopCanvasPlayModeFix>();
            return;
        }
    }

    private static void WireButtons()
    {
        // Desktop Icon: scene Inspector → DesktopAppWindow.OpenFromLauncher
        WireButton("WorkDashboardButton", () =>
        {
            CloseStartMenuIfOpen();
            OpenWorkDashboard();
        });
        WireButton("File Explorer Button", () =>
        {
            CloseStartMenuIfOpen();
            OpenSocialMedia();
        });
    }

    private static void CloseStartMenuIfOpen()
    {
        var menu = GameObject.Find("StartMenu");
        if (menu == null || !menu.activeSelf) return;

        var controller = Object.FindFirstObjectByType<StartMenuController>();
        if (controller != null)
            controller.AnimateClose();
        else
            menu.SetActive(false);
    }

    private static void WireButton(string buttonName, UnityEngine.Events.UnityAction handler)
    {
        var button = FindSceneButton(buttonName);
        if (button == null) return;

        DisableChildRaycastBlockers(button.gameObject);
        DesktopUIButtonWiring.SetSingleClickListener(button, handler);
    }

    private static Button FindSceneButton(string name)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || !b.gameObject.scene.IsValid()) continue;
            if (b.name == name) return b;
        }
        return null;
    }

    private static void DisableChildRaycastBlockers(GameObject root)
    {
        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.gameObject != root)
                tmp.raycastTarget = false;
        }
    }
}
