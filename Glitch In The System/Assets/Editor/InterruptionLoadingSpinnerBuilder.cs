#if UNITY_EDITOR
using GlitchInTheSystem.Interruptions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the centered loading spinner shown before the gray "not responding" overlay.
/// </summary>
public static class InterruptionLoadingSpinnerBuilder
{
    private const string MenuPath = "Glitch In The System/UI/Build Interruption Loading Spinner";

    [MenuItem(MenuPath, false, 11)]
    public static void BuildLoadingSpinner()
    {
        var fakeDesktop = GameObject.Find("FakeDesktop");
        if (fakeDesktop == null)
        {
            EditorUtility.DisplayDialog(
                "Build Loading Spinner",
                "Could not find FakeDesktop in the open scene.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Interruption Loading Spinner");

        Transform existing = fakeDesktop.transform.Find(InterruptionLoadingSpinnerFactory.RootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var root = InterruptionLoadingSpinnerFactory.Create(fakeDesktop.transform);
        Undo.RegisterCreatedObjectUndo(root, "Create InterruptionLoading");
        SetUiLayerRecursive(root);

        var manager = Object.FindFirstObjectByType<InterruptionManager>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("interruptionLoadingRoot").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;

        Debug.Log(
            "[InterruptionLoadingSpinnerBuilder] Created InterruptionLoading with ring spinner under FakeDesktop.");
    }

    private static void SetUiLayerRecursive(GameObject go)
    {
        go.layer = 5;
        foreach (Transform child in go.transform)
            SetUiLayerRecursive(child.gameObject);
    }
}
#endif
