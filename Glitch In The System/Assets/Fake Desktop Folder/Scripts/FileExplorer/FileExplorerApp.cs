using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// File Explorer — Batches 1–7.
/// Audit fixes applied:
///   - EnsureContentLayout() guarded — only runs once, not every navigation
///   - FsContextMenu.Init() guarded — panel built once, not every OnEnable
///   - Path breadcrumb display fixed — leading separator stripped correctly
///   - EmptyLabel moved outside FileContent (ContentSizeFitter) so it always shows
///   - BuildSidebar guard improved
///   - SidebarFolderButton listener safety (RemoveAllListeners before Add)
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

    // ── Sub-systems ───────────────────────────────────────────────────────
    private FsContextMenu   _contextMenu;
    private FsRenameOverlay _renameOverlay;

    // ── Navigation ────────────────────────────────────────────────────────
    private readonly List<string>              _history     = new();
    private int                                _histIdx     = -1;
    private readonly List<SidebarFolderButton> _sidebarBtns = new();

    // ── Content ───────────────────────────────────────────────────────────
    private readonly List<FsItemView> _items        = new();
    private FsItemView                _selectedItem;
    private bool                      _contentLayoutReady; // FIX: guard EnsureContentLayout

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private void Awake()
    {
        // Context menu — created once here, Init() called in OnEnable
        var cmGO = new GameObject("FsContextMenu", typeof(RectTransform), typeof(FsContextMenu));
        cmGO.transform.SetParent(transform, false);
        var cmRT = cmGO.GetComponent<RectTransform>();
        cmRT.anchorMin = Vector2.zero; cmRT.anchorMax = Vector2.one;
        cmRT.offsetMin = Vector2.zero; cmRT.offsetMax = Vector2.zero;
        _contextMenu = cmGO.GetComponent<FsContextMenu>();

        // Rename overlay
        var rnGO = new GameObject("FsRenameOverlay", typeof(RectTransform), typeof(FsRenameOverlay));
        rnGO.transform.SetParent(transform, false);
        _renameOverlay = rnGO.GetComponent<FsRenameOverlay>();
        _renameOverlay.Init();

        EnsurePathText();
        EnsureContentLayout(); // run once at awake, not per-navigation
    }

    private void OnEnable()
    {
        var canvas = GetComponentInParent<Canvas>();

        // FIX: Init is now guarded inside FsContextMenu — safe to call every OnEnable
        if (_contextMenu != null) _contextMenu.Init(canvas);

        // FIX: RemoveAllListeners before re-wiring (prevents stacking on re-enable)
        if (backButton    != null) { backButton.onClick.RemoveAllListeners();    backButton.onClick.AddListener(GoBack);    }
        if (forwardButton != null) { forwardButton.onClick.RemoveAllListeners(); forwardButton.onClick.AddListener(GoForward); }

        // FIX: rebuild sidebar if it was destroyed or never built
        if (_sidebarBtns.Count == 0 || (sidebarContent != null && sidebarContent.childCount == 0))
            BuildSidebar();

        if (_histIdx < 0) NavigateTo("/Desktop");
        else              RefreshUI();
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

        PopulateContent(CurrentPath);
        var view = _items.Find(i => i.Entry.fullPath == entry.fullPath);
        if (view != null) BeginRename(view);
    }

    public void DeleteSelected()
    {
        if (_selectedItem == null) return;
        var fs = FileSystemManager.Instance;
        if (fs == null) return;

        string path = _selectedItem.Entry.fullPath;
        fs.Delete(path);
        _selectedItem = null;
        PopulateContent(CurrentPath);
    }

    public void BeginRename(FsItemView view)
    {
        if (view == null) return;
        _renameOverlay.Show(
            view.GetComponent<RectTransform>(),
            view.Entry.name,
            newName =>
            {
                var fs = FileSystemManager.Instance;
                if (fs != null) fs.Rename(view.Entry.fullPath, newName);
                PopulateContent(CurrentPath);
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
        PopulateContent(CurrentPath);
    }

    // ── Context menu ──────────────────────────────────────────────────────
    private void ShowItemContextMenu(FsItemView view, Vector2 screenPos)
    {
        bool isFolder = view.Entry.type == FileSystemManager.EntryType.Folder;

        var items = new List<(string, System.Action)>
        {
            ("Rename",     () => BeginRename(view)),
            ("Delete",     () => DeleteSelected()),
            ("---",        null),
            ("New Folder", () => CreateFolder()),
        };

        if (isFolder)
            items.Insert(0, ("Open", () => NavigateTo(view.Entry.fullPath)));

        _contextMenu.ShowAt(screenPos, items);
    }

    private void ShowBackgroundContextMenu(Vector2 screenPos)
    {
        _contextMenu.ShowAt(screenPos, new List<(string, System.Action)>
        {
            ("New Folder", () => CreateFolder()),
        });
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right) return;
        if (_contextMenu.IsOpen) { _contextMenu.Hide(); return; }
        ShowBackgroundContextMenu(e.position);
    }

    // ── Refresh / populate ────────────────────────────────────────────────
    private void RefreshUI()
    {
        string path = CurrentPath;

        // FIX: correct breadcrumb — strip leading separator then replace remaining
        if (pathText != null)
        {
            if (string.IsNullOrEmpty(path))
                pathText.text = "File Explorer";
            else
            {
                // "/Desktop" → "Desktop"
                // "/Documents/Work" → "Documents  ›  Work"
                string display = path.TrimStart('/').Replace("/", "  ›  ");
                pathText.text = string.IsNullOrEmpty(display) ? "File Explorer" : display;
            }
        }

        if (backButton    != null) backButton.interactable    = _histIdx > 0;
        if (forwardButton != null) forwardButton.interactable = _histIdx < _history.Count - 1;

        // Sync sidebar selection
        foreach (var btn in _sidebarBtns)
            btn.SetSelected(btn.name == "SidebarBtn_" + path);

        PopulateContent(path);
    }

    private void PopulateContent(string path)
    {
        foreach (var item in _items) if (item != null) Destroy(item.gameObject);
        _items.Clear();
        _selectedItem = null;

        if (fileContent == null) return;

        var fs = FileSystemManager.Instance;
        if (fs == null) return;
        var children = fs.GetChildren(path);

        // FIX: EmptyLabel lives outside FileContent (above Viewport or in ContentArea directly)
        // so ContentSizeFitter collapsing FileContent to 0 doesn't hide it.
        if (emptyLabel != null)
        {
            bool empty = children.Count == 0;
            emptyLabel.text    = empty ? "This folder is empty." : "";
            emptyLabel.enabled = empty;
            emptyLabel.gameObject.SetActive(empty);
        }

        foreach (var entry in children)
            _items.Add(BuildItemRow(entry));
    }

    // FIX: run once (guarded by _contentLayoutReady), not every PopulateContent call
    private void EnsureContentLayout()
    {
        if (_contentLayoutReady || fileContent == null) return;
        _contentLayoutReady = true;

        // FileContent VLG — reduced outer padding since item rows have inner padding
        var vlg = fileContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = fileContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(4, 4, 4, 4); // was L8R8 — reduced to avoid double-padding
        vlg.spacing              = 1;                           // was 2 — tighter, more Windows-like
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = fileContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = fileContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Zero out initial stale sizeDelta
        fileContent.sizeDelta = new Vector2(fileContent.sizeDelta.x, 0f);
    }

    private FsItemView BuildItemRow(FileSystemManager.FsEntry entry)
    {
        bool isFolder = entry.type == FileSystemManager.EntryType.Folder;

        var go = new GameObject("FsItem_" + entry.name,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(FsItemView));
        go.transform.SetParent(fileContent, false);

        go.AddComponent<LayoutElement>().preferredHeight = 32f; // was 36 — tighter, more Windows-like

        var bgImg = go.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0f);
        bgImg.raycastTarget = true;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding        = new RectOffset(6, 8, 0, 0); // was L8R8T4B4 — reduced (VLG outer handles vertical)
        hlg.spacing        = 6;                            // was 8
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        // Icon — colored square (placeholder for real sprites)
        var iconGO = new GameObject("Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        var iconLE  = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth  = 16f; // was 20 — tighter
        iconLE.preferredHeight = 16f;
        var iconImg = iconGO.GetComponent<Image>();

        if (!isFolder && entry.name.EndsWith(".lnk", System.StringComparison.OrdinalIgnoreCase))
            iconImg.color = new Color(0.55f, 0.80f, 1.00f, 1f);
        else if (isFolder)
            iconImg.color = new Color(0.96f, 0.76f, 0.26f, 1f);
        else
            iconImg.color = new Color(0.65f, 0.67f, 0.72f, 1f);

        iconImg.raycastTarget = false;
        if (isFolder && folderIcon != null) { iconImg.sprite = folderIcon; iconImg.color = Color.white; }
        if (!isFolder && fileIcon  != null) { iconImg.sprite = fileIcon;   iconImg.color = Color.white; }

        // Name
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

        // Type column — right-aligned, dimmer
        var typeGO = new GameObject("Type",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        typeGO.transform.SetParent(go.transform, false);
        typeGO.AddComponent<LayoutElement>().preferredWidth = 64f; // was 70
        var typeTMP = typeGO.GetComponent<TextMeshProUGUI>();
        typeTMP.text          = GetTypeLabel(entry);
        typeTMP.fontSize      = 11;
        typeTMP.color         = new Color(0.55f, 0.54f, 0.52f, 0.85f);
        typeTMP.alignment     = TextAlignmentOptions.MidlineRight;
        typeTMP.raycastTarget = false;

        var view = go.GetComponent<FsItemView>();
        view.SetRefs(bgImg, iconImg, tmp);
        view.Init(entry, folderIcon, fileIcon);
        view.OnSingleClick = OnItemSingleClick;
        view.OnDoubleClick = OnItemDoubleClick;
        view.OnRightClick  = (v, pos) => { OnItemSingleClick(v); ShowItemContextMenu(v, pos); };

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
        vlg.spacing              = 1; // was 2
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
        go.AddComponent<LayoutElement>().preferredHeight = 30f; // was 32 — tighter

        var bgImg = go.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0f);
        bgImg.raycastTarget = true;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding        = new RectOffset(10, 8, 0, 0); // indent label from left edge
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
        tmp.text         = displayName;
        tmp.fontSize     = 12;
        tmp.color        = new Color(0.80f, 0.78f, 0.75f, 1f); // slightly dimmer than content text
        tmp.alignment    = TextAlignmentOptions.MidlineLeft;
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
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f,  2f);
        rt.offsetMax = new Vector2(-8f, -2f);

        pathText              = lblGO.GetComponent<TextMeshProUGUI>();
        pathText.fontSize     = 12;
        pathText.color        = new Color(0.82f, 0.80f, 0.76f, 1f);
        pathText.alignment    = TextAlignmentOptions.MidlineLeft;
        pathText.overflowMode  = TextOverflowModes.Ellipsis;
        pathText.raycastTarget = false;
        pathText.text          = "File Explorer";
    }
}
