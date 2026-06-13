using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// File Explorer — Batches 1–7 + Upgrade patch.
/// Upgrade additions:
///   - Copy/Paste: Ctrl+C snapshots entry data (deep copy struct), Ctrl+V pastes true duplicate
///   - Drag-drop move: drop file/folder onto a folder row to move it (FsItemView)
///   - Sidebar drag: SidebarFolderButton now supports drag-drop between sidebar roots
///   - Refresh button: scene-placed RefreshButton wired via Inspector [SerializeField]
///   - OnFsChanged now fires correctly (FileSystemManager.NotifyChanged fixed)
/// </summary>
public sealed class FileExplorerApp : MonoBehaviour, IPointerClickHandler
{
    [Header("Navigation")]
    [SerializeField] private RectTransform   sidebarContent;
    [SerializeField] private RectTransform   fileContent;
    [SerializeField] private TextMeshProUGUI pathText;
    [SerializeField] private Button          backButton;
    [SerializeField] private Button          forwardButton;
    [SerializeField] private TextMeshProUGUI emptyLabel;

    [Header("Icons (optional)")]
    [SerializeField] private Sprite folderIcon;
    [SerializeField] private Sprite fileIcon;

    [Header("Refresh Button (scene-placed, Inspector-editable)")]
    [Tooltip("Wire the RefreshButton scene object here. Must be in TopBar or NavigationBar.")]
    [SerializeField] private Button refreshButton;

    // ── Sub-systems ───────────────────────────────────────────────────────
    private FsContextMenu       _contextMenu;
    private FsRenameOverlay     _renameOverlay;
    private FsFolderPickerModal _folderPicker;
    private FsStatusToast       _toast;        // lightweight auto-hide status feedback

    // ── Navigation ────────────────────────────────────────────────────────
    private readonly List<string>              _history     = new();
    private int                                _histIdx     = -1;
    private readonly List<SidebarFolderButton> _sidebarBtns = new();

    // ── Content ───────────────────────────────────────────────────────────
    private readonly List<FsItemView>              _items    = new();
    private FsItemView                             _selectedItem;
    private bool                                   _contentLayoutReady;
    private readonly List<(string, System.Action)> _menuItems = new(6);
    private readonly List<FsItemView>              _rowPool   = new(32);

    // ── Copy/Paste clipboard (deep-copy snapshot, NOT a live reference) ───
    // Stores a value-type snapshot so renamed/deleted originals don't corrupt paste.
    private struct ClipboardSnapshot
    {
        public string                         name;
        public FileSystemManager.EntryType    type;
        public string                         parentPath;
        // Extendable: add metadata fields here as the FS grows
    }
    private ClipboardSnapshot? _clipboard;   // null = nothing copied

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private void Awake() => EnsureAwakeInit();

    private void EnsureAwakeInit()
    {
        if (_contextMenu == null)
        {
            var cmGO = new GameObject("FsContextMenu", typeof(RectTransform), typeof(FsContextMenu));
            cmGO.transform.SetParent(transform, false);
            var cmRT = cmGO.GetComponent<RectTransform>();
            cmRT.anchorMin = Vector2.zero; cmRT.anchorMax = Vector2.one;
            cmRT.offsetMin = Vector2.zero; cmRT.offsetMax = Vector2.zero;
            _contextMenu = cmGO.GetComponent<FsContextMenu>();
        }

        if (_renameOverlay == null)
        {
            var rnGO = new GameObject("FsRenameOverlay", typeof(RectTransform), typeof(FsRenameOverlay));
            rnGO.transform.SetParent(transform, false);
            _renameOverlay = rnGO.GetComponent<FsRenameOverlay>();
            _renameOverlay.Init();
        }

        if (_folderPicker == null)
        {
            var fpGO = new GameObject("FsFolderPickerModal", typeof(RectTransform), typeof(FsFolderPickerModal));
            fpGO.transform.SetParent(transform, false);
            var fpRT = fpGO.GetComponent<RectTransform>();
            fpRT.anchorMin = Vector2.zero; fpRT.anchorMax = Vector2.one;
            fpRT.offsetMin = Vector2.zero; fpRT.offsetMax = Vector2.zero;
            _folderPicker = fpGO.GetComponent<FsFolderPickerModal>();
            _folderPicker.Init();
        }

        EnsurePathText();
        EnsureContentLayout();
        WireRefreshButton();
        EnsureToast();
    }

