using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Single row — folder or file.
/// Upgrade: drag-and-drop interfaces added. Folder rows act as drop targets.
/// No per-frame cost: all state is event-driven.
/// </summary>
public sealed class FsItemView : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI typeLabel;  // cached — avoids GetChild(2).GetComponent per PopulateContent
    [SerializeField] private TextMeshProUGUI dateLabel;  // cached date-modified column
    [SerializeField] private Image           background;

    private FileExplorerManager.FsEntry _entry;
    private bool                      _selected;
    private bool                      _isDragging;

    // ── Inline rename ──────────────────────────────────────────────────────
    private TMP_InputField _inlineInput;   // built once, reused
    private Action<string> _renameSubmit;
    private Action         _renameCancel;
    private bool           _renaming;
    private bool           _renameFired;
    private int            _renameOpenFrame;
    private UnityEngine.Events.UnityAction<string> _submitListener; // FIX-2: cached delegate

    // Callbacks wired by FileExplorerApp
    public Action<FsItemView>                OnSingleClick;
    public Action<FileExplorerManager.FsEntry> OnDoubleClick;
    public Action<FsItemView, Vector2>       OnRightClick;
    /// <summary>Called when this item is dropped onto a folder target. Arg = target folder path.</summary>
    public Action<FsItemView, string>        OnDroppedOnto;
    /// <summary>Called when another item is dropped onto THIS folder.</summary>
    public Action<FsItemView, FsItemView>    OnReceivedDrop;

    // Background colors — static so one allocation serves all instances
    private static readonly Color BgNormal      = new Color(1f,    1f,    1f,    0.00f);
    private static readonly Color BgHover       = new Color(1f,    1f,    1f,    0.06f);
    private static readonly Color BgSelected    = new Color(0.28f, 0.52f, 0.88f, 0.28f); // slightly softer blue
    private static readonly Color BgDropTarget  = new Color(0.30f, 0.70f, 0.40f, 0.30f);
    private static readonly Color BgPressed     = new Color(1f,    1f,    1f,    0.13f); // press darkening
    private static readonly Color BgDblFlash    = new Color(0.45f, 0.65f, 1.00f, 0.22f); // double-click flash

    // Fade coroutine tracking — cancel stale fades on pool reuse
    private System.Collections.IEnumerator _bgFadeRoutine;
    private const float HoverFadeTime = 0.07f; // seconds

    private float _lastClickTime = -1f;
    private const float DblClickInterval = 0.45f; // BATCH 1: was 0.35 — 0.45 feels more like Windows

    // Drag ghost — a faded clone of the name label that follows the cursor
    private static GameObject            _dragGhost;
    private static RectTransform         _dragGhostRT;   // PERF: cached — avoids GetComponent per drag frame
    private static TMPro.TextMeshProUGUI _dragGhostTMP;  // PERF: cached — avoids GetComponent per drag frame
    private static FsItemView  _draggingItem;
    private static Canvas      _rootCanvas;

    /// <summary>True while any FsItemView is being dragged. Read by SidebarFolderButton.</summary>
    public static bool IsDragging => _draggingItem != null;

    /// <summary>The FsItemView currently being dragged, or null. Used by SidebarFolderButton.OnDrop()
    /// so sidebar drops work without requiring a prior single-click selection.</summary>
    public static FsItemView DraggingItem => _draggingItem;

    /// <summary>Clear all static drag references. Call from FileExplorerApp.OnDisable() to prevent
    /// stale references after the window is closed or the scene is reloaded.</summary>
    public static void ClearDragStatics()
    {
        if (_dragGhost != null) { _dragGhost.SetActive(false); }
        _dragGhost    = null;
        _dragGhostRT  = null;
        _dragGhostTMP = null;
        _rootCanvas   = null;
        _draggingItem = null;
    }

    public void Init(FileExplorerManager.FsEntry entry, Sprite folderIcon, Sprite fileIcon)
    {
        _entry = entry;
        if (nameLabel  != null) nameLabel.text   = entry.name;
        if (iconImage  != null) iconImage.sprite  =
            entry.type == FileExplorerManager.EntryType.Folder ? folderIcon : fileIcon;
        if (background != null) background.color  = BgNormal;
    }

    public void Rebind(FileExplorerManager.FsEntry entry, Sprite folderIcon, Sprite fileIcon,
                       Color iconColor)
    {
        _entry         = entry;
        _selected      = false;
        _lastClickTime = -1f;
        // Cancel any in-flight hover/press/flash fade so pooled rows start clean
        if (_bgFadeRoutine != null) { StopCoroutine(_bgFadeRoutine); _bgFadeRoutine = null; }
        if (nameLabel  != null) nameLabel.text   = entry.name;
        if (dateLabel  != null) dateLabel.text   = FormatDate(entry.lastModified);
        if (background != null) { background.color = BgNormal; }
        if (nameLabel  != null) nameLabel.color  = new Color(0.90f, 0.88f, 0.84f, 1f); // restore text color
        if (iconImage  != null)
        {
            bool hasSprite = entry.type == FileExplorerManager.EntryType.Folder
                ? folderIcon != null : fileIcon != null;
            if (hasSprite)
            {
                iconImage.sprite          = entry.type == FileExplorerManager.EntryType.Folder
                    ? folderIcon : fileIcon;
                iconImage.color           = Color.white;
                iconImage.type            = UnityEngine.UI.Image.Type.Simple;
                iconImage.preserveAspect  = true;
                // Re-center RT in its 16x16 slot on every rebind (pool reuse can leave stale values)
                var rt = iconImage.rectTransform;
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(16f, 16f);
            }
            else
            {
                iconImage.sprite          = null;
                iconImage.color           = iconColor;
                iconImage.preserveAspect  = false;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_bgFadeRoutine != null) { StopCoroutine(_bgFadeRoutine); _bgFadeRoutine = null; }
        if (background != null)
            background.color = selected ? BgSelected : BgNormal;
        // Emphasize name text when selected
        if (nameLabel != null)
            nameLabel.color = selected
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.90f, 0.88f, 0.84f, 1f);
    }

    public void RefreshName(string newName)
    {
        if (nameLabel != null) nameLabel.text = newName;
    }

    public FileExplorerManager.FsEntry Entry => _entry;

    public void SetRefs(Image bg, Image icon, TextMeshProUGUI label,
                         TextMeshProUGUI typeLbl = null, TextMeshProUGUI dateLbl = null)
    {
        background = bg;
        iconImage  = icon;
        nameLabel  = label;
        typeLabel  = typeLbl;
        dateLabel  = dateLbl;
    }

    /// <summary>Update the type column label. Uses cached typeLabel — no GetComponent.</summary>
    public void SetTypeLabel(string text) { if (typeLabel != null) typeLabel.text = text; }

    /// <summary>Update the date column label. Uses cached dateLabel — no GetComponent.</summary>
    public void SetDateLabel(string text) { if (dateLabel != null) dateLabel.text = text; }

    private static string FormatDate(System.DateTime dt)
    {
        if (dt == default) return "—";
        return dt.ToString("dd/MM/yyyy  HH:mm");
    }

    // ── Click handling ────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_renaming) return;
        // Slight press darkening — not applied if a drag is already active
        if (!_isDragging && background != null)
            background.color = _selected
                ? Color.Lerp(BgSelected, BgPressed, 0.4f)
                : BgPressed;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_renaming || _isDragging) return;
        // Restore from press — hover or selected state
        if (background != null)
            background.color = _selected ? BgSelected : BgHover;
    }

    public void OnPointerClick(PointerEventData e)
    {
        // Suppress click if it ended a drag
        if (_isDragging) return;

        if (e.button == PointerEventData.InputButton.Right)
        {
            SetSelected(true);
            OnSingleClick?.Invoke(this);
            OnRightClick?.Invoke(this, e.position);
            return;
        }

        if (e.button != PointerEventData.InputButton.Left) return;

        float now = Time.unscaledTime;
        bool  dbl = (now - _lastClickTime) < DblClickInterval;
        _lastClickTime = now;

        if (dbl)
        {
            // Brief visual confirmation before open
            if (_bgFadeRoutine != null) { StopCoroutine(_bgFadeRoutine); _bgFadeRoutine = null; }
            _bgFadeRoutine = DblClickFlash();
            StartCoroutine(_bgFadeRoutine);
            OnDoubleClick?.Invoke(_entry);
        }
        else     { SetSelected(true); OnSingleClick?.Invoke(this); }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_draggingItem != null && _draggingItem != this &&
            _entry.type == FileExplorerManager.EntryType.Folder)
        {
            if (background != null) background.color = BgDropTarget;
            return;
        }
        if (!_selected)
            StartBgFade(BgHover, HoverFadeTime);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_selected)
            StartBgFade(BgNormal, HoverFadeTime);
    }

    private void StartBgFade(Color target, float duration)
    {
        if (_bgFadeRoutine != null) StopCoroutine(_bgFadeRoutine);
        _bgFadeRoutine = BgFadeRoutine(target, duration);
        StartCoroutine(_bgFadeRoutine);
    }

    private System.Collections.IEnumerator BgFadeRoutine(Color target, float duration)
    {
        if (background == null) yield break;
        var start   = background.color;
        float t     = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            if (background != null)
                background.color = Color.Lerp(start, target, t);
            yield return null;
        }
        if (background != null) background.color = target;
        _bgFadeRoutine = null;
    }

    // ── Drag handling ─────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;

        _isDragging   = true;
        _draggingItem = this;

        // Find root canvas once
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();

        // Build drag ghost — a lightweight label that follows the mouse
        if (_dragGhost == null && _rootCanvas != null)
        {
            _dragGhost = new GameObject("DragGhost",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            _dragGhost.transform.SetParent(_rootCanvas.transform, false);

            var rt = _dragGhost.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 28f);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            _dragGhostRT = rt; // cache once — reused in OnDrag every frame

            var tmp = _dragGhost.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text          = _entry.name;
            tmp.fontSize      = 13;
            tmp.color         = new Color(0.90f, 0.88f, 0.84f, 0.75f);
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            _dragGhostTMP = tmp; // cache once

            // Bring ghost to front
            _dragGhost.transform.SetAsLastSibling();
        }
        else if (_dragGhost != null)
        {
            // Reuse — just update label
            var tmp = _dragGhostTMP; // cached — no GetComponent
            if (tmp != null) tmp.text = _entry.name;
            _dragGhost.SetActive(true);
            _dragGhost.transform.SetAsLastSibling();
        }

        // Dim self while dragging
        if (background != null) background.color = new Color(1f, 1f, 1f, 0.03f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_isDragging || _dragGhost == null || _rootCanvas == null) return;

        // Move ghost to cursor position in canvas space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            e.position,
            _rootCanvas.worldCamera,
            out var localPos);

        if (_dragGhostRT != null) _dragGhostRT.anchoredPosition = localPos + new Vector2(12f, -8f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        _isDragging   = false;
        _draggingItem = null;

        if (_dragGhost != null) _dragGhost.SetActive(false);

        // Restore visual
        if (background != null)
            background.color = _selected ? BgSelected : BgNormal;
    }

    // ── Inline rename entry points ─────────────────────────────────────────

    /// <summary>
    /// Replace the name label with an in-place TMP_InputField.
    /// Stem-only selection for files; full selection for folders.
    /// </summary>
    public void BeginInlineRename(Action<string> onSubmit, Action onCancel)
    {
        if (_renaming) return;
        _renameSubmit    = onSubmit;
        _renameCancel    = onCancel;
        _renaming        = true;
        _renameFired     = false;
        _renameOpenFrame = Time.frameCount;

        EnsureInlineInput();
        SyncInlineInputRect();       // FIX-1: re-copy RT from current nameLabel (pooled rows)

        _inlineInput.text = _entry.name;
        if (nameLabel != null) nameLabel.alpha = 0f;
        _inlineInput.gameObject.SetActive(true);

        // FIX-2: ensure exactly one submit listener — unsubscribe before subscribe
        _inlineInput.onSubmit.RemoveAllListeners();
        _inlineInput.onSubmit.AddListener(_submitListener ??= _ => SubmitInlineRename());

        StartCoroutine(ActivateInlineInput(_entry.name));
    }

    /// <summary>Cancel rename without saving — restores nameLabel.</summary>
    public void CancelInlineRename()
    {
        if (!_renaming) return;
        _renaming = false;
        if (_inlineInput != null) _inlineInput.gameObject.SetActive(false);
        if (nameLabel    != null) nameLabel.alpha = 1f;
        _renameCancel?.Invoke();
    }

    private void SubmitInlineRename()
    {
        if (!_renaming || _renameFired) return;
        _renameFired = true;
        _renaming    = false;

        string val = _inlineInput != null ? _inlineInput.text.Trim() : string.Empty;
        if (_inlineInput != null) _inlineInput.gameObject.SetActive(false);
        if (nameLabel    != null) nameLabel.alpha = 1f;

        if (!string.IsNullOrEmpty(val)) _renameSubmit?.Invoke(val);
        else                             _renameCancel?.Invoke();
    }

    private void Update()
    {
        if (!_renaming) return;

        // FIX-1: abort if row was disabled or input destroyed during rename
        if (_inlineInput == null || !gameObject.activeInHierarchy)
        {
            _renaming    = false;
            _renameFired = false;
            _renameCancel?.Invoke();
            return;
        }

        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            CancelInlineRename();
            return;
        }

        // Submit on focus-loss (click outside) with 2-frame grace period
        if (Time.frameCount <= _renameOpenFrame + 1) return;
        bool leftHeld = Mouse.current?.leftButton.isPressed ?? false;
        if (_inlineInput != null && !_inlineInput.isFocused && !leftHeld)
            SubmitInlineRename();
    }

    private System.Collections.IEnumerator ActivateInlineInput(string currentName)
    {
        yield return null;
        if (_inlineInput == null || !_renaming) yield break;
        _inlineInput.ActivateInputField();
        yield return null;

        // Stem-only selection for files; full for folders
        int selectEnd = currentName != null ? currentName.Length : 0;
        if (!string.IsNullOrEmpty(currentName))
        {
            int dot = currentName.LastIndexOf('.');
            if (dot > 0) selectEnd = dot;
        }
        _inlineInput.selectionAnchorPosition = 0;
        _inlineInput.selectionFocusPosition  = selectEnd;
    }

    private void EnsureInlineInput()
    {
        if (_inlineInput != null) return;

        // Build the TMP_InputField to exactly overlay the nameLabel rect
        if (nameLabel == null) return;
        var nameLabelRT = nameLabel.rectTransform;

        var go = new GameObject("InlineRenameInput",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(nameLabelRT.parent, false);

        // Ignore HLG so it doesn't treat this as a new column item
        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // Copy exact anchors/offsets from nameLabel so it sits flush
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = nameLabelRT.anchorMin;
        rt.anchorMax        = nameLabelRT.anchorMax;
        rt.pivot            = nameLabelRT.pivot;
        rt.anchoredPosition = nameLabelRT.anchoredPosition;
        rt.sizeDelta        = nameLabelRT.sizeDelta;
        rt.offsetMin        = nameLabelRT.offsetMin;
        rt.offsetMax        = nameLabelRT.offsetMax;
        rt.SetSiblingIndex(nameLabel.transform.GetSiblingIndex());

        go.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.97f);

        // Viewport
        var vpGO = new GameObject("Viewport",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        vpGO.transform.SetParent(go.transform, false);
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4f, 1f); vpRT.offsetMax = new Vector2(-4f, -1f);
        vpGO.GetComponent<Image>().color = Color.clear;

        // Text component — match nameLabel's font settings
        var textGO = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(vpGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize      = nameLabel.fontSize;
        tmp.fontStyle     = nameLabel.fontStyle;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        if (nameLabel.font != null) tmp.font = nameLabel.font;

        _inlineInput                  = go.GetComponent<TMP_InputField>();
        _inlineInput.textViewport     = vpRT;
        _inlineInput.textComponent    = tmp;
        _inlineInput.caretColor       = Color.white;
        _inlineInput.selectionColor   = new Color(0.28f, 0.52f, 0.90f, 0.6f);

        go.SetActive(false);
    }

    /// <summary>FIX-1: Re-copy nameLabel RectTransform values into _inlineInput.
    /// Called every BeginInlineRename() so pooled rows always align correctly.</summary>
    private void SyncInlineInputRect()
    {
        if (_inlineInput == null || nameLabel == null) return;
        var src = nameLabel.rectTransform;
        var dst = _inlineInput.GetComponent<RectTransform>();
        dst.anchorMin        = src.anchorMin;
        dst.anchorMax        = src.anchorMax;
        dst.pivot            = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta        = src.sizeDelta;
        dst.offsetMin        = src.offsetMin;
        dst.offsetMax        = src.offsetMax;
        dst.SetSiblingIndex(nameLabel.transform.GetSiblingIndex());
    }

    private System.Collections.IEnumerator DblClickFlash()
    {
        if (background == null) yield break;
        var original = background.color;
        background.color = BgDblFlash;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.12f;
            if (background != null)
                background.color = Color.Lerp(BgDblFlash, BgNormal, t);
            yield return null;
        }
        if (background != null) background.color = BgNormal;
        _bgFadeRoutine = null;
    }

    // ── Drop target (folders only) ────────────────────────────────────────
    public void OnDrop(PointerEventData e)
    {
        // Clear drop highlight
        if (!_selected && background != null) background.color = BgNormal;

        if (_entry.type != FileExplorerManager.EntryType.Folder) return;
        if (_draggingItem == null || _draggingItem == this)    return;
        if (_draggingItem._entry.fullPath == _entry.fullPath)  return;

        // Prevent dropping a folder into its own descendant
        if (_entry.fullPath.StartsWith(_draggingItem._entry.fullPath + "/",
            StringComparison.Ordinal)) return;

        OnReceivedDrop?.Invoke(this, _draggingItem);
    }

    private void OnDisable()
    {
        // FIX-1: clean up rename state when row is pooled/hidden mid-rename
        if (_renaming)
        {
            _renaming    = false;
            _renameFired = false;
            if (_inlineInput != null) _inlineInput.gameObject.SetActive(false);
            if (nameLabel    != null) nameLabel.alpha = 1f;
        }
    }
}
