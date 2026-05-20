#if UNITY_EDITOR
using GlitchInTheSystem.Algorithm;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AlgorithmTrustSettings))]
public sealed class AlgorithmTrustSettingsEditor : Editor
{
    private SerializedProperty _startingTrust;
    private SerializedProperty _startingStress;
    private SerializedProperty _passiveMin;
    private SerializedProperty _assertiveMin;
    private SerializedProperty _passiveProfile;
    private SerializedProperty _assertiveProfile;
    private SerializedProperty _aggressiveProfile;
    private SerializedProperty _hesitation;
    private SerializedProperty _stressHigh;
    private SerializedProperty _maxDay;

    private void OnEnable()
    {
        _startingTrust = serializedObject.FindProperty("startingTrust");
        _startingStress = serializedObject.FindProperty("startingStressLevel");
        _passiveMin = serializedObject.FindProperty("passiveTrustMin");
        _assertiveMin = serializedObject.FindProperty("assertiveTrustMin");
        _passiveProfile = serializedObject.FindProperty("passiveProfile");
        _assertiveProfile = serializedObject.FindProperty("assertiveProfile");
        _aggressiveProfile = serializedObject.FindProperty("aggressiveProfile");
        _hesitation = serializedObject.FindProperty("hesitationSeconds");
        _stressHigh = serializedObject.FindProperty("stressHighThreshold");
        _maxDay = serializedObject.FindProperty("maxManipulationDay");
    }

    public override void OnInspectorGUI()
    {
        var settings = (AlgorithmTrustSettings)target;
        serializedObject.Update();

        EditorGUILayout.LabelField("Algorithm trust (edit anytime — no Play mode)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(_startingTrust, new GUIContent("Starting trust (session start)"));
        EditorGUILayout.PropertyField(_startingStress, new GUIContent("Starting stress"));

        DrawTrustBandPreview(settings);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Fallback trust thresholds", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Used when no State Profile range matches. Typical: Passive ≥ 67, Assertive 34–66, Aggressive 0–33.",
            MessageType.None);

        EditorGUILayout.PropertyField(_passiveMin, new GUIContent("Passive from trust ≥"));
        EditorGUILayout.PropertyField(_assertiveMin, new GUIContent("Assertive from trust ≥"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("State profiles", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_passiveProfile);
        EditorGUILayout.PropertyField(_assertiveProfile);
        EditorGUILayout.PropertyField(_aggressiveProfile);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_hesitation);
        EditorGUILayout.PropertyField(_stressHigh);
        EditorGUILayout.PropertyField(_maxDay);

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(settings);
    }

    private static void DrawTrustBandPreview(AlgorithmTrustSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            float trust = settings.startingTrust;
            var state = settings.ResolveBehaviourState(trust);
            EditorGUILayout.LabelField("Preview at starting trust", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Mood: {state}", EditorStyles.largeLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick preview (move slider, no Play)", EditorStyles.miniLabel);
            float preview = EditorGUILayout.Slider("Preview trust", trust, 0f, 100f);
            if (!Mathf.Approximately(preview, trust))
            {
                EditorGUILayout.LabelField($"→ {settings.ResolveBehaviourState(preview)}");
            }

            DrawBandBar(settings);
        }
    }

    private static void DrawBandBar(AlgorithmTrustSettings settings)
    {
        Rect r = GUILayoutUtility.GetRect(18, 22);
        EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));

        float passive = settings.passiveTrustMin / 100f;
        float assertive = settings.assertiveTrustMin / 100f;

        DrawSegment(r, 0f, assertive, new Color(0.75f, 0.25f, 0.25f, 0.9f));
        DrawSegment(r, assertive, passive, new Color(0.85f, 0.65f, 0.2f, 0.9f));
        DrawSegment(r, passive, 1f, new Color(0.25f, 0.65f, 0.35f, 0.9f));

        float marker = Mathf.Clamp01(settings.startingTrust / 100f);
        float x = r.x + r.width * marker;
        EditorGUI.DrawRect(new Rect(x - 1f, r.y, 2f, r.height), Color.white);
    }

    private static void DrawSegment(Rect bar, float startNorm, float endNorm, Color color)
    {
        float w = bar.width * (endNorm - startNorm);
        var seg = new Rect(bar.x + bar.width * startNorm, bar.y, w, bar.height);
        EditorGUI.DrawRect(seg, color);
    }
}
#endif
