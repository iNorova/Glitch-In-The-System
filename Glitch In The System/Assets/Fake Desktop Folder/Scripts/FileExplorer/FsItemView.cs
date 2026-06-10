using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [SerializeField] private Image           background;

    private FileSystemManager.FsEntry _entry;
    private bool                      _selected;
    private bool                      _isDragging;

    // Callbacks wired by FileExplorerApp
    public Action<FsItemView>                OnSingleClick;
    public Action<FileSystemManager.FsEntry> OnDoubleClick;
    public Action<FsItemView, Vector2>       OnRightClick;
    /// <summary>Called when this item is dropped onto a folder target. Arg = target folder path.</summary>
    public Action<FsItemView, string>        OnDroppedOnto;
    /// <summary>Called when another item is dropped onto THIS folder.</summary>
    public Action<FsItemView, FsItemView>    OnReceivedDrop;

    // Background colors — static so one allocation serves all instances
    private static readonly Color BgNormal      = new Color(1f,    1f,    1f,    0.00f);
    private static readonly Color BgHover       = new Color(1f,    1f,    1f,    0.06f);
    private static readonly Color BgSelected    = new Color(0.30f, 0.55f, 0.90f, 0.30f);
    private static readonly Color BgDropTarget  = new Color(0.30f, 0.70f, 0.40f, 0.30f);

    private float _lastClickTime = -1f;
    private const float DblClickInterval = 0.35f;

    // Drag ghost — a faded clone of the name label that follows the cursor
    private static GameObject  _dragGhost;
    private static FsItemView  _draggingItem;
    private static Canvas      _rootCanvas;

    public void Init(FileSystemManager.FsEntry entry, Sprite folderIcon, Sprite fileIcon)
    {
        _entry = entry;
        if (nameLabel  != null) nameLabel.text   = entry.name;
        if (iconImage  != null) iconImage.sprite  =
            entry.type == FileSystemManager.EntryType.Folder ? folderIcon : fileIcon;
        if (background != null) background.color  = BgNormal;
    }

    public void Rebind(FileSystemManager.FsEntry entry, Sprite folderIcon, Sprite fileIcon,
                       Color iconColor)
    {
        _entry         = entry;
        _selected      = false;
        _lastClickTime = -1f;
        if (nameLabel  != null) nameLabel.text   = entry.name;
        if (background != null) background.color = BgNormal;
        if (iconImage  != null)
        {
            bool hasSprite = entry.type == FileSystemManager.EntryType.Folder
                ? folderIcon != null : fileIcon != null;
            if (hasSprite)
            {
                iconImage.sprite = entry.type == FileSystemManager.EntryType.Folder
                    ? folderIcon : fileIcon;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color  = iconColor;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (background != null)
            background.color = selected ? BgSelected : BgNormal;
    }

    public void RefreshName(string newName)
    {
        if (nameLabel != null) nameLabel.text = newName;
    }

    public FileSystemManager.FsEntry Entry => _entry;

    public void SetRefs(Image bg, Image icon, TextMeshProUGUI label)
    {
        background = bg;
        iconImage  = icon;
        nameLabel  = label;
    }

    // ── Click handling ────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData e) { /* required by IPointerDownHandler to receive drag events */ }
    public void OnPointerUp(PointerEventData e)   { /* paired with OnPointerDown */ }

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

        if (dbl) OnDoubleClick?.Invoke(_entry);
        else     { SetSelected(true); OnSingleClick?.Invoke(this); }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        // If something is being dragged and we're a folder, show drop highlight
        if (_draggingItem != null && _draggingItem != this &&
            _entry.type == FileSystemManager.EntryType.Folder)
        {
            if (background != null) background.color = BgDropTarget;
            return;
        }
        if (!_selected && background != null) background.color = BgHover;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_selected && background != null) background.color = BgNormal;
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

            var tmp = _dragGhost.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text          = _entry.name;
            tmp.fontSize      = 13;
            tmp.color         = new Color(0.90f, 0.88f, 0.84f, 0.75f);
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;

            // Bring ghost to front
            _dragGhost.transform.SetAsLastSibling();
        }
        else if (_dragGhost != null)
        {
            // Reuse — just update label
            var tmp = _dragGhost.GetComponent<TMPro.TextMeshProUGUI>();
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

        var rt = _dragGhost.GetComponent<RectTransform>();
        rt.anchoredPosition = localPos + new Vector2(12f, -8f);
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

    // ── Drop target (folders only) ────────────────────────────────────────
    public void OnDrop(PointerEventData e)
    {
        // Clear drop highlight
        if (!_selected && background != null) background.color = BgNormal;

        if (_entry.type != FileSystemManager.EntryType.Folder) return;
        if (_draggingItem == null || _draggingItem == this)    return;
        if (_draggingItem._entry.fullPath == _entry.fullPath)  return;

        // Prevent dropping a folder into its own descendant
        if (_entry.fullPath.StartsWith(_draggingItem._entry.fullPath + "/",
            StringComparison.Ordinal)) return;

        OnReceivedDrop?.Invoke(this, _draggingItem);
    }
}
