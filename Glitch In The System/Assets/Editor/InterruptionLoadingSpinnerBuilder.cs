#if UNITY_EDITOR
using GlitchInTheSystem.Interruptions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        Transform existing = fakeDesktop.transform.Find("InterruptionLoading");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var root = new GameObject(
            "InterruptionLoading",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Create InterruptionLoading");
        root.transform.SetParent(fakeDesktop.transform, false);
        root.transform.SetAsLastSibling();
        Stretch(root.GetComponent<RectTransform>());

        var rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0f);
        rootImage.raycastTarget = true;

        var iconGo = new GameObject(
            "LoadingIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(InterruptionLoadingSpinner));
        Undo.RegisterCreatedObjectUndo(iconGo, "Create LoadingIcon");
        iconGo.transform.SetParent(root.transform, false);

        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = new Vector2(52f, 52f);

        var image = iconGo.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        image.color = new Color(0.35f, 0.35f, 0.38f, 1f);
        image.raycastTarget = false;

        root.SetActive(false);

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
            "[InterruptionLoadingSpinnerBuilder] Created InterruptionLoading under FakeDesktop. " +
            "It shows after intro audio, then the gray overlay enables.");
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
#endif
