using GlitchInTheSystem.Interruptions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds captcha UI under <c>CaptchaMinigamePanel</c> and wires <see cref="CaptchaMinigame"/>.
/// </summary>
public static class CaptchaMinigamePanelBuilder
{
    private const string MenuPath = "Glitch In The System/UI/Build Captcha Minigame Panel";

    [MenuItem(MenuPath, false, 12)]
    public static void BuildCaptchaPanel()
    {
        var panel = FindCaptchaPanel();
        if (panel == null)
        {
            EditorUtility.DisplayDialog(
                "Build Captcha Panel",
                "Select CaptchaMinigamePanel in the Hierarchy (under InterruptionOverlay → MinigameRoot), then run this again.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Captcha Minigame Panel");

        ClearOldCaptchaChildren(panel);

        var panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0f, 0f, 0f, 0.55f);
            panelImage.raycastTarget = true;
        }

        Stretch(panel);

        var window = CreatePanel("CaptchaWindow", panel, new Color(0.18f, 0.20f, 0.24f, 0.98f));
        SetCentered(window, 520f, 300f);

        CreateTMP("Title", window, "SECURITY VERIFICATION", 22, TextAlignmentOptions.Top);
        SetAnchored(window, "Title", new Vector2(0f, -28f), new Vector2(480f, 36f));

        var captchaText = CreateTMP("CaptchaText", window, "AB12C", 40, TextAlignmentOptions.Center);
        SetAnchored(captchaText.rectTransform, new Vector2(0f, 30f), new Vector2(420f, 56f));
        captchaText.fontStyle = FontStyles.Bold;
        captchaText.color = new Color(0.75f, 0.95f, 0.85f, 1f);

        var timerText = CreateTMP("TimerText", window, "12", 28, TextAlignmentOptions.TopRight);
        SetAnchored(timerText.rectTransform, new Vector2(200f, 118f), new Vector2(80f, 40f));
        timerText.color = new Color(1f, 0.85f, 0.45f, 1f);

        var inputField = CreateTmpInputField(window, "CaptchaInput");
        SetAnchored(inputField.GetComponent<RectTransform>(), new Vector2(0f, -35f), new Vector2(380f, 44f));

        var submitButton = CreateButton(window, "SubmitButton", "SUBMIT", new Color(0.55f, 0.18f, 0.18f, 1f));
        SetAnchored(submitButton.GetComponent<RectTransform>(), new Vector2(0f, -105f), new Vector2(200f, 46f));

        var captcha = panel.GetComponent<CaptchaMinigame>();
        if (captcha == null)
            captcha = Undo.AddComponent<CaptchaMinigame>(panel.gameObject);

        var so = new SerializedObject(captcha);
        so.FindProperty("captchaText").objectReferenceValue = captchaText;
        so.FindProperty("inputField").objectReferenceValue = inputField;
        so.FindProperty("submitButton").objectReferenceValue = submitButton;
        so.FindProperty("timerText").objectReferenceValue = timerText;
        so.ApplyModifiedPropertiesWithoutUndo();

        FixMinigameRootStretch(panel);

        EditorUtility.SetDirty(panel.gameObject);
        Undo.CollapseUndoOperations(group);

        Debug.Log("[CaptchaMinigamePanelBuilder] Captcha UI built and wired on " + panel.name);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateBuild() => FindCaptchaPanel() != null;

    private static RectTransform FindCaptchaPanel()
    {
        if (Selection.activeGameObject != null)
        {
            var onSelection = Selection.activeGameObject.GetComponent<CaptchaMinigame>();
            if (onSelection != null)
                return onSelection.GetComponent<RectTransform>();

            if (Selection.activeGameObject.name == "CaptchaMinigamePanel")
                return Selection.activeGameObject.GetComponent<RectTransform>();
        }

        var found = Object.FindFirstObjectByType<CaptchaMinigame>(FindObjectsInactive.Include);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    private static void ClearOldCaptchaChildren(RectTransform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            var child = panel.GetChild(i);
            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static void FixMinigameRootStretch(RectTransform panel)
    {
        var root = panel.parent as RectTransform;
        if (root == null || root.name != "MinigameRoot") return;

        Stretch(root);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create Captcha UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        go.GetComponent<Image>().color = bg;
        return rt;
    }

    private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, string text, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create Captcha UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create Captcha UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        go.GetComponent<Image>().color = bg;

        var text = CreateTMP("Label", rt, label, 18, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.raycastTarget = false;

        return go.GetComponent<Button>();
    }

    private static TMP_InputField CreateTmpInputField(RectTransform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        Undo.RegisterCreatedObjectUndo(root, "Create Captcha UI");
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        rootRt.localScale = Vector3.one;
        root.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.13f, 1f);

        var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        Undo.RegisterCreatedObjectUndo(textArea, "Create Captcha UI");
        var textAreaRt = textArea.GetComponent<RectTransform>();
        textAreaRt.SetParent(rootRt, false);
        Stretch(textAreaRt);
        textAreaRt.offsetMin = new Vector2(12f, 8f);
        textAreaRt.offsetMax = new Vector2(-12f, -8f);

        var placeholder = CreateTMP("Placeholder", textAreaRt, "Enter code...", 20, TextAlignmentOptions.MidlineLeft);
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        Stretch(placeholder.rectTransform);

        var text = CreateTMP("Text", textAreaRt, "", 22, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform);

        var input = root.GetComponent<TMP_InputField>();
        input.textViewport = textAreaRt;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = text.font;
        input.pointSize = 22;
        input.caretColor = Color.white;

        return input;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static void SetCentered(RectTransform rt, float width, float height)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void SetAnchored(RectTransform parent, string childName, Vector2 pos, Vector2 size)
    {
        var child = parent.Find(childName) as RectTransform;
        if (child != null)
            SetAnchored(child, pos, size);
    }

    private static void SetAnchored(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
