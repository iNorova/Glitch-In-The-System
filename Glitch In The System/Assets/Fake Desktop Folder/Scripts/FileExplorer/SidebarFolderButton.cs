using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Sidebar folder button — one per sidebar entry.
/// Upgrade: added IBeginDragHandler/IDragHandler/IEndDragHandler/IDropHandler
///          so folders can be dragged from the sidebar and dropped onto other sidebar folders.
///          Drop calls FileExplorerApp.OnSidebarDrop() which validates and calls FileSystemManager.Move().
///          Guards: circular parenting + self-drop prevented in FileExplorerApp.OnSidebarDrop().
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SidebarFolderButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image           background;

    private string          _folderPath;
    private FileExplorerApp _app;
    private bool            _selected;

    private static readonly Color Normal     = new Color(1f, 1f, 1f, 0.000f);
    private static readonly Color Hover      = new Color(1f, 1f, 1f, 0.070f);
    private static readonly Color Selected   = new Color(1f, 1f, 1f, 0.130f);
    private static readonly Color DropTarget = new Color(0.30f, 0.70f, 0.40f, 0.25f);

    // Static drag state shared across all sidebar buttons
    private static SidebarFolderButton _dragging;
    private static GameObject          _sidebarDragGhost;

    public void Init(FileExplorerApp app, string folderPath, string displayName)
    {
        _app        = app;
        _folderPath = folderPath;
        _selected   = false;

        if (label      != null) label.text       = displayName;
        if (background != null) background.color = Normal;

        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClick);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (background != null)
            background.color = selected ? Selected : Normal;
        if (label != null)
            label.color = selected
                ? new Color(0.95f, 0.93f, 0.90f, 1f)
                : new Color(0.80f, 0.78f, 0.75f, 1f);
    }

    // ── Hover ──────────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData e)
    {
        // If something is dragging, show drop target highlight
        if (_dragging != null && _dragging != this)
        {
            if (background != null) background.color = DropTarget;
            return;
        }
        if (!_selected && background != null) background.color = Hover;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_dragging != null && _dragging != this)
        {
            if (background != null) background.color = Normal;
            return;
        }
        if (!_selected && background != null) background.color = Normal;
    }

    // ── Drag ───────────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _dragging = this;

        // Dim self during drag
        if (background != null) background.color = new Color(1f, 1f, 1f, 0.03f);

        // Build or reuse ghost label
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (_sidebarDragGhost == null)
        {
            _sidebarDragGhost = new GameObject("SidebarDragGhost",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            _sidebarDragGhost.transform.SetParent(canvas.transform, false);

            var rt = _sidebarDragGhost.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 26f);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);

            var tmp = _sidebarDragGhost.GetComponent<TextMeshProUGUI>();
            tmp.fontSize      = 12;
            tmp.color         = new Color(0.80f, 0.78f, 0.75f, 0.70f);
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
        }

        var ghostTMP = _sidebarDragGhost.GetComponent<TextMeshProUGUI>();
        if (ghostTMP != null) ghostTMP.text = label != null ? label.text : _folderPath;
        _sidebarDragGhost.SetActive(true);
        _sidebarDragGhost.transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (_dragging != this || _sidebarDragGhost == null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            e.position, canvas.worldCamera, out var local);

        var rt = _sidebarDragGhost.GetComponent<RectTransform>();
        rt.anchoredPosition = local + new Vector2(10f, -6f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_dragging == this) _dragging = null;
        if (_sidebarDragGhost != null) _sidebarDragGhost.SetActive(false);

        // Restore visual
        if (background != null)
            background.color = _selected ? Selected : Normal;
    }

    // ── Drop target ────────────────────────────────────────────────────────
    public void OnDrop(PointerEventData e)
    {
        // Clear drop highlight
        if (!_selected && background != null) background.color = Normal;

        if (_dragging == null || _dragging == this) return;

        // Delegate validation + move to FileExplorerApp (has access to FileSystemManager)
        _app?.OnSidebarDrop(_dragging._folderPath, _folderPath);
    }

    private void OnClick() => _app?.NavigateTo(_folderPath);
}
