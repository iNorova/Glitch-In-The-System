using GlitchInTheSystem.Social;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ensures the single design template post when a scene opens. Keeps canvas on Overlay (Game view).
/// </summary>
[InitializeOnLoad]
internal static class SocialMediaFeedEditModeBootstrap
{
    static SocialMediaFeedEditModeBootstrap()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += (_, __) => ScheduleSetup();
        EditorApplication.delayCall += OnEditorLoad;
    }

    private static void OnEditorLoad()
    {
        SocialMediaCanvasSceneViewEditor.RestoreAll();
        ScheduleSetup();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.EnteredPlayMode
            || state == PlayModeStateChange.EnteredEditMode)
            SocialMediaCanvasSceneViewEditor.RestoreAll();

        if (state == PlayModeStateChange.EnteredEditMode)
            ScheduleSetup();
    }

    private static void ScheduleSetup()
    {
        if (Application.isPlaying) return;
        EditorApplication.delayCall += SetupSocialFeedsOnce;
    }

    private static void SetupSocialFeedsOnce()
    {
        if (Application.isPlaying) return;

        SocialMediaCanvasSceneViewEditor.RestoreAll();

        var controllers = Object.FindObjectsByType<SocialMediaFeedController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var controller in controllers)
        {
            if (controller == null) continue;

            SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(controller, rebuildAll: false, selectTemplate: false);

            controller.PrepareEditModeLayout();
        }
    }
}
