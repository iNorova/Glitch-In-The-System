#if UNITY_EDITOR
using GlitchInTheSystem.Algorithm;
using UnityEditor;
using UnityEngine;

public static class AlgorithmTrustSettingsMenu
{
    private const string DefaultFolder = "Assets/Fake Desktop Folder/GameData";

    [MenuItem("Glitch In The System/Algorithm/Create Trust Settings Asset", false, 20)]
    public static void CreateTrustSettingsAsset()
    {
        EnsureFolderExists("Assets/Fake Desktop Folder", "GameData");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/AlgorithmTrustSettings.asset");
        var asset = ScriptableObject.CreateInstance<AlgorithmTrustSettings>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    [MenuItem("Glitch In The System/Algorithm/Select Trust Settings In Scene", false, 21)]
    public static void SelectBootstrapSettings()
    {
        var bootstrap = Object.FindFirstObjectByType<GlitchInTheSystem.GameData.GameBootstrap>(FindObjectsInactive.Include);
        if (bootstrap == null || bootstrap.AlgorithmTrustSettings == null)
        {
            EditorUtility.DisplayDialog(
                "Algorithm trust",
                "No GameBootstrap in scene, or Algorithm Trust Settings not assigned on it.",
                "OK");
            return;
        }

        Selection.activeObject = bootstrap.AlgorithmTrustSettings;
        EditorGUIUtility.PingObject(bootstrap.AlgorithmTrustSettings);
    }

    private static void EnsureFolderExists(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder("Assets", "Fake Desktop Folder");
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
