using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Clears runtime UnityEvent listeners. Scene persistent onClick entries must be cleared in the scene or via editor.
/// </summary>
public static class DesktopUIButtonWiring
{
    public static void SetSingleClickListener(Button button, UnityAction handler)
    {
        if (button == null || handler == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(handler);
    }
}
