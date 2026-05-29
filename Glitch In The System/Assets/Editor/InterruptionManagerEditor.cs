#if UNITY_EDITOR
using GlitchInTheSystem.Interruptions;
using UnityEditor;
using UnityEngine;

namespace GlitchInTheSystem.Editor
{
    [CustomEditor(typeof(InterruptionManager))]
    public sealed class InterruptionManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (IsHiddenLegacyProperty(property.name))
                    continue;

                if (property.name == "minigameIntroClip")
                {
                    DrawIntroSectionHeader();
                    EditorGUILayout.PropertyField(property, new GUIContent("Intro Clip"));
                    continue;
                }

                if (property.name is "minigameIntroVolume" or "minigameIntroPostDelaySeconds")
                {
                    EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name == "interruptionLoadingRoot")
                {
                    DrawNotRespondingSectionHeader();
                    EditorGUILayout.PropertyField(property, new GUIContent("Loading Spinner Root"));
                    continue;
                }

                if (property.name == "loadingSpinnerDurationSeconds")
                {
                    EditorGUILayout.PropertyField(property, new GUIContent("Loading Duration (sec)"));
                    continue;
                }

                if (property.name == "bgmTrack1")
                {
                    DrawBgmSectionHeader();
                    EditorGUILayout.PropertyField(property, new GUIContent("BGM Track 1"), true);
                    continue;
                }

                if (property.name == "bgmTrack2")
                {
                    EditorGUILayout.PropertyField(property, new GUIContent("BGM Track 2"), true);
                    EditorGUILayout.Space(6);
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawNotRespondingSectionHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Not responding intro", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Assign InterruptionLoading (menu: Build Interruption Loading Spinner), or leave empty to auto-create at runtime.",
                MessageType.None);
        }

        private static void DrawIntroSectionHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Minigame Intro", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Order: intro audio → loading spinner → gray overlay → error popups → BGM + captcha.",
                MessageType.Info);
        }

        private static void DrawBgmSectionHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Minigame BGM (2 tracks)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Both tracks loop together during the captcha. Set an Audio Clip and Volume for each track below.",
                MessageType.Info);
        }

        private static bool IsHiddenLegacyProperty(string name)
        {
            return name.StartsWith("legacy") ||
                   name.StartsWith("minigameMusicSource");
        }
    }
}
#endif
