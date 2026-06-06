using UnityEngine;

/// <summary>
/// Ensures app window shells are full-screen under the desktop canvas at scene load.
/// Button wiring is handled by <see cref="StartMenuController"/>.
/// </summary>
public static class DesktopLaunchBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        if (!Application.isPlaying) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Contains("Gameplay")) return;

        DesktopLauncherHub.EnsureInitialized();
    }

    public static void PrepareAppWindows()
    {
        var workDashboard = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator", "WorkDashboard");
        var socialMedia = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
        var paintApp = DesktopAppLocator.Find<SimpleAppWindow>("Paint", "PaintApp");
        var stickyNotes = DesktopAppLocator.Find<SimpleAppWindow>("StickyNotes", "StickyNotesApp");

        if (workDashboard != null)
            DesktopWindowLayer.PrepareWindowRoot(workDashboard.gameObject);

        if (!DesktopTutorialScope.IsContentModeratorOnly)
        {
            if (socialMedia != null)
                DesktopWindowLayer.PrepareWindowRoot(socialMedia.gameObject);
            if (paintApp != null)
                DesktopWindowLayer.PrepareWindowRoot(paintApp.gameObject);
            if (stickyNotes != null)
                DesktopWindowLayer.PrepareWindowRoot(stickyNotes.gameObject);
        }
    }
}
