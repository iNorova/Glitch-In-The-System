using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Full-screen snipping overlay.
/// Attach to SnippingOverlay (child of FakeDesktop, with its own Canvas sortingOrder=9999).
/// Wire: selectionRect, selectionImage in Inspector.
/// The root Image on this GO acts as the dark panel (raycastTarget=true).
/// Call BeginSnip() from SnippingToolApp.
/// </summary>
public sealed class SnippingOverlay : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform selectionRect;  // the bright selection box
    [SerializeField] private Image         selectionImage; // image on selectionRect

    [Header("Appearance")]
    [SerializeField] private Color darkColor      = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color selectionColor = new Color(1f, 1f, 1f, 0.08f);

    // ── Events ────────────────────────────────────────────────────────────────
    public System.Action<Rect> onSnipComplete;
    public System.Action       onSnipCancelled;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool          _active;
    private Vector2       _startPos;     // local-space start corner (within overlay canvas)
    private RectTransform _overlayRT;    // this GO's RectTransform — used for coord conversion
    private Image         _darkImage;    // root Image = dark panel

    // ── Public API ────────────────────────────────────────────────────────────
    public void BeginSnip()
    {
        // Cache lazily here — Awake doesn't fire on inactive GOs reliably.
        _overlayRT = GetComponent<RectTransform>();
        _darkImage = GetComponent<Image>();

        gameObject.SetActive(true);

        // Push to top of FakeDesktop's child stack so it renders above everything
        transform.SetAsLastSibling();

        if (_darkImage != null)
            _darkImage.color = darkColor;

        if (selectionRect != null)
        {
            selectionRect.sizeDelta = Vector2.zero;
            selectionRect.gameObject.SetActive(false);
        }

        _active = true;
        Debug.Log($"[SnippingOverlay] BeginSnip — overlayRT rect={_overlayRT.rect}  pivot={_overlayRT.pivot}  anchMin={_overlayRT.anchorMin}  anchMax={_overlayRT.anchorMax}");
        StartCoroutine(WatchEscape());
    }

    // ── Pointer events ────────────────────────────────────────────────────────
    // NOTE: coordinate space.
    // SnippingOverlay has its OWN Canvas (sortingOrder 9999) but is a child of FakeDesktop.
    // Its RectTransform is full-stretch (0,0)-(1,1) so its local space == FakeDesktop local space.
    // We use _overlayRT (this GO) as the reference — worldCamera=null for ScreenSpaceOverlay.
    // This keeps SelectionRect (child of this GO) in the same space as the pointer math.

    public void OnPointerDown(PointerEventData e)
    {
        if (!_active) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRT, e.position, null, out _startPos);
        Debug.Log($"[SnippingOverlay] PointerDown — screen={e.position}  local={_startPos}");

        if (selectionRect != null)
        {
            selectionRect.gameObject.SetActive(true);
            selectionRect.anchorMin        = new Vector2(0.5f, 0.5f);
            selectionRect.anchorMax        = new Vector2(0.5f, 0.5f);
            selectionRect.pivot            = new Vector2(0f, 0f);  // fixed — no flip
            selectionRect.anchoredPosition = _startPos;
            selectionRect.sizeDelta        = Vector2.zero;

            if (selectionImage != null)
                selectionImage.color = selectionColor;
        }
    }

    private int _dragFrameCount;
    public void OnDrag(PointerEventData e)
    {
        if (!_active || selectionRect == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRT, e.position, null, out var currentPos);
        UpdateSelectionRect(_startPos, currentPos);
        if (++_dragFrameCount % 10 == 0)
            Debug.Log($"[SnippingOverlay] Drag#{_dragFrameCount} — screen={e.position}  local={currentPos}  selRT.aPos={selectionRect.anchoredPosition}  sz={selectionRect.sizeDelta}  pivot={selectionRect.pivot}");
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_active) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRT, e.position, null, out var endPos);

        float x      = Mathf.Min(_startPos.x, endPos.x);
        float y      = Mathf.Min(_startPos.y, endPos.y);
        float width  = Mathf.Abs(endPos.x - _startPos.x);
        float height = Mathf.Abs(endPos.y - _startPos.y);

        var selectedRect = new Rect(x, y, width, height);
        Debug.Log($"[SnippingOverlay] PointerUp — screen={e.position}  localEnd={endPos}  start={_startPos}  RECT x={x:F1} y={y:F1} w={width:F1} h={height:F1}");
        EndSnip();
        onSnipComplete?.Invoke(selectedRect);
    }

    // ── Internal ──────────────────────────────────────────────────────────────
    private void UpdateSelectionRect(Vector2 start, Vector2 current)
    {
        // Fixed pivot=(0,0) = bottom-left corner. No pivot flipping.
        // Compute bottom-left of the selection in canvas-local space,
        // then size is always positive. Eliminates the 1-frame snap
        // that occurs when pivot changes direction mid-drag.
        float left   = Mathf.Min(start.x, current.x);
        float bottom = Mathf.Min(start.y, current.y);
        float w      = Mathf.Abs(current.x - start.x);
        float h      = Mathf.Abs(current.y - start.y);

        selectionRect.pivot            = new Vector2(0f, 0f);
        selectionRect.anchoredPosition = new Vector2(left, bottom);
        selectionRect.sizeDelta        = new Vector2(w, h);
    }

    private void EndSnip()
    {
        _active = false;
        gameObject.SetActive(false);
        if (selectionRect != null)
            selectionRect.gameObject.SetActive(false);
    }

    private void Cancel()
    {
        if (!_active) return;
        EndSnip();
        onSnipCancelled?.Invoke();
    }

    private IEnumerator WatchEscape()
    {
        while (_active)
        {
            if (EscapePressed()) { Cancel(); yield break; }
            yield return null;
        }
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
