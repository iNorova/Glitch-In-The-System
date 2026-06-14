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
///   - OnFsChanged now fires correctly (FileExplorerManager.NotifyChanged fixed)
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

    // Cached nav-button TMP label refs — assigned once in OnEnable.
    // Avoids GetComponentInChildren<TMP> on every folder navigation.
    private TMPro.TextMeshProUGUI _backLabel;
    private TMPro.TextMeshProUGUI _forwardLabel;

    [Header("Icons (optional)")]
    [SerializeField] private Sprite folderIcon;
    [SerializeField] private Sprite fileIcon;

    [Header("Refresh Button (scene-placed, Inspector-editable)")]
    [Tooltip("Wire the RefreshButton scene object here. Must be in TopBar or NavigationBar.")]
    [SerializeField] private Button refreshButton;

    [Header("Status Bar (auto-created; wire a scene-placed TMP to override)")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    // ── Sub-systems ───────────────────────────────────────────────────────
    private FsContextMenu       _contextMenu;
    private FsRenameOverlay     _renameOverlay;
    private FsFolderPickerModal _folderPicker;
    private FsStatusToast       _toast;        // lightweight auto-hide status feedback
    private FsBoxSelect         _boxSelect;    // Batch 5: drag box-selection rectangle
    private FsBreadcrumb        _breadcrumb;   // Batch 6: clickable path breadcrumb

    // ── Navigation ────────────────────────────────────────────────────────
    private readonly List<string>              _history     = new();
    private int                                _histIdx     = -1;
    private readonly List<SidebarFolderButton> _sidebarBtns = new();

    // ── Content ───────────────────────────────────────────────────────────
    private readonly List<FsItemView>              _items    = new();
    private FsItemView                             _selectedItem;       // primary/anchor selection
    private readonly List<FsItemView>              _selection = new();  // full multi-select set
    private int                                    _lastSelectedIdx = -1; // for Shift+click range
    private bool                                   _contentLayoutReady;
    private readonly List<(string, System.Action)> _menuItems = new(6);
    private readonly List<FsItemView>              _rowPool   = new(32);

    // ── Copy/Paste clipboard (deep-copy snapshot, NOT a live reference) ───
    // Stores a value-type snapshot so renamed/deleted originals don't corrupt paste.
    private struct ClipboardSnapshot
    {
        public string                         name;
        public FileExplorerManager.EntryType    type;
        public string                         parentPath;
        // Extendable: add metadata fields here as the FS grows
    }
    private ClipboardSnapshot? _clipboard;   // null = nothing copied
    private bool               _clipboardIsCut;  // true = Ctrl+X was used (future move-on-paste)
    private ScrollRect         _scrollRect;       // cached in EnsureContentLayout for ScrollIntoView

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
        EnsureStatusBar();
        EnsureBoxSelect();
        EnsureBreadcrumb();
    }

    private void OnEnable()
    {
        if (FileExplorerManager.Instance != null)
            FileExplorerManager.Instance.OnChanged += OnFsChanged;

        EnsureAwakeInit();

        var canvas = GetComponentInParent<Canvas>();
        if (_contextMenu != null) _contextMenu.Init(canvas);

        if (backButton    != null) { backButton.onClick.RemoveAllListeners();    backButton.onClick.AddListener(GoBack);    _backLabel    = backButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); }
        if (forwardButton != null) { forwardButton.onClick.RemoveAllListeners(); forwardButton.onClick.AddListener(GoForward); _forwardLabel = forwardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); }

        if (_sidebarBtns.Count == 0 || (sidebarContent != null && sidebarContent.childCount == 0))
            BuildSidebar();

        if (_histIdx < 0) NavigateTo("/Desktop");
        else              RefreshUI();
    }

    private void OnDisable()
    {
        if (FileExplorerManager.Instance != null)
            FileExplorerManager.Instance.OnChanged -= OnFsChanged;
        // Clear static drag references so reopening the window starts clean
        FsItemView.ClearDragStatics();
    }

    private void OnFsChanged()
    {
        if (gameObject.activeInHierarchy)
            PopulateContent(CurrentPath);
    }

    // ── Keyboard: Batch 1+7 unified ──────────────────────────────────────
    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // Guard: TMP_InputField focused = inline rename or search bar is active.
        // In these cases ALL file-explorer shortcuts are suppressed so the user can
        // type/confirm/cancel normally. This replaces the earlier per-key rename guard.
        bool textFocused = EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject
                         .GetComponent<TMP_InputField>() != null;

        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

        // ── Ctrl combos ────────────────────────────────────────────────────
        if (ctrl)
        {
            if      (kb.aKey.wasPressedThisFrame && !textFocused) SelectAll();
            else if (kb.cKey.wasPressedThisFrame && !textFocused) CopySelected();
            else if (kb.xKey.wasPressedThisFrame && !textFocused) CutSelected();
            else if (kb.vKey.wasPressedThisFrame && !textFocused) PasteClipboard();
            return; // never let ctrl+key leak into non-ctrl block
        }

        // ── All remaining shortcuts suppressed while a text field has focus ─
        if (textFocused) return;

        // F2 — Rename selected
        if (kb.f2Key.wasPressedThisFrame)
        {
            if (_selectedItem != null
                && _selectedItem.gameObject.activeInHierarchy
                && _selectedItem.Entry != null)
                BeginRename(_selectedItem);
            else if (_selectedItem != null)
                _selectedItem = null;   // discard stale reference
        }
        // Delete — delete selected item
        else if (kb.deleteKey.wasPressedThisFrame)
        {
            if (_selectedItem != null) DeleteSelected();
        }
        // Enter — open selected (folder=navigate, file=open via router)
        else if (kb.enterKey.wasPressedThisFrame)
        {
            if (_selectedItem != null) OnItemDoubleClick(_selectedItem.Entry);
        }
        // Backspace — navigate back (mirrors the Back button)
        else if (kb.backspaceKey.wasPressedThisFrame)
        {
            GoBack();
        }
        // Up/Down arrows — move selection through visible rows
        else if (kb.upArrowKey.wasPressedThisFrame)
        {
            NavigateArrow(-1);
        }
        else if (kb.downArrowKey.wasPressedThisFrame)
        {
            NavigateArrow(1);
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
        var fs   = FileExplorerManager.Instance;
        if (fs == null) return;

        if (snap.type == FileExplorerManager.EntryType.Folder)
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
        if (newEntry != null) _toast?.Show($"Pasted \"{candidate}\"");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (newEntry != null) Debug.Log($"[FileExplorer] Pasted: {newEntry.fullPath}");
        else Debug.LogWarning($"[FileExplorer] Paste failed: '{candidate}' in '{CurrentPath}'");
#endif
    }

    // ── Keyboard helpers (Batch 7) ────────────────────────────────────────

    /// <summary>Ctrl+X — snapshot for cut. Actual move-on-paste is a future stub.</summary>
    private void CutSelected()
    {
        if (_selectedItem == null) return;
        CopySelected();          // reuse snapshot logic
        _clipboardIsCut = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[FileExplorer] Cut (stub): {_selectedItem.Entry?.name}");
#endif
        // TODO Batch 8+: dim cut item visually; on paste, move instead of copy.
    }

    /// <summary>Ctrl+A — select all visible items in the current folder.</summary>
    private void SelectAll()
    {
        if (_items.Count == 0) return;

        foreach (var v in _selection) v.SetSelected(false);
        _selection.Clear();

        foreach (var v in _items)
        {
            v.SetSelected(true);
            _selection.Add(v);
        }

        _selectedItem    = _items[0];
        _lastSelectedIdx = 0;
        UpdateStatusBar();
    }

    /// <summary>
    /// Arrow key navigation. dir = -1 (up) or +1 (down).
    /// Selects the previous/next item in _items (the visible ordered row list).
    /// First press when nothing is selected picks the first or last item.
    /// Uses OnItemSingleClick so all selection/status-bar state stays consistent.
    /// </summary>
    private void NavigateArrow(int dir)
    {
        if (_items.Count == 0) return;

        int curIdx = _selectedItem != null ? _items.IndexOf(_selectedItem) : -1;
        int nextIdx;

        if (curIdx < 0)
        {
            nextIdx = dir > 0 ? 0 : _items.Count - 1;
        }
        else
        {
            nextIdx = Mathf.Clamp(curIdx + dir, 0, _items.Count - 1);
            if (nextIdx == curIdx) return; // already at boundary
        }

        OnItemSingleClick(_items[nextIdx]);
        ScrollItemIntoView(_items[nextIdx]);
    }

    /// <summary>
    /// Scrolls the ScrollRect just enough to bring the item into view.
    /// No-ops if the item is already fully visible or no scroll rect is cached.
    /// Uses the content-space Y positions so it is correct regardless of padding.
    /// </summary>
    private void ScrollItemIntoView(FsItemView view)
    {
        if (_scrollRect == null || view == null) return;

        var viewportRT = _scrollRect.viewport;
        var contentRT  = _scrollRect.content;
        var itemRT     = view.GetComponent<RectTransform>();
        if (viewportRT == null || contentRT == null || itemRT == null) return;

        // Force layout to ensure anchoredPosition values are current.
        Canvas.ForceUpdateCanvases();

        float contentH  = contentRT.rect.height;
        float viewportH = viewportRT.rect.height;
        if (contentH <= viewportH) return; // entire list visible — no scroll needed

        float scrollableH = contentH - viewportH;
        if (scrollableH <= 0f) return;

        // In Unity's vertical ScrollRect: verticalNormalizedPosition 1 = top, 0 = bottom.
        // Content anchor is (0,1) = top-left, so anchoredPosition.y is negative (going down).
        float itemTopInContent    = -itemRT.anchoredPosition.y;
        float itemBottomInContent = itemTopInContent + itemRT.rect.height;
        float currentScrollPx     = (1f - _scrollRect.verticalNormalizedPosition) * scrollableH;
        float viewTopPx           = currentScrollPx;
        float viewBottomPx        = currentScrollPx + viewportH;

        if (itemTopInContent < viewTopPx)
        {
            // Item above viewport — scroll up to reveal it
            _scrollRect.verticalNormalizedPosition =
                1f - Mathf.Clamp01(itemTopInContent / scrollableH);
        }
        else if (itemBottomInContent > viewBottomPx)
        {
            // Item below viewport — scroll down to reveal it
            _scrollRect.verticalNormalizedPosition =
                1f - Mathf.Clamp01((itemBottomInContent - viewportH) / scrollableH);
        }
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
        var fs = FileExplorerManager.Instance;
        if (fs == null) return;

        string name = "New Folder";
        int n = 1;
        while (fs.Exists(CurrentPath + "/" + name)) name = $"New Folder ({n++})";

        var entry = fs.CreateFolder(CurrentPath, name);
        if (entry == null) return;
        _toast?.Show("New folder created");

        var view = _items.Find(i => i.Entry.fullPath == entry.fullPath);
        if (view != null) BeginRename(view);
    }

    public void DeleteSelected()
    {
        if (_selectedItem == null) return;
        var fs = FileExplorerManager.Instance;
        if (fs == null) return;

        // Invalidate clipboard if the copied item is being deleted
        if (_clipboard != null &&
            _clipboard.Value.name == _selectedItem.Entry.name &&
            _clipboard.Value.parentPath == _selectedItem.Entry.parentPath)
            _clipboard = null;

        string deletedName = _selectedItem.Entry.name;
        fs.Delete(_selectedItem.Entry.fullPath);
        _selectedItem = null;
        _toast?.Show($"Deleted \"{deletedName}\"");
    }

    public void BeginRename(FsItemView view)
    {
        if (view == null) return;
        // FIX-2: reject stale pooled reference
        if (view.Entry == null) { _selectedItem = null; return; }
        view.BeginInlineRename(
            newName =>
            {
                var fs = FileExplorerManager.Instance;
                if (fs != null)
                {
                    fs.Rename(view.Entry.fullPath, newName);
                    _toast?.Show($"Renamed to {newName}");
                }
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

                var fs = FileExplorerManager.Instance;
                if (fs == null) return;

                // Guard: cannot move folder into its own descendant
                if (entry.type == FileExplorerManager.EntryType.Folder &&
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
        var fs = FileExplorerManager.Instance;
        if (fs == null) return;
        fs.Move(_selectedItem.Entry.fullPath, targetFolderPath);
        _selectedItem = null;
    }

    /// <summary>
    /// Move any entry by path. Used by SidebarFolderButton.OnDrop() which resolves the
    /// dragged item directly (no _selectedItem dependency), enabling drag-to-sidebar
    /// without a prior click. Circular-parent guard is inside FileExplorerManager.Move().
    /// </summary>
    public void MoveEntryTo(string entryFullPath, string targetFolderPath)
    {
        if (string.IsNullOrEmpty(entryFullPath) || string.IsNullOrEmpty(targetFolderPath)) return;
        var fs = FileExplorerManager.Instance;
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
        if (target.Entry.type != FileExplorerManager.EntryType.Folder) return;

        var fs = FileExplorerManager.Instance;
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
        bool isFolder = view.Entry.type == FileExplorerManager.EntryType.Folder;

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
        if (e.button == PointerEventData.InputButton.Left)
        {
            // Click on empty background — clear multi-selection
            foreach (var v in _selection) v.SetSelected(false);
            _selection.Clear();
            _selectedItem    = null;
            _lastSelectedIdx = -1;
            UpdateStatusBar();
            return;
        }
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

        var fs = FileExplorerManager.Instance;
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

        // Batch 6: breadcrumb takes over display; pathText kept as fallback only
        if (_breadcrumb != null)
            _breadcrumb.Rebuild(path, NavigateTo);
        else if (pathText != null)
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
            if (_backLabel != null) _backLabel.alpha = _histIdx > 0 ? 1f : 0.30f;
        }
        if (forwardButton != null)
        {
            forwardButton.interactable = _histIdx < _history.Count - 1;
            if (_forwardLabel != null) _forwardLabel.alpha = _histIdx < _history.Count - 1 ? 1f : 0.30f;
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
        _selectedItem    = null;
        _selection.Clear();
        _lastSelectedIdx = -1;

        if (fileContent == null) return;

        var fs = FileExplorerManager.Instance;
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

    private static void UpdateTypeLabel(FsItemView view, FileExplorerManager.FsEntry entry)
    {
        // Uses FsItemView.SetTypeLabel — cached reference, no GetChild/GetComponent.
        view.SetTypeLabel(GetTypeLabel(entry));
    }

    private static Color GetIconColor(FileExplorerManager.FsEntry entry)
    {
        if (entry.type == FileExplorerManager.EntryType.Folder)
            return new Color(0.96f, 0.76f, 0.26f, 1f);
        if (!string.IsNullOrEmpty(entry.name) &&
            entry.name.EndsWith(".lnk", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.55f, 0.80f, 1.00f, 1f);
        return new Color(0.65f, 0.67f, 0.72f, 1f);
    }

    // ── Status bar ────────────────────────────────────────────────────────
    // EnsureStatusBar is guarded by statusLabel != null — safe across Awake/OnEnable re-entries.
    private void EnsureStatusBar()
    {
        if (statusLabel != null) return;

        // Reuse a previously built bar (e.g. after domain reload with scene reference lost)
        var existingBar = transform.Find("__StatusBar");
        if (existingBar != null)
        {
            statusLabel = existingBar.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statusLabel != null) return;
        }

        // Build the status bar GO as a direct child of this window, pinned to the bottom.
        var barGO = new GameObject("__StatusBar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barGO.transform.SetParent(transform, false);

        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0f, 0f);
        barRT.anchorMax        = new Vector2(1f, 0f);
        barRT.pivot            = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta        = new Vector2(0f, 24f);

        var barImg = barGO.GetComponent<Image>();
        barImg.color        = new Color(0.11f, 0.11f, 0.14f, 1f);
        barImg.raycastTarget = false;

        // Optional separator line at top of bar
        var lineGO = new GameObject("Separator",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineGO.transform.SetParent(barGO.transform, false);
        var lineRT = lineGO.GetComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0f, 1f); lineRT.anchorMax = new Vector2(1f, 1f);
        lineRT.pivot     = new Vector2(0.5f, 1f);
        lineRT.anchoredPosition = Vector2.zero; lineRT.sizeDelta = new Vector2(0f, 1f);
        lineGO.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.26f, 1f);

        // Left-aligned status label
        var lblGO = new GameObject("StatusLabel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(barGO.transform, false);
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(10f, 0f); lblRT.offsetMax = new Vector2(-6f, 0f);
        statusLabel               = lblGO.GetComponent<TextMeshProUGUI>();
        statusLabel.fontSize      = 11f;
        statusLabel.color         = new Color(0.60f, 0.59f, 0.57f, 1f);
        statusLabel.alignment     = TextAlignmentOptions.MidlineLeft;
        statusLabel.overflowMode  = TextOverflowModes.Ellipsis;
        statusLabel.raycastTarget = false;

        // Carve 24px from Body bottom so status bar never overlaps the scroll view.
        // Run once only (guard: statusLabel != null prevents re-entry).
        var bodyRT = transform.Find("Body")?.GetComponent<RectTransform>();
        if (bodyRT != null)
        {
            bodyRT.sizeDelta = new Vector2(
                bodyRT.sizeDelta.x,
                bodyRT.sizeDelta.y - 24f);
            bodyRT.anchoredPosition = new Vector2(
                bodyRT.anchoredPosition.x,
                bodyRT.anchoredPosition.y + 12f);
        }
    }

    private void UpdateStatusBar()
    {
        if (statusLabel == null) return;

        if (_selection.Count > 1)
        {
            statusLabel.text = $"{_selection.Count} items selected";
            return;
        }

        if (_selectedItem == null || _selectedItem.Entry == null)
        {
            int n = _items.Count;
            statusLabel.text = n == 0
                ? "Empty folder"
                : n == 1 ? "1 item" : $"{n} items";
            return;
        }

        var e = _selectedItem.Entry;
        statusLabel.text = e.type == FileExplorerManager.EntryType.Folder
            ? $"{e.name}  \u2014  Folder"
            : $"{e.name}  \u2014  {GetTypeLabel(e)}";
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
            _scrollRect = sr;   // cache for ScrollItemIntoView
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

    private FsItemView BuildItemRow(FileExplorerManager.FsEntry entry)
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
        if (entry.type == FileExplorerManager.EntryType.Folder && folderIcon != null) { iconImg.sprite = folderIcon; iconImg.color = Color.white; }
        if (entry.type == FileExplorerManager.EntryType.File   && fileIcon   != null) { iconImg.sprite = fileIcon;   iconImg.color = Color.white; }

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

    private static string GetTypeLabel(FileExplorerManager.FsEntry entry)
    {
        if (entry.type == FileExplorerManager.EntryType.Folder) return "Folder";
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
        var kb   = Keyboard.current;
        bool ctrl  = kb != null && (kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed);
        bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        int  idx   = _items.IndexOf(clicked);

        if (ctrl)
        {
            // Ctrl+click: toggle this item in/out of selection
            if (_selection.Contains(clicked))
            {
                _selection.Remove(clicked);
                clicked.SetSelected(false);
                // Move anchor to last remaining selected item (or null)
                _selectedItem = _selection.Count > 0 ? _selection[_selection.Count - 1] : null;
            }
            else
            {
                _selection.Add(clicked);
                clicked.SetSelected(true);
                _selectedItem    = clicked;
                _lastSelectedIdx = idx;
            }
        }
        else if (shift && _lastSelectedIdx >= 0 && idx >= 0)
        {
            // Shift+click: select range between anchor and clicked item
            // Deselect previous selection first
            foreach (var v in _selection) v.SetSelected(false);
            _selection.Clear();

            int lo = Mathf.Min(_lastSelectedIdx, idx);
            int hi = Mathf.Max(_lastSelectedIdx, idx);
            for (int i = lo; i <= hi; i++)
            {
                if (i < 0 || i >= _items.Count) continue;
                _items[i].SetSelected(true);
                _selection.Add(_items[i]);
            }
            _selectedItem = clicked; // anchor stays at original, status shows clicked
        }
        else
        {
            // Plain click: clear all, select only this item
            foreach (var v in _selection) if (v != clicked) v.SetSelected(false);
            _selection.Clear();
            clicked.SetSelected(true);
            _selection.Add(clicked);
            _selectedItem    = clicked;
            _lastSelectedIdx = idx;
        }

        UpdateStatusBar();
    }

    private void OnItemDoubleClick(FileExplorerManager.FsEntry entry)
    {
        if (entry.type == FileExplorerManager.EntryType.Folder)
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

        foreach (var root in FileExplorerManager.SidebarRoots)
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

    // ── Box select ────────────────────────────────────────────────────────
    private void EnsureBoxSelect()
    {
        if (_boxSelect != null) { _boxSelect.Items = _items; return; }

        // Attach to FileScrollView — the full content viewport that receives pointer
        // events on empty space. FsItemView rows sit inside it and consume their own
        // clicks first, so the box only starts on a true empty-area pointer-down.
        var scrollViewT = transform.Find("Body/ContentArea/FileScrollView");
        if (scrollViewT == null) return;

        _boxSelect = scrollViewT.gameObject.AddComponent<FsBoxSelect>();
        _boxSelect.Items          = _items;
        _boxSelect.OnBoxSelection = (list, additive) => ApplyBoxSelection(list, additive);
    }

    /// Called by FsBoxSelect when the drag-select box is released.
    /// Replaces the current selection with all rows inside the box.
    private void EnsureBreadcrumb()
    {
        if (_breadcrumb != null) return;
        var pathBarT = transform.Find("NavigationBar/PathBar");
        if (pathBarT == null) return;
        _breadcrumb = pathBarT.gameObject.AddComponent<FsBreadcrumb>();
    }

    private void ApplyBoxSelection(System.Collections.Generic.List<FsItemView> inside, bool additive)
    {
        if (!additive)
        {
            // Plain drag: replace selection
            foreach (var v in _selection) v.SetSelected(false);
            _selection.Clear();
        }

        foreach (var v in inside)
        {
            if (_selection.Contains(v)) continue; // already selected (additive mode)
            v.SetSelected(true);
            _selection.Add(v);
        }

        // Primary selected item = first in list (top-most row)
        _selectedItem    = _selection.Count > 0 ? _selection[0] : null;
        _lastSelectedIdx = _selectedItem != null ? _items.IndexOf(_selectedItem) : -1;

        UpdateStatusBar();
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
