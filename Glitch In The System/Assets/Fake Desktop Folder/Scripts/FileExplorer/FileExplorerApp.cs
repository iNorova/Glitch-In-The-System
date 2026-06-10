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
    private FsContextMenu   _contextMenu;
    private FsRenameOverlay _renameOverlay;

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

        EnsurePathText();
        EnsureContentLayout();
        WireRefreshButton();
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
        if (!ctrl) return;

        if (kb.cKey.wasPressedThisFrame)      CopySelected();
        else if (kb.vKey.wasPressedThisFrame) PasteClipboard();
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
        Debug.Log($"[FileExplorer] Copied (snapshot): name={e.name} type={e.type}");
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
            // Folder deep-copy would require recursive clone — deferred, log for now
            Debug.Log("[FileExplorer] Paste: folder copy not yet supported.");
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
        if (newEntry != null)
            Debug.Log($"[FileExplorer] Pasted new entry: {newEntry.fullPath}");
        else
            Debug.LogWarning($"[FileExplorer] Paste failed — could not create '{candidate}' in '{CurrentPath}'");
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
        Debug.Log($"[Rename] BeginRename view={view?.name ?? "NULL"} entry={view?.Entry.name ?? "NULL"}");
        if (view == null) return;
        _renameOverlay.Show(
            view.GetComponent<RectTransform>(),
            view.Entry.name,
            newName =>
            {
                var fs = FileSystemManager.Instance;
                if (fs != null) fs.Rename(view.Entry.fullPath, newName);
            },
            () => { /* cancelled */ }
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

    // ── Drag & drop acceptance (content area) ─────────────────────────────
    private void OnItemReceivedDrop(FsItemView target, FsItemView dragged)
    {
        if (target == null || dragged == null) return;
        if (target.Entry.type != FileSystemManager.EntryType.Folder) return;

        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        bool ok = fs.Move(dragged.Entry.fullPath, target.Entry.fullPath);
        if (!ok)
            Debug.LogWarning($"[FileExplorer] Move failed: {dragged.Entry.fullPath} → {target.Entry.fullPath}");

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
        _menuItems.Add(("Paste",      () => PasteClipboard()));
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
            Debug.LogWarning($"[FileExplorer] Sidebar move blocked: cannot move '{draggedPath}' into its own descendant '{targetPath}'");
            return;
        }

        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        bool ok = fs.Move(draggedPath, targetPath);
        if (ok)
        {
            // Sidebar roots list is static — rebuild sidebar to reflect new hierarchy
            BuildSidebar();
            Debug.Log($"[FileExplorer] Sidebar move: '{draggedPath}' → '{targetPath}'");
        }
        else
            Debug.LogWarning($"[FileExplorer] Sidebar move failed: '{draggedPath}' → '{targetPath}'");
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
            _items.Add(view);
        }
    }

    private static void UpdateTypeLabel(FsItemView view, FileSystemManager.FsEntry entry)
    {
        var t = view.transform;
        if (t.childCount < 3) return;
        var typeTMP = t.GetChild(2).GetComponent<TMPro.TextMeshProUGUI>();
        if (typeTMP != null) typeTMP.text = GetTypeLabel(entry);
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
        iconImg.color = GetIconColor(entry);
        iconImg.raycastTarget = false;
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

        var typeGO = new GameObject("Type",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        typeGO.transform.SetParent(go.transform, false);
        typeGO.AddComponent<LayoutElement>().preferredWidth = 64f;
        var typeTMP = typeGO.GetComponent<TextMeshProUGUI>();
        typeTMP.text          = GetTypeLabel(entry);
        typeTMP.fontSize      = 11;
        typeTMP.color         = new Color(0.55f, 0.54f, 0.52f, 0.85f);
        typeTMP.alignment     = TextAlignmentOptions.MidlineRight;
        typeTMP.raycastTarget = false;

        var view = go.GetComponent<FsItemView>();
        view.SetRefs(bgImg, iconImg, tmp);
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
        var pathBar = navBar.Find("PathBar");
        if (pathBar == null) return;

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

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SetButtonLabelAlpha(Button btn, bool active)
    {
        if (btn == null) return;
        var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (lbl != null) lbl.alpha = active ? 1f : 0.30f;
    }
}
