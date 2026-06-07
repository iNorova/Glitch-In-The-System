using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

/// <summary>
/// Individual sticky note card.
/// Supports: color, delete, timestamp, drag-reorder, persistence data export/import.
///
/// INPUT PRIORITY (enforced explicitly):
///   1. Dragging note  → StickyNote owns drag; ScrollRect frozen via velocity=0 during drag.
///   2. Scroll wheel   → always forwarded to _parentScrollRect regardless of state.
///   3. Typing         → InputField gets pointer-down; scroll and drag unaffected.
///
/// AUTOBIND: null Inspector refs filled by name search in Awake.
/// SAFE TEMPLATE COEXISTENCE: _app==null guards all callbacks before Init().
/// </summary>
public sealed class StickyNote : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler,
    IScrollHandler
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
    private bool           _isDragging;

    // Drag state
    private RectTransform                  _rectTransform;
    private Canvas                         _canvas;
    private Vector2                        _dragOffset;
    private Vector3                        _dragWorldPos;
    private LayoutElement                  _layoutElement;
    private ScrollRect                     _parentScrollRect;

    // ── Data ───────────────────────────────────────────────────────────────
    [Serializable]
    public struct NoteData
    {
        public string text;
        public float  r, g, b;
        public string createdAt;
    }

    public TMP_InputField GetInputField() => inputField;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _baseScale = transform.localScale;
        if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
        AutoBind();
        WireButtons();
    }

    private void AutoBind()
    {
        if (inputField    == null) inputField    = FindInChildren<TMP_InputField>("InputField");
        if (cardBackground == null) cardBackground = GetComponent<Image>();
        if (deleteButton  == null) deleteButton  = FindInChildren<Button>("DeleteButton");
        if (timestampLabel == null) timestampLabel = FindInChildren<TMP_Text>("TimestampLabel");
        if (colorBtn0 == null) colorBtn0 = FindInChildren<Button>("ColorBtn0");
        if (colorBtn1 == null) colorBtn1 = FindInChildren<Button>("ColorBtn1");
        if (colorBtn2 == null) colorBtn2 = FindInChildren<Button>("ColorBtn2");
    }

    private T FindInChildren<T>(string childName) where T : Component
    {
        foreach (T comp in GetComponentsInChildren<T>(includeInactive: true))
            if (comp.gameObject.name == childName) return comp;
        return null;
    }

    private void WireButtons()
    {
        deleteButton?.onClick.AddListener(OnDelete);
        colorBtn0?.onClick.AddListener(() => SetColor(color0));
        colorBtn1?.onClick.AddListener(() => SetColor(color1));
        colorBtn2?.onClick.AddListener(() => SetColor(color2));
        inputField?.onValueChanged.AddListener(_ => _app?.ScheduleSave());
    }

    private void Update()
    {
        Vector3 target = _hovering ? _baseScale * hoverScaleMultiplier : _baseScale;
        if (!_hovering && (transform.localScale - _baseScale).sqrMagnitude < 1e-6f) return;
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * hoverSpeed);
    }

    public void Init(StickyNotesApp app, NoteData? data = null)
    {
        _app              = app;
        _canvas           = GetComponentInParent<Canvas>();
        _parentScrollRect = GetComponentInParent<ScrollRect>();

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

    public NoteData ExportData() => new NoteData
    {
        text      = inputField != null ? inputField.text : "",
        r         = _currentColor.r,
        g         = _currentColor.g,
        b         = _currentColor.b,
        createdAt = _createdAt
    };

    private void OnDelete()    => _app?.RemoveNote(this);
    private void SetColor(Color c) { _currentColor = c; if (cardBackground != null) cardBackground.color = c; _app?.ScheduleSave(); }
    private void RefreshTimestamp() { if (timestampLabel != null) timestampLabel.text = _createdAt; }

    // ── Drag reorder ───────────────────────────────────────────────────────
    // FIX: Do NOT forward drag events to ScrollRect.
    // Previously, forwarding OnBeginDrag + OnDrag to parent caused ScrollRect to
    // also move content while StickyNote moved the card — double-movement = jitter.
    // Correct approach: StickyNote owns the drag exclusively. ScrollRect is frozen
    // (velocity zeroed) during drag so it doesn't fight. Scroll wheel still works
    // via OnScroll which bypasses drag state entirely.
    public void OnBeginDrag(PointerEventData e)
    {
        _isDragging = true;

        if (_parentScrollRect != null)
            _parentScrollRect.velocity = Vector2.zero;

        // Remove from VLG flow so manual position doesn't fight layout.
        // ignoreLayout=true makes VLG skip this child; it still renders in place.
        _layoutElement = GetComponent<LayoutElement>();
        if (_layoutElement == null) _layoutElement = gameObject.AddComponent<LayoutElement>();
        _layoutElement.ignoreLayout = true;

        // Capture world position BEFORE leaving the layout flow so we can
        // convert it to a parent-local position for free dragging.
        _dragWorldPos = _rectTransform.position;

        // Record pointer offset from note pivot in canvas space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, e.position, e.pressEventCamera, out _dragOffset);

        // Lift above siblings visually
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (_canvas == null) return;

        // Move freely — VLG is not controlling this note during drag
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform.parent as RectTransform,
            e.position, e.pressEventCamera, out var parentLocal);
        _rectTransform.anchoredPosition = parentLocal - _dragOffset;

        // Determine insertion index by comparing note's world-Y to siblings' world-Y midpoints.
        // Siblings are still in VLG flow so their positions are stable and reliable.
        var parent = transform.parent;
        float myY = _rectTransform.position.y;
        int newIndex = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var sibling = parent.GetChild(i);
            if (sibling == transform) continue;
            // Use world position midpoint of each sibling
            if (myY < sibling.position.y)
                newIndex = i + 1;
        }
        transform.SetSiblingIndex(newIndex);
    }

    public void OnEndDrag(PointerEventData e)
    {
        _isDragging = false;

        // Re-enter VLG flow — layout will snap card to its correct slot next frame
        if (_layoutElement != null)
            _layoutElement.ignoreLayout = false;

        // Clear manual position so VLG positions it cleanly
        _rectTransform.anchoredPosition = Vector2.zero;

        _app?.ScheduleSave();
    }

    // ── Hover ──────────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData e) => _hovering = true;
    public void OnPointerExit(PointerEventData e)  => _hovering = false;

    // ── Scroll forwarding ──────────────────────────────────────────────────
    // Always forward scroll to ScrollRect — even during drag, even when InputField
    // is focused. This is the single reliable path for scroll to reach the viewport.
    // TMP_InputField.OnScroll swallows events when it has focus; because StickyNote
    // root Image has raycastTarget=true, StickyNote receives scroll first and forwards
    // it here before InputField ever sees it.
    public void OnScroll(PointerEventData e)
    {
        if (_parentScrollRect != null)
            _parentScrollRect.OnScroll(e);
    }
}
