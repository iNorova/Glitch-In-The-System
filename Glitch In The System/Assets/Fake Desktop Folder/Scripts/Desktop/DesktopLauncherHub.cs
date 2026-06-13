using UnityEngine;

/// <summary>
/// Central static launcher for all FakeDesktop apps.
/// Called by StartMenuController, FsAppRouter, and any system that needs to open an app.
/// </summary>
public static class DesktopLauncherHub
{
    private static SimpleAppWindow  _paintApp;
    private static SimpleAppWindow  _stickyNotesApp;
    private static SimpleAppWindow  _socialMediaApp;
    private static SimpleAppWindow  _fileExplorerApp;
    private static DesktopAppWindow _workDashboardApp;

    private static bool _initialized;

    // ── Init ──────────────────────────────────────────────────────────────
    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _paintApp         = DesktopAppLocator.Find<SimpleAppWindow> ("Paint",          "PaintApp");
        _stickyNotesApp   = DesktopAppLocator.Find<SimpleAppWindow> ("StickyNotes",    "StickyNotesApp");
        _socialMediaApp   = DesktopAppLocator.Find<SimpleAppWindow> ("SocialMedia",    "SocialMediaApp");
        _fileExplorerApp  = DesktopAppLocator.Find<SimpleAppWindow> ("FileExplorer",   "FileExplorerApp");
        _workDashboardApp = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator","WorkDashboard");
    }

    // ── App launchers ─────────────────────────────────────────────────────
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
        if (_stickyNotesApp == null)
            _stickyNotesApp = DesktopAppLocator.Find<SimpleAppWindow>("StickyNotes", "StickyNotesApp");
        _stickyNotesApp?.OpenFromLauncher();
    }

    public static void OpenSocialMedia()
    {
        EnsureInitialized();
        if (_socialMediaApp == null)
            _socialMediaApp = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
        _socialMediaApp?.OpenFromLauncher();
    }

    public static void OpenWorkDashboard()
    {
        EnsureInitialized();
        if (_workDashboardApp == null)
            _workDashboardApp = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator", "WorkDashboard");
        _workDashboardApp?.Open();
    }

    public static void OpenFileExplorer()
    {
        if (DesktopTutorialScope.IsContentModeratorOnly) return;

        EnsureInitialized();
        if (_fileExplorerApp == null)
            _fileExplorerApp = DesktopAppLocator.Find<SimpleAppWindow>("FileExplorer", "FileExplorerApp");
        _fileExplorerApp?.OpenFromLauncher();
    }

    /// <summary>
    /// Opens the SnippingExpandPreview modal to display a PNG texture.
    /// Used by FsAppRouter when the user double-clicks an image file.
    /// Does NOT open Paint — view-only.
    /// </summary>
    public static void OpenImagePreview(Texture2D texture)
        => OpenImagePreview(texture, null);

    /// Open preview with an explicit title-bar filename.
    public static void OpenImagePreview(Texture2D texture, string fileName)
    {
        if (texture == null) return;
        var preview = Object.FindFirstObjectByType<SnippingExpandPreview>(FindObjectsInactive.Include);
        if (preview != null)
        {
            if (!string.IsNullOrEmpty(fileName)) preview.OpenModal(texture, fileName);
            else                                  preview.OpenModal(texture);
        }
        else
            Debug.LogWarning("[DesktopLauncherHub] SnippingExpandPreview not found in scene.");
    }
}
