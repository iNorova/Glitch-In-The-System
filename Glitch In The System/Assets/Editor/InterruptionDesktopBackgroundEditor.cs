#if UNITY_EDITOR
using GlitchInTheSystem.Interruptions;
using UnityEditor;
using UnityEngine;

namespace GlitchInTheSystem.Editor
{
    [CustomEditor(typeof(InterruptionDesktopBackground))]
    public sealed class InterruptionDesktopBackgroundEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Each flicker: wait on normal (Delay Before), show inverted (Inverted Hold), then back to normal " +
                "until the next step. After the last flicker, inverted stays on for the interruption overlay.",
                MessageType.Info);

            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
