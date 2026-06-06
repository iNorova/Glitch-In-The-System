using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

/// <summary>
/// Individual sticky note card.
/// Supports: color, delete, timestamp, drag-reorder, persistence data export/import.
///
/// AUTOBIND: If any [SerializeField] reference is null in Awake (e.g. when spawned
/// from a scene template clone rather than a prefab), AutoBind() finds children by
/// their canonical names. This makes the component work correctly whether spawned
/// from a prefab or a scene template clone.
///
/// SAFE TEMPLATE COEXISTENCE: If this component runs on the NoteTemplate scene object
/// itself (before the app deactivates it), _app remains null and all button callbacks
/// are no-ops. No crash, no visible side effect.
/// </summary>
public sealed class StickyNote : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button         deleteButton;
    [SerializeField] private Image          cardBackground;
    [SerializeField] private TMP_Text       timestampLabel;

    [Header("Color Options")]
    [SerializeField] private Button colorBtn0;
    [SerializeField] private Button colorBtn1;
    [SerializeField] private Button colorBtn2;

    [Header("Colors")]
    [SerializeField] private Color color0 = new Color(0.98f, 0.93f, 0.55f);
    [SerializeField] private Color color1 = new Color(0.55f, 0.93f, 0.71f);
    [SerializeField] private Color color2 = new Color(0.55f, 0.75f, 0.98f);

    [Header("Hover")]
    [SerializeField] private float hoverScaleMultiplier = 1.02f;
    [SerializeField] private float hoverSpeed = 8f;

    // ── State ──────────────────────────────────────────────────────────────
    private StickyNotesApp _app;
    private Color          _currentColor;
    private string         _createdAt;
    private Vector3        _baseScale;
    private bool           _hovering;

    // Drag state
    private RectTransform _rectTransform;
    private Canvas        _canvas;
    private Vector2       _dragOffset;

    // ── Data ───────────────────────────────────────────────────────────────
    [Serializable]
    public struct NoteData
    {
        public string text;
        public float  r, g, b;
        public string createdAt;
    }

    // ── Public accessors ───────────────────────────────────────────────────
    /// <summary>
    /// Returns the TMP_InputField for this note.
    /// Used by StickyNotesApp to force focus after spawning a new note.
    /// </summary>
    public TMP_InputField GetInputField() => inputField;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();

        // Guard: base scale must never be zero (causes lerp to freeze at zero).
        _baseScale = transform.localScale;
        if (_baseScale == Vector3.zero) _baseScale = Vector3.one;

        // AutoBind fills any null Inspector references by searching child names.
        // This handles the scene-template-clone path where prefab serialization
        // is not available. Safe no-op when all refs are already wired from prefab.
        AutoBind();

        WireButtons();
    }

    /// <summary>
    /// Finds child components by canonical name when Inspector references are null.
    /// Does NOT overwrite references that are already set.
    /// Searches the full child hierarchy (includeInactive: true) so nested objects work.
    /// Logs a warning for each expected reference that cannot be resolved, without crashing.
    /// </summary>
    private void AutoBind()
    {
        // TMP_InputField — child named "InputField"
        if (inputField == null)
        {
            inputField = FindInChildren<TMP_InputField>("InputField");
            if (inputField == null)
                Debug.LogWarning($"[StickyNote] AutoBind: 'InputField' TMP_InputField not found on {name}. " +
                                 "Text input will not work.", this);
        }

        // Card background Image — the Image component on THIS root object
        if (cardBackground == null)
        {
            cardBackground = GetComponent<Image>();
            if (cardBackground == null)
                Debug.LogWarning($"[StickyNote] AutoBind: No Image component on root of {name}. " +
                                 "Color changes will not display.", this);
        }

        // Delete button — child named "DeleteButton" (may be nested under NoteBar)
        if (deleteButton == null)
        {
            deleteButton = FindInChildren<Button>("DeleteButton");
            if (deleteButton == null)
                Debug.LogWarning($"[StickyNote] AutoBind: 'DeleteButton' not found on {name}.", this);
        }

        // Timestamp label — child named "TimestampLabel"
        if (timestampLabel == null)
            timestampLabel = FindInChildren<TMP_Text>("TimestampLabel");
        // Timestamp is optional — no warning if missing.

        // Color buttons — children named "ColorBtn0", "ColorBtn1", "ColorBtn2"
        if (colorBtn0 == null) colorBtn0 = FindInChildren<Button>("ColorBtn0");
        if (colorBtn1 == null) colorBtn1 = FindInChildren<Button>("ColorBtn1");
        if (colorBtn2 == null) colorBtn2 = FindInChildren<Button>("ColorBtn2");
    }

    /// <summary>Finds the first component of type T whose GameObject name exactly matches.</summary>
    private T FindInChildren<T>(string childName) where T : Component
    {
        foreach (T comp in GetComponentsInChildren<T>(includeInactive: true))
            if (comp.gameObject.name == childName)
                return comp;
        return null;
    }

    private void WireButtons()
    {
        // All callbacks guard against null _app — safe even on the template scene object
        // where Init() is never called and _app remains null.
        deleteButton?.onClick.AddListener(OnDelete);
        colorBtn0?.onClick.AddListener(() => SetColor(color0));
        colorBtn1?.onClick.AddListener(() => SetColor(color1));
        colorBtn2?.onClick.AddListener(() => SetColor(color2));
        inputField?.onValueChanged.AddListener(_ => _app?.ScheduleSave());
    }

    private void Update()
    {
        Vector3 target = _hovering ? _baseScale * hoverScaleMultiplier : _baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * hoverSpeed);
    }

    public void Init(StickyNotesApp app, NoteData? data = null)
    {
        _app    = app;
        _canvas = GetComponentInParent<Canvas>();

        if (data.HasValue)
        {
            var d = data.Value;
            _currentColor = new Color(d.r, d.g, d.b);
            _createdAt    = d.createdAt;
            if (inputField != null) inputField.text = d.text;
        }
        else
        {
            _currentColor = color0;
            _createdAt    = DateTime.Now.ToString("MMM d  h:mmtt").ToLower();
        }

        SetColor(_currentColor);
        RefreshTimestamp();
    }

    // ── Public API ─────────────────────────────────────────────────────────
    public NoteData ExportData() => new NoteData
    {
        text      = inputField != null ? inputField.text : "",
        r         = _currentColor.r,
        g         = _currentColor.g,
        b         = _currentColor.b,
        createdAt = _createdAt
    };

    // ── Internal ───────────────────────────────────────────────────────────
    private void OnDelete() => _app?.RemoveNote(this);

    private void SetColor(Color c)
    {
        _currentColor = c;
        if (cardBackground != null) cardBackground.color = c;
        _app?.ScheduleSave();
    }

    private void RefreshTimestamp()
    {
        if (timestampLabel != null)
            timestampLabel.text = _createdAt;
    }

    // ── Drag reorder ───────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        transform.SetAsLastSibling();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, e.position, e.pressEventCamera, out var local);
        _dragOffset = local;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_canvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform.parent as RectTransform,
            e.position, e.pressEventCamera, out var parentLocal);
        _rectTransform.anchoredPosition = parentLocal - _dragOffset;

        var parent = transform.parent;
        int newIndex = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var sibling = parent.GetChild(i);
            if (sibling == transform) continue;
            var sibRT = sibling.GetComponent<RectTransform>();
            if (sibRT != null && _rectTransform.anchoredPosition.y < sibRT.anchoredPosition.y)
                newIndex = i + 1;
        }
        transform.SetSiblingIndex(newIndex);
    }

    public void OnEndDrag(PointerEventData e)
    {
        _rectTransform.anchoredPosition = Vector2.zero;
        _app?.ScheduleSave();
    }

    // ── Hover ──────────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData e) => _hovering = true;
    public void OnPointerExit(PointerEventData e)  => _hovering = false;
}
