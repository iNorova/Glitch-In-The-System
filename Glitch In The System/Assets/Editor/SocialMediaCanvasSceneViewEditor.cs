using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restores desktop canvas to Screen Space Overlay (fixes gray Game view after old scene-view experiments).
/// </summary>
[InitializeOnLoad]
internal static class SocialMediaCanvasSceneViewEditor
{
    private struct CanvasSnapshot
    {
        public RenderMode renderMode;
        public Camera worldCamera;
        public float planeDistance;
    }

    private static readonly Dictionary<int, CanvasSnapshot> Snapshots = new();

    static SocialMediaCanvasSceneViewEditor()
    {
        EditorApplication.playModeStateChanged += _ => RestoreAll();
        EditorApplication.quitting += RestoreAll;
        EditorApplication.delayCall += RestoreAll;
    }

    public static void Restore(Canvas canvas)
    {
        if (canvas == null) return;

        int id = canvas.GetInstanceID();
        if (!Snapshots.TryGetValue(id, out var snap))
        {
            ForceOverlay(canvas);
            return;
        }

        canvas.renderMode = snap.renderMode;
        canvas.worldCamera = snap.worldCamera;
        canvas.planeDistance = snap.planeDistance;
        Snapshots.Remove(id);
    }

    public static void RestoreAll()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas == null) continue;
            if (Snapshots.ContainsKey(canvas.GetInstanceID()))
                Restore(canvas);
            else
                ForceOverlay(canvas);
        }

        Snapshots.Clear();
        Canvas.ForceUpdateCanvases();
    }

    public static void ForceOverlay(Canvas canvas)
    {
        if (canvas == null) return;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
    }

    [MenuItem("Glitch In The System/UI/Social Media/Fix Game View (Gray Screen)", false, 12)]
    public static void FixGameViewMenu()
    {
        RestoreAll();
        EditorUtility.DisplayDialog(
            "Game view fixed",
            "Canvas reset to normal mode. Use the Game tab (Play off) to see your UI.",
            "OK");
    }
}
