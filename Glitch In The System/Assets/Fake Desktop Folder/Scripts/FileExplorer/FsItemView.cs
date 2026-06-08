using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Single row — folder or file. Left-click to select/open; right-click for context menu.
/// </summary>
public sealed class FsItemView : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private Image           background;

    private FileSystemManager.FsEntry _entry;
    private bool                      _selected;

    public Action<FsItemView>                OnSingleClick;
    public Action<FileSystemManager.FsEntry> OnDoubleClick;
    public Action<FsItemView, Vector2>       OnRightClick;  // NEW

    private static readonly Color BgNormal   = new Color(1f, 1f, 1f, 0.00f);
    private static readonly Color BgHover    = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color BgSelected = new Color(0.3f, 0.55f, 0.9f, 0.30f);

    private float _lastClickTime = -1f;
    private const float DblClickInterval = 0.35f;

    public void Init(FileSystemManager.FsEntry entry, Sprite folderIcon, Sprite fileIcon)
    {
        _entry = entry;
        if (nameLabel  != null) nameLabel.text   = entry.name;
        if (iconImage  != null) iconImage.sprite  =
            entry.type == FileSystemManager.EntryType.Folder ? folderIcon : fileIcon;
        if (background != null) background.color  = BgNormal;
    }

    /// <summary>
    /// Rebind an existing pooled row to a new entry — no GOs created or destroyed.
    /// Called by FileExplorerApp.PopulateContent when reusing a pool slot.
    /// </summary>
    public void Rebind(FileSystemManager.FsEntry entry, Sprite folderIcon, Sprite fileIcon,
                       Color iconColor)
    {
        _entry         = entry;
        _selected      = false;
        _lastClickTime = -1f;
        if (nameLabel  != null) nameLabel.text  = entry.name;
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
        if (background != null) background.color = selected ? BgSelected : BgNormal;
    }

    /// Refresh name label after rename
    public void RefreshName(string newName)
    {
        if (nameLabel != null) nameLabel.text = newName;
    }

    public void OnPointerClick(PointerEventData e)
    {
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
        if (!_selected && background != null) background.color = BgHover;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_selected && background != null) background.color = BgNormal;
    }

    public FileSystemManager.FsEntry Entry => _entry;

    public void SetRefs(Image bg, Image icon, TextMeshProUGUI label)
    {
        background = bg;
        iconImage  = icon;
        nameLabel  = label;
    }
}
