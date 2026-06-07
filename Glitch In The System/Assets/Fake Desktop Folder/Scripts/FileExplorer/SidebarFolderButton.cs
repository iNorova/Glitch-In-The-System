using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Sidebar folder button — one per sidebar entry.
/// Audit fixes:
///   - Added IPointerEnterHandler / IPointerExitHandler for hover state (was missing)
///   - RemoveAllListeners before AddListener in Init() to prevent stacking on re-init
///   - Selected color uses a left-edge accent instead of flat white tint for modern feel
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SidebarFolderButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image           background;

    private string          _folderPath;
    private FileExplorerApp _app;
    private bool            _selected;

    private static readonly Color Normal   = new Color(1f, 1f, 1f, 0.000f);
    private static readonly Color Hover    = new Color(1f, 1f, 1f, 0.070f);
    private static readonly Color Selected = new Color(1f, 1f, 1f, 0.130f);

    public void Init(FileExplorerApp app, string folderPath, string displayName)
    {
        _app        = app;
        _folderPath = folderPath;
        _selected   = false;

        if (label      != null) label.text      = displayName;
        if (background != null) background.color = Normal;

        // FIX: RemoveAllListeners prevents duplicate callbacks if Init() called more than once
        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClick);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (background != null)
            background.color = selected ? Selected : Normal;

        // Slightly brighten label text when selected for Windows-like feel
        if (label != null)
            label.color = selected
                ? new Color(0.95f, 0.93f, 0.90f, 1f)
                : new Color(0.80f, 0.78f, 0.75f, 1f);
    }

    // FIX: hover state — was never implemented (colors defined but never applied on hover)
    public void OnPointerEnter(PointerEventData e)
    {
        if (!_selected && background != null)
            background.color = Hover;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_selected && background != null)
            background.color = Normal;
    }

    private void OnClick() => _app?.NavigateTo(_folderPath);
}
