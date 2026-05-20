#if UNITY_EDITOR
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.GameData;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameBootstrap))]
public sealed class GameBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var bootstrap = (GameBootstrap)target;
        var settings = bootstrap.AlgorithmTrustSettings;

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Algorithm trust (no Play mode)", EditorStyles.boldLabel);

            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign Algorithm Trust Settings, or create one below. Edit trust and mood bands in that asset.",
                    MessageType.Info);

                if (GUILayout.Button("Create Algorithm Trust Settings asset"))
                    CreateAndAssignSettings(bootstrap);
            }
            else
            {
                EditorGUILayout.LabelField("Starting trust", settings.startingTrust.ToString("0"));
                EditorGUILayout.LabelField("Mood at start", settings.ResolveBehaviourState(settings.startingTrust).ToString());

                if (GUILayout.Button("Open Trust Settings asset"))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
        }
    }

    private static void CreateAndAssignSettings(GameBootstrap bootstrap)
    {
        const string folder = "Assets/Fake Desktop Folder/GameData";
        if (!AssetDatabase.IsValidFolder("Assets/Fake Desktop Folder"))
        {
            Debug.LogWarning("Expected folder Assets/Fake Desktop Folder/GameData — creating asset in Assets/.");
        }

        string path = $"{folder}/AlgorithmTrustSettings.asset";
        if (!System.IO.Directory.Exists(folder))
            path = "Assets/AlgorithmTrustSettings.asset";

        var asset = ScriptableObject.CreateInstance<AlgorithmTrustSettings>();
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
        AssetDatabase.SaveAssets();

        bootstrap.SetAlgorithmTrustSettings(asset);
        EditorUtility.SetDirty(bootstrap);
        Selection.activeObject = asset;
    }
}
#endif
