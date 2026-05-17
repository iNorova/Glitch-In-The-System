using GlitchInTheSystem.Social;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SocialMediaFeedController))]
public sealed class SocialMediaFeedControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var controller = (SocialMediaFeedController)target;
        if (controller == null || Application.isPlaying) return;

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Edit feed (Play off)", EditorStyles.boldLabel);

            if (controller.IsEditModeFreeformLayout)
            {
                EditorGUILayout.HelpBox(
                    "Freeform mode ON — Rect Transform Pos X/Y, Width, Height are unlocked. " +
                    "Use the Rect Tool on posts, BodyText, CommentsPanel, etc.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "One design post: EditorFeedPost_Template. Put sprites on CommentsPanel etc.\n" +
                    "Play clones it for every feed post (text comes from the game).\n" +
                    "Unlock layout to edit Pos/Size freely.",
                    MessageType.Info);
            }

            if (GUILayout.Button("Create design template post"))
            {
                SocialMediaCanvasSceneViewEditor.RestoreAll();
                SocialMediaFeedEditorUtility.EnsureDesignTemplatePost(controller, rebuildAll: true, selectTemplate: true);
                controller.PrepareEditModeLayout();
            }

            if (GUILayout.Button("Reset template layout (like original preview card)"))
                controller.RefreshDesignTemplateLayout();

            if (GUILayout.Button("Unlock layout (edit Pos X/Y, Width, Height)"))
                SocialMediaAppBuilder.UnlockLayoutForFreeformEditing(controller);

            if (GUILayout.Button("Fix Game View (gray screen)"))
                SocialMediaCanvasSceneViewEditor.RestoreAll();
        }
    }
}
