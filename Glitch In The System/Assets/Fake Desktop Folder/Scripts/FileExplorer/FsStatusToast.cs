using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Batch 9 — Queued Windows-style toast notifications for File Explorer.
/// Batch 9.1 — Two-line layout: title + subtitle.
///
/// Message format:
///   "Single line message"           → title only  (height = 34px)
///   "Title — subtitle"             → two-line     (height = 50px)
///
/// Split on " — " (em-dash with spaces). Everything before = title, after = subtitle.
/// All queue / cooldown / fade logic UNCHANGED.
/// </summary>
public sealed class FsStatusToast : MonoBehaviour
{
    // ── Static access ─────────────────────────────────────────────────────
    private static FsStatusToast _instance;

    public static void ShowGlobal(string message, float duration = 2.5f)
    {
        if (_instance != null) _instance.Show(message, duration);
    }

    // ── Queue config (UNCHANGED) ──────────────────────────────────────────
    private const float CooldownBetweenMessages = 0.4f;
    private const float DefaultDuration         = 2.5f;
    private const float FadeTime                = 0.30f;
    private const int   MaxQueue                = 6;

    private const string Separator = " \u2014 "; // " — "

    // ── Animation config ──────────────────────────────────────────────────
    private const float FadeInDuration  = 0.20f;  // show: alpha + slide + scale
    private const float FadeOutDuration = 0.28f;  // hide: alpha + subtle drift down
    private const float SlideDistance   = 8f;     // px upward travel on show
    private const float DriftDistance   = 4f;     // px downward drift on hide
    private const float ScaleStart      = 0.97f;  // scale at start of show
    private const float RestY           = 14f;    // anchoredPosition.y at rest

    // ── State (UNCHANGED) ─────────────────────────────────────────────────
    private readonly Queue<(string msg, float dur)> _queue = new();
    private bool      _showing;
    private Coroutine _routine;

    // ── UI ────────────────────────────────────────────────────────────────
    private GameObject      _toast;
    private RectTransform   _toastRT;
    private TextMeshProUGUI _titleLabel;
    private TextMeshProUGUI _subtitleLabel;
    private CanvasGroup     _cg;
    private Image           _bg;

    // Heights
    private const float HeightSingle = 36f;
    private const float HeightDouble = 52f;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private void Awake()     { _instance = this; }
    private void OnDestroy() { if (_instance == this) _instance = null; }

    // ── Public API (UNCHANGED signature) ─────────────────────────────────

    public void Show(string message, float duration = DefaultDuration)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (_queue.Count >= MaxQueue) return;

        // Duplicate guard — check against current title
        if (_showing && _titleLabel != null)
        {
            ParseMessage(message, out string t, out _);
            if (_titleLabel.text == t) return;
        }