    private void OnEnable()
    {
        if (FileSystemManager.Instance != null)
            FileSystemManager.Instance.OnChanged += OnFsChanged;

        EnsureAwakeInit();

        var canvas = GetComponentInParent<Canvas>();
        if (_contextMenu != null) _contextMenu.Init(canvas);

        if (backButton    != null) { backButton.onClick.RemoveAllListeners();    backButton.onClick.AddListener(GoBack);    }
        if (forwardButton != null) { forwardButton.onClick.RemoveAllListeners(); forwardButton.onClick.AddListener(GoForward); }

        if (_sidebarBtns.Count == 0 || (sidebarContent != null && sidebarContent.childCount == 0))
            BuildSidebar();

        if (_histIdx < 0) NavigateTo("/Desktop");
        else              RefreshUI();
    }

    private void OnDisable()
    {
        if (FileSystemManager.Instance != null)
            FileSystemManager.Instance.OnChanged -= OnFsChanged;
        // Clear static drag references so reopening the window starts clean
        FsItemView.ClearDragStatics();
    }

    private void OnFsChanged()
    {
        if (gameObject.activeInHierarchy)
            PopulateContent(CurrentPath);
    }

    // ── Keyboard: Copy / Paste ────────────────────────────────────────────
    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

        // BATCH 1: keyboard shortcuts (fire only when no modifier key held, except Ctrl combos)
        if (!ctrl)
        {
            if (kb.f2Key.wasPressedThisFrame && _selectedItem != null)
                BeginRename(_selectedItem);

            else if (kb.deleteKey.wasPressedThisFrame && _selectedItem != null)
                DeleteSelected();

            else if (kb.enterKey.wasPressedThisFrame && _selectedItem != null)
                OnItemDoubleClick(_selectedItem.Entry);
        }
        else
        {
            if (kb.cKey.wasPressedThisFrame)      CopySelected();
            else if (kb.vKey.wasPressedThisFrame) PasteClipboard();
        }
    }

    // FIX: deep-copy snapshot — stores primitive fields, not a reference to the live FsEntry object.
    // If the original is later renamed or deleted, the clipboard is unaffected.
    private void CopySelected()
    {
        if (_selectedItem == null) return;
        var e = _selectedItem.Entry;
        _clipboard = new ClipboardSnapshot
        {
            name       = e.name,
            type       = e.type,
            parentPath = e.parentPath,
        };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[FileExplorer] Copied: {e.name} ({e.type})");
#endif
    }

    // FIX: paste creates a truly new entry using snapshotted name, never the original reference.
    // Unique name suffix ensures no collision even if paste is repeated.
    private void PasteClipboard()
    {
        if (_clipboard == null) return;

        var snap = _clipboard.Value;
        var fs   = FileSystemManager.Instance;
        if (fs == null) return;

        if (snap.type == FileSystemManager.EntryType.Folder)
        {
            // Folder copy not yet supported — show non-modal feedback and bail.
            _toast?.Show("Folder copy not supported yet");
            return;
        }

        // Build unique name: "file (Copy).txt", "file (Copy 2).txt", …
        string baseName = snap.name;
        string ext      = "";
        int dotIdx      = baseName.LastIndexOf('.');
        if (dotIdx > 0) { ext = baseName.Substring(dotIdx); baseName = baseName.Substring(0, dotIdx); }

        string candidate = baseName + " (Copy)" + ext;
        int n = 2;
        while (fs.Exists(CurrentPath + "/" + candidate))
            candidate = baseName + $" (Copy {n++})" + ext;

        // CreateFile creates a brand-new FsEntry with a unique fullPath — true duplication
        var newEntry = fs.CreateFile(CurrentPath, candidate);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (newEntry != null) Debug.Log($"[FileExplorer] Pasted: {newEntry.fullPath}");
        else Debug.LogWarning($"[FileExplorer] Paste failed: '{candidate}' in '{CurrentPath}'");
#endif
    }

    // ── Navigation API ────────────────────────────────────────────────────
    public void NavigateTo(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return;
        _contextMenu?.Hide();
        _renameOverlay?.Hide();

        if (_histIdx < _history.Count - 1)
            _history.RemoveRange(_histIdx + 1, _history.Count - _histIdx - 1);
        if (_histIdx < 0 || _history[_histIdx] != fullPath)
        {
            _history.Add(fullPath);
            _histIdx = _history.Count - 1;
        }
        RefreshUI();
    }

    public void GoBack()    { if (_histIdx > 0)                  { _histIdx--; RefreshUI(); } }
    public void GoForward() { if (_histIdx < _history.Count - 1) { _histIdx++; RefreshUI(); } }

    public string CurrentPath => (_histIdx >= 0 && _histIdx < _history.Count)
        ? _history[_histIdx] : "";

    // ── File Actions ──────────────────────────────────────────────────────
    public void CreateFolder()
    {
        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        string name = "New Folder";
        int n = 1;
        while (fs.Exists(CurrentPath + "/" + name)) name = $"New Folder ({n++})";

        var entry = fs.CreateFolder(CurrentPath, name);
        if (entry == null) return;

        var view = _items.Find(i => i.Entry.fullPath == entry.fullPath);
        if (view != null) BeginRename(view);
    }

    public void DeleteSelected()
    {
        if (_selectedItem == null) return;
        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        // Invalidate clipboard if the copied item is being deleted
        if (_clipboard != null &&
            _clipboard.Value.name == _selectedItem.Entry.name &&
            _clipboard.Value.parentPath == _selectedItem.Entry.parentPath)
            _clipboard = null;

        fs.Delete(_selectedItem.Entry.fullPath);
        _selectedItem = null;
    }

    public void BeginRename(FsItemView view)
    {
        if (view == null) return;
        view.BeginInlineRename(
            newName =>
            {
                var fs = FileSystemManager.Instance;
                if (fs != null) fs.Rename(view.Entry.fullPath, newName);
            },
            () => { /* cancelled */ }
        );
    }

    private void ShowMoveModal(FsItemView view)
    {
        if (view == null || _folderPicker == null) return;
        var entry = view.Entry;

        _folderPicker.Show(
            sourcePath: entry.fullPath,
            onConfirm: targetPath =>
            {
                // Guard: same folder = no-op
                if (targetPath == entry.parentPath) return;

                var fs = FileSystemManager.Instance;
                if (fs == null) return;

                // Guard: cannot move folder into its own descendant
                if (entry.type == FileSystemManager.EntryType.Folder &&
                    targetPath.StartsWith(entry.fullPath + "/", System.StringComparison.Ordinal))
                    return;

                bool ok = fs.Move(entry.fullPath, targetPath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!ok) Debug.LogWarning($"[FileExplorer] Move failed: {entry.fullPath} → {targetPath}");
#endif
                if (_selectedItem == view) _selectedItem = null;
            },
            onCancel: null
        );
    }

    public void MoveSelectedTo(string targetFolderPath)
    {
        if (_selectedItem == null) return;
        var fs = FileSystemManager.Instance;
        if (fs == null) return;
        fs.Move(_selectedItem.Entry.fullPath, targetFolderPath);
        _selectedItem = null;
    }

    /// <summary>
    /// Move any entry by path. Used by SidebarFolderButton.OnDrop() which resolves the
    /// dragged item directly (no _selectedItem dependency), enabling drag-to-sidebar
    /// without a prior click. Circular-parent guard is inside FileSystemManager.Move().
    /// </summary>
    public void MoveEntryTo(string entryFullPath, string targetFolderPath)
    {
        if (string.IsNullOrEmpty(entryFullPath) || string.IsNullOrEmpty(targetFolderPath)) return;
        var fs = FileSystemManager.Instance;
        if (fs == null) return;
        bool ok = fs.Move(entryFullPath, targetFolderPath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ok)
            Debug.LogWarning($"[FileExplorer] MoveEntryTo failed: {entryFullPath} → {targetFolderPath}");
#endif
        if (_selectedItem != null && _selectedItem.Entry.fullPath == entryFullPath)
            _selectedItem = null;
    }

    // ── Drag & drop acceptance (content area) ─────────────────────────────
    private void OnItemReceivedDrop(FsItemView target, FsItemView dragged)
    {
        if (target == null || dragged == null) return;
        if (target.Entry.type != FileSystemManager.EntryType.Folder) return;

        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        bool ok = fs.Move(dragged.Entry.fullPath, target.Entry.fullPath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ok)
            Debug.LogWarning($"[FileExplorer] Move failed: {dragged.Entry.fullPath} → {target.Entry.fullPath}");
#endif

        if (_selectedItem == dragged) _selectedItem = null;
    }

    // ── Context menu ──────────────────────────────────────────────────────
    private void ShowItemContextMenu(FsItemView view, Vector2 screenPos)
    {
        bool isFolder = view.Entry.type == FileSystemManager.EntryType.Folder;

        _menuItems.Clear();
        if (isFolder)
            _menuItems.Add(("Open",       () => NavigateTo(view.Entry.fullPath)));
        _menuItems.Add(("Copy",       () => { OnItemSingleClick(view); CopySelected(); }));
        _menuItems.Add(("Move...",    () => ShowMoveModal(view)));
        _menuItems.Add(("Rename",     () => BeginRename(view)));
        _menuItems.Add(("Delete",     () => DeleteSelected()));
        _menuItems.Add(("---",        null));
        _menuItems.Add(("New Folder", () => CreateFolder()));
        if (_clipboard != null)
            _menuItems.Add(("Paste",   () => PasteClipboard()));

        _contextMenu.ShowAt(screenPos, _menuItems);
    }

    private void ShowBackgroundContextMenu(Vector2 screenPos)
    {
        _menuItems.Clear();
        _menuItems.Add(("New Folder", () => CreateFolder()));
        // BATCH 1: only show Paste when there is something on the clipboard.
        if (_clipboard != null)
            _menuItems.Add(("Paste", () => PasteClipboard()));
        _contextMenu.ShowAt(screenPos, _menuItems);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right) return;
        if (_contextMenu.IsOpen) { _contextMenu.Hide(); return; }
        ShowBackgroundContextMenu(e.position);
    }

    // ── Refresh ───────────────────────────────────────────────────────────
    public void Refresh() => PopulateContent(CurrentPath);

    // ── Sidebar drag-drop: called by SidebarFolderButton ──────────────────
    /// <summary>
    /// Called when a sidebar button receives a drop from another sidebar button.
    /// Moves the dragged folder under the target folder.
    /// Guards: cannot move into self, cannot move into a descendant.
    /// </summary>
    public void OnSidebarDrop(string draggedPath, string targetPath)
    {
        if (string.IsNullOrEmpty(draggedPath) || string.IsNullOrEmpty(targetPath)) return;
        if (draggedPath == targetPath) return;

        // Prevent moving into own descendant (circular parenting)
        if (targetPath.StartsWith(draggedPath + "/", System.StringComparison.Ordinal))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[FileExplorer] Sidebar move blocked: '{draggedPath}' → '{targetPath}'");
#endif
            return;
        }

        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        bool ok = fs.Move(draggedPath, targetPath);
        if (ok)
        {
            // Sidebar roots list is static — rebuild sidebar to reflect new hierarchy
            BuildSidebar();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FileExplorer] Sidebar move: '{draggedPath}' → '{targetPath}'");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
            Debug.LogWarning($"[FileExplorer] Sidebar move failed: '{draggedPath}' → '{targetPath}'");
