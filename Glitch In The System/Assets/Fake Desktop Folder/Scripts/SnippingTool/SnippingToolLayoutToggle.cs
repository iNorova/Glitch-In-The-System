using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to SnippingToolAppWindow/FloatingPanel.
/// Tick 'Edit Mode Enabled' in the Inspector (outside Play Mode only) to disable
/// layout groups so you can freely drag UI elements in the Scene View.
/// Untick to restore layout groups.
///
/// Zero runtime behaviour — no Awake, no lifecycle methods, no Play Mode hooks.
/// </summary>
[ExecuteAlways]
public sealed class SnippingToolLayoutToggle : MonoBehaviour
{
    [Header("Layout Edit Toggle  (Outside Play Mode only)")]
    [Tooltip("Tick: layout groups OFF — drag/resize freely in Scene View.\n" +
             "Untick: layout groups ON — responsive layout active.")]
    [SerializeField] private bool editModeEnabled = false;

    // OnValidate fires whenever the Inspector checkbox changes.
    // Hard-guarded: never runs during Play Mode.
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        bool on = !editModeEnabled;
        foreach (var g in GetComponentsInChildren<HorizontalLayoutGroup>(includeInactive: true))
            g.enabled = on;
        foreach (var g in GetComponentsInChildren<VerticalLayoutGroup>(includeInactive: true))
            g.enabled = on;
    }
}