        _queue.Enqueue((message, duration));
        if (!_showing) StartNext();
    }

    // ── Internal (UNCHANGED flow) ─────────────────────────────────────────

    private void StartNext()
    {
        if (_queue.Count == 0) { _showing = false; return; }

        _showing = true;
        var (msg, dur) = _queue.Dequeue();

        EnsureBuilt();
        ApplyMessage(msg);
        // Set animation start state before activating — prevents 1-frame flash at rest position
        _cg.alpha = 0f;
        _toastRT.anchoredPosition = new Vector2(0f, RestY - SlideDistance); // start 8px below rest
        _toastRT.localScale       = new Vector3(ScaleStart, ScaleStart, 1f);
        _toast.SetActive(true);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(dur));
    }

    private IEnumerator ShowRoutine(float hold)
    {
        // ── Show: slide up + fade in + scale to 1.0 ─────────────────────
        // Ease-out curve: fast start, settles smoothly at rest position.
        float t = 0f;
        while (t < FadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / FadeInDuration;              // 0→1 linear
            float e = 1f - (1f - p) * (1f - p);       // ease-out quad
            _cg.alpha               = e;
            _toastRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(RestY - SlideDistance, RestY, e));
            _toastRT.localScale       = new Vector3(Mathf.Lerp(ScaleStart, 1f, e),
                                                    Mathf.Lerp(ScaleStart, 1f, e), 1f);
            yield return null;
        }
        // Snap to exact rest state — eliminates float drift
        _cg.alpha               = 1f;
        _toastRT.anchoredPosition = new Vector2(0f, RestY);
        _toastRT.localScale       = Vector3.one;

        yield return new WaitForSecondsRealtime(hold);

        // ── Hide: fade out + subtle downward drift ───────────────────────
        // Linear fade — keeps the feel calm and desktop-like.
        t = 0f;
        while (t < FadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / FadeOutDuration);
            _cg.alpha               = 1f - p;
            _toastRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(RestY, RestY - DriftDistance, p));
            yield return null;
        }

        // Reset to rest state so next Show() starts clean
        _cg.alpha               = 0f;
        _toastRT.anchoredPosition = new Vector2(0f, RestY);
        _toastRT.localScale       = Vector3.one;
        _toast.SetActive(false);
        _routine = null;

        if (_queue.Count > 0)
        {
            yield return new WaitForSecondsRealtime(CooldownBetweenMessages);
            StartNext();
        }
        else
        {
            _showing = false;
        }
    }

    // ── Message parsing & display ─────────────────────────────────────────

    private static void ParseMessage(string message, out string title, out string subtitle)
    {
        int sepIdx = message.IndexOf(Separator, System.StringComparison.Ordinal);
        if (sepIdx >= 0)
        {
            title    = message.Substring(0, sepIdx).Trim();
            subtitle = message.Substring(sepIdx + Separator.Length).Trim();
        }
        else
        {
            title    = message;
            subtitle = null;
        }
    }

    private void ApplyMessage(string message)
    {
        ParseMessage(message, out string title, out string subtitle);

        bool hasSub = !string.IsNullOrEmpty(subtitle);

        _titleLabel.text = title;

        if (hasSub)
        {
            _subtitleLabel.text = subtitle;
            _subtitleLabel.gameObject.SetActive(true);
            _toastRT.sizeDelta = new Vector2(_toastRT.sizeDelta.x, HeightDouble);
        }
        else
        {
            _subtitleLabel.gameObject.SetActive(false);
            _toastRT.sizeDelta = new Vector2(_toastRT.sizeDelta.x, HeightSingle);
        }
    }

    // ── UI construction ───────────────────────────────────────────────────

    private void EnsureBuilt()
    {
        if (_toast != null) return;

        // ── Toast root ────────────────────────────────────────────────────
        _toast = new GameObject("FsToast",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup));
        _toast.transform.SetParent(transform, false);
        _toast.transform.SetAsLastSibling();

        _toastRT            = _toast.GetComponent<RectTransform>();
        _toastRT.anchorMin  = new Vector2(0.5f, 0f);
        _toastRT.anchorMax  = new Vector2(0.5f, 0f);
        _toastRT.pivot      = new Vector2(0.5f, 0f);
        _toastRT.sizeDelta  = new Vector2(300f, HeightSingle);
        _toastRT.anchoredPosition = new Vector2(0f, 14f);

        _bg       = _toast.GetComponent<Image>();
        _bg.color = new Color(0.08f, 0.09f, 0.11f, 0.78f); // dark glass

        _cg                = _toast.GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
        _cg.interactable   = false;

        // ── Shadow (behind everything, slightly larger than toast) ────────
        // Child of _toast → inherits CanvasGroup alpha automatically.
        {
            var shadow = new GameObject("Shadow",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadow.transform.SetParent(_toast.transform, false);
            shadow.transform.SetAsFirstSibling(); // render behind BG + content
            var sRT = shadow.GetComponent<RectTransform>();
            sRT.anchorMin = Vector2.zero;
            sRT.anchorMax = Vector2.one;
            sRT.offsetMin = new Vector2(-2f, -3f);
            sRT.offsetMax = new Vector2( 2f,  2f);
            var sImg = shadow.GetComponent<Image>();
            sImg.color         = new Color(0f, 0f, 0f, 0.12f); // subtle desktop shadow
            sImg.raycastTarget = false;
        }

        // ── Border (1px inset white rim, near-invisible) ──────────────────
        // Uses an Image as the full-size frame layer with very low opacity.
        {
            var border = new GameObject("Border",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            border.transform.SetParent(_toast.transform, false);
            // Insert after shadow, before Content — sibling [1]
            border.transform.SetSiblingIndex(1);
            var bRT = border.GetComponent<RectTransform>();
            bRT.anchorMin = Vector2.zero;
            bRT.anchorMax = Vector2.one;
            bRT.offsetMin = Vector2.zero;
            bRT.offsetMax = Vector2.zero;
            var bImg = border.GetComponent<Image>();
            bImg.color         = new Color(1f, 1f, 1f, 0.025f); // almost invisible separation
            bImg.raycastTarget = false;
        }

        // ── VLG container ─────────────────────────────────────────────────
        var container = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(_toast.transform, false);

        var cRT = container.GetComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        // Full-height container — VLG padding drives vertical centering.
        // offsetMin.y/offsetMax.y=0 means VLG sees the full toast height.
        // Top+bottom padding of 9px centers the label block for both single and double height.
        cRT.offsetMin = new Vector2(14f, 0f); cRT.offsetMax = new Vector2(-14f, 0f);

        var vlg             = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing         = 1f;
        vlg.childAlignment  = TextAnchor.MiddleLeft;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;  // FIX: must be true so VLG respects LE.preferredHeight and sizes label correctly
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 9, 9); // equal top+bottom → labels visually centered

        // ── Title label ───────────────────────────────────────────────────
        _titleLabel = BuildLabel(container.transform,
            fontSize: 12.5f,
            color: new Color(0.92f, 0.90f, 0.87f, 1f),
            bold: true,
            height: 18f);

        // ── Subtitle label ────────────────────────────────────────────────
        _subtitleLabel = BuildLabel(container.transform,
            fontSize: 10.5f,
            color: new Color(0.62f, 0.60f, 0.57f, 1f),
            bold: false,
            height: 14f);

        _subtitleLabel.gameObject.SetActive(false);
        _toast.SetActive(false);
    }

    private static TextMeshProUGUI BuildLabel(Transform parent,
        float fontSize, Color color, bool bold, float height)
    {
        var go = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height; // clamp so VLG (childControlHeight=true) uses our height

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize      = fontSize;
        tmp.color         = color;
        tmp.fontStyle     = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.richText      = true;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
        tmp.textWrappingMode  = TMPro.TextWrappingModes.NoWrap;
        return tmp;
    }
}
