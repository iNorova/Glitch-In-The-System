using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene utility to clean up "The referenced script (Unknown) on this Behaviour is missing!" warnings.
/// </summary>
public static class MissingScriptTools
{
    [MenuItem("Glitch In The System/Tools/Remove Missing Scripts In Open Scenes", false, 200)]
    public static void RemoveMissingScriptsInOpenScenes()
    {
        int removedTotal = 0;
        int objectsScanned = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
                removedTotal += RemoveMissingRecursively(root, ref objectsScanned);

            if (removedTotal > 0)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"Missing script cleanup done. Removed: {removedTotal}, Objects scanned: {objectsScanned}.");
        EditorUtility.DisplayDialog(
            "Remove Missing Scripts",
            $"Removed {removedTotal} missing script components.\nScanned {objectsScanned} objects.",
            "OK");
    }

    private static int RemoveMissingRecursively(GameObject go, ref int objectsScanned)
    {
        objectsScanned++;
        int removed = 0;

        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (count > 0)
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            removed += RemoveMissingRecursively(t.GetChild(i).gameObject, ref objectsScanned);

        return removed;
    }
}
