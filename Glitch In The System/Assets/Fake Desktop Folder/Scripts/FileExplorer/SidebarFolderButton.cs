using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

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
    private bool            _dropHovered;
    private GameObject      _accentBar;

    private static SidebarFolderButton _dragging;
    private static GameObject          _sidebarDragGhost;

    private static readonly Color Normal          = new Color(1f,    1f,    1f,    0.000f);
    private static readonly Color Hover           = new Color(1f,    1f,    1f,    0.070f);
    private static readonly Color Selected        = new Color(1f,    1f,    1f,    0.130f);
    private static readonly Color DropTarget      = new Color(0.35f, 0.65f, 1.00f, 0.18f);
    private static readonly Color LabelNormal     = new Color(0.80f, 0.78f, 0.75f, 1f);
    private static readonly Color LabelSelected   = new Color(0.95f, 0.93f, 0.90f, 1f);
    private static readonly Color LabelDropTarget = new Color(0.92f, 0.96f, 1.00f, 1f);

    public void Init(FileExplorerApp app, string folderPath, string displayName)
    {
        _app        = app;
        _folderPath = folderPath;
        _selected   = false;

        // Auto-wire refs if not set in Inspector (runtime-built buttons)
        if (background == null) background = GetComponent<Image>();
        if (label      == null) label      = GetComponentInChildren<TextMeshProUGUI>(true);

        if (label      != null) { label.text = displayName; label.color = LabelNormal; }
        if (background != null) background.color = Normal;

        GetComponent<Button>().onClick.RemoveAllListeners();
        GetComponent<Button>().onClick.AddListener(OnClick);

        EnsureAccentBar();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (background != null) background.color = selected ? Selected : Normal;
        if (label      != null) label.color       = selected ? LabelSelected : LabelNormal;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (FsItemView.IsDragging || _dragging != null) return;
        if (!_selected && background != null) background.color = Hover;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (FsItemView.IsDragging || _dragging != null) return;
        if (!_selected)
        {
            if (background != null) background.color = Normal;
            if (label      != null) label.color       = LabelNormal;
        }
    }

    private void Update()
    {
        bool anyExternalDrag = FsItemView.IsDragging || (_dragging != null && _dragging != this);

        if (!anyExternalDrag)
        {
            if (_dropHovered) { _dropHovered = false; ApplyDropHover(false); }
            return;
        }

        if (Mouse.current == null) return;

        var     rt      = GetComponent<RectTransform>();
        var     canvas  = GetComponentInParent<Canvas>();
        Vector2 mPos    = Mouse.current.position.ReadValue();
        bool    over    = RectTransformUtility.RectangleContainsScreenPoint(
                              rt, mPos, canvas != null ? canvas.worldCamera : null);

        if (over  && !_dropHovered) { _dropHovered = true;  ApplyDropHover(true);  }
        if (!over &&  _dropHovered) { _dropHovered = false; ApplyDropHover(false); }
    }

    private void ApplyDropHover(bool active)
    {
        if (background != null)
            background.color = active ? DropTarget : (_selected ? Selected : Normal);
        if (label != null)
            label.color = active ? LabelDropTarget : (_selected ? LabelSelected : LabelNormal);
        SetAccentBar(active);
    }

    private void EnsureAccentBar()
    {
        if (_accentBar != null || background == null) return;
        _accentBar = new GameObject("AccentBar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _accentBar.transform.SetParent(transform, false);
        var rt = _accentBar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f,0f); rt.anchorMax = new Vector2(0f,1f);
        rt.pivot = new Vector2(0f,0.5f); rt.sizeDelta = new Vector2(3f,-6f);
        rt.anchoredPosition = Vector2.zero;
        var img = _accentBar.GetComponent<Image>();
        img.color = new Color(0.40f,0.70f,1.00f,0.90f); img.raycastTarget = false;
        _accentBar.SetActive(false);
    }

    private void SetAccentBar(bool visible)
    {
        if (_accentBar != null) _accentBar.SetActive(visible);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _dragging = this;
        if (background != null) background.color = new Color(1f,1f,1f,0.03f);
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        if (_sidebarDragGhost == null)
        {
            _sidebarDragGhost = new GameObject("SidebarDragGhost",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            _sidebarDragGhost.transform.SetParent(canvas.transform, false);
            var rt = _sidebarDragGhost.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f,26f);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f,1f);
            var tmp = _sidebarDragGhost.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 12; tmp.color = new Color(0.80f,0.78f,0.75f,0.70f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.raycastTarget = false;
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
            canvas.transform as RectTransform, e.position, canvas.worldCamera, out var local);
        _sidebarDragGhost.GetComponent<RectTransform>().anchoredPosition = local + new Vector2(10f,-6f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_dragging == this) _dragging = null;
        if (_sidebarDragGhost != null) _sidebarDragGhost.SetActive(false);
        _dropHovered = false;
        ApplyDropHover(false);
        if (background != null) background.color = _selected ? Selected : Normal;
    }

    public void OnDrop(PointerEventData e)
    {
        _dropHovered = false;
        ApplyDropHover(false);
        if (_dragging != null && _dragging != this)
        {
            _app?.OnSidebarDrop(_dragging._folderPath, _folderPath);
            return;
        }
        var dragged = FsItemView.DraggingItem;
        if (dragged != null)
            _app?.MoveEntryTo(dragged.Entry.fullPath, _folderPath);
    }

    private void OnClick() => _app?.NavigateTo(_folderPath);
}
