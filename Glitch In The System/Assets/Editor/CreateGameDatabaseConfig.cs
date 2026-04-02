using UnityEditor;
using UnityEngine;
using GlitchInTheSystem.GameData;

public static class CreateGameDatabaseConfig
{
    [MenuItem("Glitch In The System/Create Game Database Config", false, 0)]
    public static void Create()
    {
        var config = ScriptableObject.CreateInstance<GameDatabaseConfig>();
        config.postsPerDay = 10;
        config.currentDay = 1;
        config.algorithmPhase = 0;

        string path = "Assets/GameData/GameDatabaseConfig.asset";
        var folder = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = config;
    }
}
