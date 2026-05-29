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

        if (workDashboard != null)
            DesktopWindowLayer.PrepareWindowRoot(workDashboard.gameObject);

        if (socialMedia != null && !DesktopTutorialScope.IsContentModeratorOnly)
            DesktopWindowLayer.PrepareWindowRoot(socialMedia.gameObject);
    }
}