#endif
    }

    // ── Refresh / populate ────────────────────────────────────────────────
    private void RefreshUI()
    {
        string path = CurrentPath;

        if (pathText != null)
        {
            if (string.IsNullOrEmpty(path))
                pathText.text = "File Explorer";
            else
            {
                string display = path.TrimStart('/').Replace("/", "  ›  ");
                pathText.text = string.IsNullOrEmpty(display) ? "File Explorer" : display;
            }
        }

        if (backButton != null)
        {
            backButton.interactable = _histIdx > 0;
            SetButtonLabelAlpha(backButton, _histIdx > 0);
        }
        if (forwardButton != null)
        {
            forwardButton.interactable = _histIdx < _history.Count - 1;
            SetButtonLabelAlpha(forwardButton, _histIdx < _history.Count - 1);
        }

        foreach (var btn in _sidebarBtns)
            btn.SetSelected(btn.name == "SidebarBtn_" + path);

        PopulateContent(path);
    }

    private void PopulateContent(string path)
    {
        foreach (var item in _items)
            if (item != null) item.gameObject.SetActive(false);
        _items.Clear();
        _selectedItem = null;

        if (fileContent == null) return;

        var fs = FileSystemManager.Instance;
        if (fs == null) return;
        var children = fs.GetChildren(path);

        if (emptyLabel != null)
        {
            bool empty = children.Count == 0;
            emptyLabel.text    = empty ? "This folder is empty." : "";
            emptyLabel.enabled = empty;
            emptyLabel.gameObject.SetActive(empty);
        }

        int poolIdx = 0;
        foreach (var entry in children)
        {
            FsItemView view;
            while (poolIdx < _rowPool.Count && _rowPool[poolIdx].gameObject.activeSelf)
                poolIdx++;

            if (poolIdx < _rowPool.Count)
            {
                view = _rowPool[poolIdx];
                Color iconColor = GetIconColor(entry);
                view.Rebind(entry, folderIcon, fileIcon, iconColor);
                view.OnSingleClick  = OnItemSingleClick;
                view.OnDoubleClick  = OnItemDoubleClick;
                view.OnRightClick   = (v, pos) => { OnItemSingleClick(v); ShowItemContextMenu(v, pos); };
                view.OnReceivedDrop = OnItemReceivedDrop;
                view.gameObject.SetActive(true);
                view.transform.SetAsLastSibling();
                poolIdx++;
            }
            else
            {
                view = BuildItemRow(entry);
                _rowPool.Add(view);
            }

            UpdateTypeLabel(view, entry);
            view.SetDateLabel(view.Entry.lastModified == default ? "—" : view.Entry.lastModified.ToString("dd/MM/yyyy  HH:mm"));
            _items.Add(view);
        }
    }

    private static void UpdateTypeLabel(FsItemView view, FileSystemManager.FsEntry entry)
    {
        // Uses FsItemView.SetTypeLabel — cached reference, no GetChild/GetComponent.
        view.SetTypeLabel(GetTypeLabel(entry));
    }

    private static Color GetIconColor(FileSystemManager.FsEntry entry)
    {
        if (entry.type == FileSystemManager.EntryType.Folder)
            return new Color(0.96f, 0.76f, 0.26f, 1f);
        if (!string.IsNullOrEmpty(entry.name) &&
            entry.name.EndsWith(".lnk", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.55f, 0.80f, 1.00f, 1f);
        return new Color(0.65f, 0.67f, 0.72f, 1f);
    }

    private void EnsureContentLayout()
    {
        if (_contentLayoutReady || fileContent == null) return;
        _contentLayoutReady = true;

        // BATCH 1: Windows-like scroll feel — find the ScrollRect that contains fileContent.
        var sr = fileContent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (sr != null)
        {
            sr.scrollSensitivity = 100f;
            sr.decelerationRate  = 0.06f;
        }

        var vlg = fileContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = fileContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(4, 4, 4, 4);
        vlg.spacing              = 1;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = fileContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = fileContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        fileContent.sizeDelta = new Vector2(fileContent.sizeDelta.x, 0f);

        // ── Column header row ─────────────────────────────────────────────
        // Guard: never add a second header if EnsureContentLayout somehow runs twice.
        if (fileContent.Find("__ColumnHeader") == null)
        {
            var hdrGO = new GameObject("__ColumnHeader",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            hdrGO.transform.SetParent(fileContent, false);
            hdrGO.transform.SetAsFirstSibling();

            hdrGO.GetComponent<LayoutElement>().preferredHeight = 24f;

            var hdrImg = hdrGO.GetComponent<Image>();
            hdrImg.color = new Color(0.14f, 0.14f, 0.17f, 1f);
            hdrImg.raycastTarget = false;

            // HLG mirrors row layout:
            // left pad = row-left(6) + icon-width(16) + spacing(6) = 28
            // right pad = row-right(8), spacing = row-spacing(6)
            var hdrHLG = hdrGO.GetComponent<HorizontalLayoutGroup>();
            hdrHLG.padding              = new RectOffset(28, 8, 0, 0);
            hdrHLG.spacing              = 6;
            hdrHLG.childAlignment       = TextAnchor.MiddleLeft;
            hdrHLG.childControlWidth    = false;
            hdrHLG.childControlHeight   = true;
            hdrHLG.childForceExpandWidth  = false;
            hdrHLG.childForceExpandHeight = true;

            AddHeaderLabel(hdrGO.transform, "Name",          0f,   1f);   // flexible
            AddHeaderLabel(hdrGO.transform, "Date modified", 120f, 0f);   // fixed 120
            AddHeaderLabel(hdrGO.transform, "Type",          80f,  0f);   // fixed 80
        }
    }

    private static void AddHeaderLabel(Transform parent, string text,
                                       float preferredWidth, float flexibleWidth)
    {
        var go = new GameObject("Hdr_" + text,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.flexibleWidth  = flexibleWidth;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = text.ToUpperInvariant();
        tmp.fontSize      = 10f;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.color         = new Color(0.55f, 0.54f, 0.58f, 1f);
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
    }

    private FsItemView BuildItemRow(FileSystemManager.FsEntry entry)
    {
        var go = new GameObject("FsItem_" + entry.name,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(FsItemView));
        go.transform.SetParent(fileContent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 32f;

        var bgImg = go.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0f);
        bgImg.raycastTarget = true;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding        = new RectOffset(6, 8, 0, 0);
        hlg.spacing        = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        var iconGO = new GameObject("Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth  = 16f;
        iconLE.preferredHeight = 16f;
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.color           = GetIconColor(entry);
        iconImg.raycastTarget   = false;
        iconImg.type            = Image.Type.Simple;
        iconImg.preserveAspect  = true;
        // Center the icon RT inside its 16x16 LE slot — fixes visual misalignment from preserveAspect
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRT.pivot            = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta        = new Vector2(16f, 16f);
        if (entry.type == FileSystemManager.EntryType.Folder && folderIcon != null) { iconImg.sprite = folderIcon; iconImg.color = Color.white; }
        if (entry.type == FileSystemManager.EntryType.File   && fileIcon   != null) { iconImg.sprite = fileIcon;   iconImg.color = Color.white; }

        var lblGO = new GameObject("Name",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        lblGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text          = entry.name;
        tmp.fontSize      = 13;
        tmp.color         = new Color(0.90f, 0.88f, 0.84f, 1f);
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        var dateGO = new GameObject("Date",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        dateGO.transform.SetParent(go.transform, false);
        dateGO.AddComponent<LayoutElement>().preferredWidth = 120f;
        var dateTMP = dateGO.GetComponent<TextMeshProUGUI>();
        dateTMP.text          = entry.lastModified == default ? "\u2014" : entry.lastModified.ToString("dd/MM/yyyy  HH:mm");
        dateTMP.fontSize      = 11;
        dateTMP.color         = new Color(0.55f, 0.54f, 0.52f, 0.85f);
        dateTMP.alignment     = TextAlignmentOptions.MidlineLeft;
        dateTMP.overflowMode  = TextOverflowModes.Ellipsis;
        dateTMP.raycastTarget = false;

        var typeGO = new GameObject("Type",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        typeGO.transform.SetParent(go.transform, false);
        typeGO.AddComponent<LayoutElement>().preferredWidth = 80f;
        var typeTMP = typeGO.GetComponent<TextMeshProUGUI>();
        typeTMP.text          = GetTypeLabel(entry);
        typeTMP.fontSize      = 11;
        typeTMP.color         = new Color(0.55f, 0.54f, 0.52f, 0.85f);
        typeTMP.alignment     = TextAlignmentOptions.MidlineLeft;
        typeTMP.overflowMode  = TextOverflowModes.Ellipsis;
        typeTMP.raycastTarget = false;

        var view = go.GetComponent<FsItemView>();
        view.SetRefs(bgImg, iconImg, tmp, typeTMP, dateTMP); // cache all labels — no GetComponent per row
        view.Init(entry, folderIcon, fileIcon);
        view.OnSingleClick  = OnItemSingleClick;
        view.OnDoubleClick  = OnItemDoubleClick;
        view.OnRightClick   = (v, pos) => { OnItemSingleClick(v); ShowItemContextMenu(v, pos); };
        view.OnReceivedDrop = OnItemReceivedDrop;

        return view;
    }

    private static string GetTypeLabel(FileSystemManager.FsEntry entry)
    {
        if (entry.type == FileSystemManager.EntryType.Folder) return "Folder";
        string name = entry.name ?? "";
        int dot = name.LastIndexOf('.');
        if (dot < 0) return "File";
        return name.Substring(dot).ToLowerInvariant() switch
        {
            ".lnk"  => "Shortcut",
            ".note" => "Note",
            ".txt"  => "Text",
            ".png"  => "Image",
            ".jpg"  => "Image",
            ".jpeg" => "Image",
            ".bmp"  => "Image",
            ".pdf"  => "PDF",
            ".zip"  => "Archive",
            ".exe"  => "App",
            _       => "File"
        };
    }

    private void OnItemSingleClick(FsItemView clicked)
    {
        if (_selectedItem != null && _selectedItem != clicked)
            _selectedItem.SetSelected(false);
        _selectedItem = clicked;
    }

    private void OnItemDoubleClick(FileSystemManager.FsEntry entry)
    {
        if (entry.type == FileSystemManager.EntryType.Folder)
            NavigateTo(entry.fullPath);
        else
            FsAppRouter.OpenFile(entry);
    }

    // ── Sidebar ───────────────────────────────────────────────────────────
    private void BuildSidebar()
    {
        if (sidebarContent == null) return;
        for (int i = sidebarContent.childCount - 1; i >= 0; i--)
            Destroy(sidebarContent.GetChild(i).gameObject);
        _sidebarBtns.Clear();

        var vlg = sidebarContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = sidebarContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(4, 4, 8, 4);
        vlg.spacing              = 1;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = sidebarContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = sidebarContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── QUICK ACCESS header ───────────────────────────────────────────────
        var headerGO = new GameObject("SidebarHeader",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        headerGO.transform.SetParent(sidebarContent, false);
        var headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 22f;
        var headerTMP = headerGO.GetComponent<TextMeshProUGUI>();
        headerTMP.text          = "QUICK ACCESS";
        headerTMP.fontSize      = 9;
        headerTMP.color         = new Color(0.50f, 0.49f, 0.47f, 1f);
        headerTMP.alignment     = TextAlignmentOptions.MidlineLeft;
        headerTMP.fontStyle     = FontStyles.Bold;
        headerTMP.raycastTarget = false;
        var headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.offsetMin = new Vector2(10f, 0f);

        foreach (var root in FileSystemManager.SidebarRoots)
            _sidebarBtns.Add(BuildSidebarButtonGO(root, "/" + root));
    }

    private SidebarFolderButton BuildSidebarButtonGO(string displayName, string folderPath)
    {
        var go = new GameObject("SidebarBtn_" + folderPath,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(SidebarFolderButton));
        go.transform.SetParent(sidebarContent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 30f;

        var bgImg = go.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0f);
        bgImg.raycastTarget = true;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding        = new RectOffset(10, 8, 0, 0);
        hlg.spacing        = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        var lblGO = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        lblGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text          = displayName;
        tmp.fontSize      = 12;
        tmp.color         = new Color(0.80f, 0.78f, 0.75f, 1f);
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;

        var sfb = go.GetComponent<SidebarFolderButton>();
        sfb.Init(this, folderPath, displayName);
        return sfb;
    }

    // ── PathBar TMP setup ─────────────────────────────────────────────────
    private void EnsurePathText()
    {
        if (pathText != null) return;

        var navBar = transform.Find("NavigationBar");
        if (navBar == null) return;

        // Style NavigationBar background for Windows-style path bar
        var navImg = navBar.GetComponent<UnityEngine.UI.Image>();
        if (navImg == null) navImg = navBar.gameObject.AddComponent<UnityEngine.UI.Image>();
        navImg.color = new Color(0.11f, 0.11f, 0.13f, 1f);

        var pathBar = navBar.Find("PathBar");
        if (pathBar == null) return;

        // Style the PathBar pill/input-style background
        var pbImg = pathBar.GetComponent<UnityEngine.UI.Image>();
        if (pbImg == null) pbImg = pathBar.gameObject.AddComponent<UnityEngine.UI.Image>();
        pbImg.color = new Color(0.17f, 0.17f, 0.20f, 1f);

        pathText = pathBar.GetComponentInChildren<TextMeshProUGUI>(true);
        if (pathText != null) return;

        var lblGO = new GameObject("PathLabel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(pathBar, false);

        var rt = lblGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f,  2f); rt.offsetMax = new Vector2(-8f, -2f);

        pathText              = lblGO.GetComponent<TextMeshProUGUI>();
        pathText.fontSize     = 12;
        pathText.color        = new Color(0.82f, 0.80f, 0.76f, 1f);
        pathText.alignment    = TextAlignmentOptions.MidlineLeft;
        pathText.overflowMode  = TextOverflowModes.Ellipsis;
        pathText.raycastTarget = false;
        pathText.text          = "File Explorer";
    }

    // ── Refresh button ────────────────────────────────────────────────────
    // Scene-placed RefreshButton wired via [SerializeField] refreshButton.
    // WireRefreshButton() only adds the listener — it does NOT create a new button.
    // The scene object is the source of truth; sprite, size, color are editable in Inspector.
    private void WireRefreshButton()
    {
        if (refreshButton == null) return;
        refreshButton.onClick.RemoveAllListeners();
        refreshButton.onClick.AddListener(Refresh);
    }

    // ── Toast ─────────────────────────────────────────────────────────────
    private void EnsureToast()
    {
        if (_toast != null) return;
        _toast = GetComponent<FsStatusToast>() ?? gameObject.AddComponent<FsStatusToast>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SetButtonLabelAlpha(Button btn, bool active)
    {
        if (btn == null) return;
        var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (lbl != null) lbl.alpha = active ? 1f : 0.30f;
    }
}
