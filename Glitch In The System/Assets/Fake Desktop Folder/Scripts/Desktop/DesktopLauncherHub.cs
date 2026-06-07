using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single place that wires desktop/start-menu launchers and opens app windows.
/// </summary>
public static class DesktopLauncherHub
{
    private static DesktopAppWindow _workDashboard;
    private static SimpleAppWindow  _socialMedia;
    private static SimpleAppWindow  _paintApp;
    private static SimpleAppWindow  _stickyNotes;
    private static SimpleAppWindow  _fileExplorer;  // Batch 7

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

    public static void OpenPaintApp()
    {
        if (DesktopTutorialScope.IsContentModeratorOnly) return;

        EnsureInitialized();
        if (_paintApp == null)
            _paintApp = DesktopAppLocator.Find<SimpleAppWindow>("Paint", "PaintApp");
        _paintApp?.OpenFromLauncher();
    }

    public static void OpenStickyNotes()
    {
        if (DesktopTutorialScope.IsContentModeratorOnly) return;

        EnsureInitialized();
        if (_stickyNotes == null)
            _stickyNotes = DesktopAppLocator.Find<SimpleAppWindow>("StickyNotes", "StickyNotesApp");
        _stickyNotes?.OpenFromLauncher();
    }

    // ── Batch 7: File Explorer launcher ───────────────────────────────────
    public static void OpenFileExplorer()
    {
        if (DesktopTutorialScope.IsContentModeratorOnly) return;

        EnsureInitialized();
        if (_fileExplorer == null)
            _fileExplorer = DesktopAppLocator.Find<SimpleAppWindow>("FileExplorer", "FileExplorerApp");
        _fileExplorer?.OpenFromLauncher();
    }

    private static void CacheWindows()
    {
        if (_workDashboard == null)
            _workDashboard = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator", "WorkDashboard");
        if (_socialMedia == null)
            _socialMedia = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
        if (_paintApp == null)
            _paintApp = DesktopAppLocator.Find<SimpleAppWindow>("Paint", "PaintApp");
        if (_stickyNotes == null)
            _stickyNotes = DesktopAppLocator.Find<SimpleAppWindow>("StickyNotes", "StickyNotesApp");
        if (_fileExplorer == null)
            _fileExplorer = DesktopAppLocator.Find<SimpleAppWindow>("FileExplorer", "FileExplorerApp");
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
        WireButton("WorkDashboardButton", () =>
        {
            CloseStartMenuIfOpen();
            OpenWorkDashboard();
        });
        // Batch 7: "File Explorer Button" now correctly opens File Explorer
        // (was incorrectly wired to OpenSocialMedia — copy-paste artefact).
        WireButton("File Explorer Button", () =>
        {
            CloseStartMenuIfOpen();
            OpenFileExplorer();
        });
        WireButton("Paint Button", () =>
        {
            CloseStartMenuIfOpen();
            OpenPaintApp();
        });
        WireButton("Sticky Notes Button", () =>
        {
            CloseStartMenuIfOpen();
            OpenStickyNotes();
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
