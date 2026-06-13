using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Inline rename overlay — appears over the item row.
/// Audit fixes:
///   - Position now uses RectTransformUtility for correct canvas-space placement
///     instead of mixing world-space position with sizeDelta (broke at non-1:1 scales)
///   - onSubmit listener re-registered correctly each Show() call
///   - Submit() guard prevents double-fire on Escape + focus-loss in same frame
///   - [CRITICAL] Input.GetKeyDown / Input.GetMouseButton replaced with Input System
///     equivalents — legacy UnityEngine.Input throws InvalidOperationException every
///     frame when Player Settings is set to "Input System Package (New)".
/// </summary>
public sealed class FsRenameOverlay : MonoBehaviour
{
    private TMP_InputField _input;
    private Action<string> _onSubmit;
    private Action         _onCancel;
    private bool           _active;
    private bool           _submitFired; // prevents double-fire
    private int            _openedFrame;  // grace period — blocks focus-loss submit on open frame

    public void Init()
    {
        BuildInputGO();
        Hide();
    }

    public void Show(RectTransform anchorRT, string currentName,
                     Action<string> onSubmit, Action onCancel)
    {
        _onSubmit    = onSubmit;
        _onCancel    = onCancel;
        _active      = true;
        _submitFired = false;
        _openedFrame = Time.frameCount;

        // Position overlay in its own parent (FileExplorerApp) coordinate space.
        // We stay here — NOT re-parented into fileContent — because PopulateContent()
        // calls Destroy() on all fileContent children, which would destroy this overlay.
        // Use anchorRT.TransformPoint → InverseTransformPoint to convert the anchor's
        // world-space corners into our parent's local space. This correctly handles
        // any scroll offset in the viewport between the anchor row and this transform.
        var overlayRT = GetComponent<RectTransform>();
        if (overlayRT != null)
        {
            var myParentRT = transform.parent as RectTransform;

            // Convert anchor world position into our parent's local space
            Vector3 worldPos = anchorRT.TransformPoint(new Vector3(anchorRT.rect.xMin, anchorRT.rect.yMin, 0f));
            Vector2 localPos = myParentRT != null
                ? (Vector2)myParentRT.InverseTransformPoint(worldPos)
                : (Vector2)worldPos;

            overlayRT.anchorMin        = new Vector2(0f, 0f);
            overlayRT.anchorMax        = new Vector2(0f, 0f);
            overlayRT.pivot            = new Vector2(0f, 0f);
            overlayRT.anchoredPosition = localPos;
            overlayRT.sizeDelta        = new Vector2(anchorRT.rect.width, anchorRT.rect.height);
        }

        Debug.Log($"[Rename] Show() called. currentName={currentName} _input={(_input == null ? "NULL" : _input.name)} inputActive={_input?.gameObject.activeInHierarchy} inputInteractable={_input?.interactable} inputReadOnly={_input?.readOnly}");
        _input.text = currentName;
        _input.gameObject.SetActive(true);
        // FIX: ActivateInputField() silently fails when called on the same frame as
        // SetActive(true) — the EventSystem hasn't processed the activation yet.
        // Defer one frame via coroutine so keyboard focus actually registers.
        StartCoroutine(ActivateNextFrame(currentName));

        _input.onSubmit.RemoveAllListeners();
        _input.onSubmit.AddListener(_ => Submit());
    }

    public void Hide()
    {
        _active = false;
        if (_input != null) _input.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_active) return;

        // FIX: was Input.GetKeyDown(KeyCode.Escape) — legacy UnityEngine.Input throws
        // InvalidOperationException every frame when project uses Input System package.
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            _onCancel?.Invoke();
            Hide();
            return;
        }

        // FIX: was !Input.GetMouseButton(0) — same issue.
        // Guard: submit on focus-loss only when left mouse button is not held,
        // so a click that transfers focus doesn't double-fire immediately.
        // Grace period: never auto-submit on the same frame Show() was called.
        // The Rename button click releases the mouse on the same frame Update() runs,
        // so leftButton.isPressed=false and isFocused=false simultaneously — without
        // this guard, Submit() fires instantly before the player can type anything.
        if (Time.frameCount <= _openedFrame + 1) return;

        bool leftHeld = Mouse.current?.leftButton.isPressed ?? false;
        if (_input != null && !_input.isFocused && !leftHeld)
        {
            Submit();
        }
    }

    private System.Collections.IEnumerator ActivateNextFrame(string currentName)
    {
        yield return null;
        if (_input == null || !_active) yield break;
        _input.ActivateInputField();
        yield return null;
        Debug.Log($"[Rename] After ActivateInputField — isFocused={_input.isFocused} currentSelected={UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name ?? "NONE"}");

        // Select only the filename stem (text before the last dot) for files,
        // or the full text for folders/names without an extension.
        int selectEnd = currentName != null ? currentName.Length : 0;
        if (!string.IsNullOrEmpty(currentName))
        {
            int dot = currentName.LastIndexOf('.');
            // dot > 0 ensures we don't treat dotfiles (e.g. ".") as having an extension
            if (dot > 0) selectEnd = dot;
        }
        _input.selectionAnchorPosition = 0;
        _input.selectionFocusPosition  = selectEnd;
    }

    private void Submit()
    {
        if (!_active || _submitFired) return;
        _submitFired = true;
        Debug.Log($"[Rename] Submit() firing. val={(_input != null ? _input.text.Trim() : "NULL")}");

        string val = _input != null ? _input.text.Trim() : "";
        Hide();
        if (!string.IsNullOrEmpty(val)) _onSubmit?.Invoke(val);
        else                             _onCancel?.Invoke();
    }

    private void BuildInputGO()
    {
        if (GetComponent<RectTransform>() == null)
            gameObject.AddComponent<RectTransform>();

        var go = new GameObject("RenameInput",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.13f, 0.97f);

        var viewport = new GameObject("Viewport",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        var vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(6f, 2f); vpRT.offsetMax = new Vector2(-6f, -2f);
        viewport.GetComponent<Image>().color = Color.clear;

        var textGO = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(viewport.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize  = 13;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        _input = go.GetComponent<TMP_InputField>();
        _input.textViewport  = vpRT;
        _input.textComponent = tmp;
    }
}
