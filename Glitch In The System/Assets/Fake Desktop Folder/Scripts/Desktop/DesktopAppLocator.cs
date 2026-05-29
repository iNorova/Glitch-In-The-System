using UnityEngine;

/// <summary>
/// Finds desktop app window components in the active gameplay scene.
/// </summary>
public static class DesktopAppLocator
{
    public static T Find<T>(string windowId, string nameContains) where T : Component
    {
        foreach (var mini in Object.FindObjectsByType<MinimizableWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mini == null || !mini.gameObject.scene.IsValid()) continue;
            if (!string.IsNullOrEmpty(windowId) && mini.WindowId == windowId)
                return mini.GetComponent<T>();
        }

        foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null || !c.gameObject.scene.IsValid()) continue;
            if (!string.IsNullOrEmpty(nameContains) && c.gameObject.name.Contains(nameContains))
                return c;
        }

        return null;
    }
}
