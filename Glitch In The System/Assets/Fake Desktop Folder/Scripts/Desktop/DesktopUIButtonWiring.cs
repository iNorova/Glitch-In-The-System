using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Replaces Button click handlers, including serialized Inspector listeners.
/// </summary>
public static class DesktopUIButtonWiring
{
    public static void SetSingleClickListener(Button button, UnityAction handler)
    {
        if (button == null || handler == null) return;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(handler);
    }
}
