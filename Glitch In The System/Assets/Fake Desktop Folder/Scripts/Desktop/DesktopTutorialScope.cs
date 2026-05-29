using UnityEngine;

/// <summary>
/// During intro tutorial, only the Content Moderator app should be available.
/// </summary>
public static class DesktopTutorialScope
{
    public static bool IsContentModeratorOnly { get; private set; }

    public static void Begin()
    {
        IsContentModeratorOnly = true;
        EnforceContentModeratorOnly();
        SetLauncherInteractable("File Explorer Button", false);
    }

    public static void End()
    {
        IsContentModeratorOnly = false;
        SetLauncherInteractable("File Explorer Button", true);
        RestoreSocialShell();
    }

    public static void EnforceContentModeratorOnly()
    {
        var social = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
        if (social != null)
            social.Close();

        var socialShell = GameObject.Find("Social Media");
        if (socialShell != null)
            socialShell.SetActive(false);
    }

    private static void RestoreSocialShell()
    {
        var socialShell = GameObject.Find("Social Media");
        if (socialShell != null)
            socialShell.SetActive(true);
    }

    private static void SetLauncherInteractable(string buttonName, bool interactable)
    {
        foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || !b.gameObject.scene.IsValid()) continue;
            if (b.name == buttonName)
                b.interactable = interactable;
        }
    }
}
