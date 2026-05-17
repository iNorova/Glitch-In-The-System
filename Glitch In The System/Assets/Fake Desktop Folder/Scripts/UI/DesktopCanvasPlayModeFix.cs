using UnityEngine;

/// <summary>
/// Safety net: keeps the desktop on Screen Space Overlay during Play (prevents blue Game view if canvas was left on Scene-view camera mode).
/// Add to the FakeDesktop object with the Canvas.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class DesktopCanvasPlayModeFix : MonoBehaviour
{
    private void Awake()
    {
        if (!Application.isPlaying) return;
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
    }
}
