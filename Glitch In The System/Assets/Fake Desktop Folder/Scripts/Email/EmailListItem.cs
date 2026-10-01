using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One row in the email list panel.
/// Handles hover, selection, unread dot, and click delegation.
/// Bind() wires data and the callback; no Update() needed.
/// </summary>
public sealed class EmailListItem : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image           background;
    [SerializeField] private GameObject      unreadDot;
    [SerializeField] private TextMeshProUGUI senderLabel;
    [SerializeField] private TextMeshProUGUI subjectLabel;   // Subject line — its own row, full brightness.
    [SerializeField] private TextMeshProUGUI previewLabel;    // FIX (Subject/Preview hierarchy): split back out
                                                               // into its own distinct element (own row, muted
                                                               // color) instead of Batch UI-7's single flowing
                                                               // rich-text string. Subject alone is always far
                                                               // shorter than "subject + preview" combined, so
                                                               // giving it the FULL row width on its own line
                                                               // keeps it well clear of the original clipping
                                                               // bug (needed 203px, had 105px) without needing
                                                               // to re-merge the two back into one label.
    [SerializeField] private TextMeshProUGUI timestampLabel;

    private EmailData                _data;
    private int                      _boundIndex = -1;
    private System.Action<EmailData> _onSelect;
    private bool                     _isSelected;
    private Coroutine                _bgFadeRoutine;

    // Batch UI-10: motion durations (unscaled time, linear — no curve specified in spec)
    private const float HoverFadeDuration  = 0.06f;
    private const float SelectFadeDuration = 0.08f;

    private static readonly Color BgNormal   = new Color(0.12f, 0.13f, 0.16f, 1f);
    private static readonly Color BgHover    = new Color(0.17f, 0.19f, 0.24f, 1f);
    private static readonly Color BgSelected = new Color(0.12f, 0.22f, 0.40f, 1f);
    private static readonly Color BgUnread   = new Color(0.14f, 0.16f, 0.22f, 1f);

    // Sender text contrast — primary visual element, per Batch UI-7 hierarchy.
    private static readonly Color SenderUnreadColor = new Color(0.95f, 0.95f, 0.98f, 1f); // bright, bold
    private static readonly Color SenderReadColor   = new Color(0.72f, 0.73f, 0.78f, 1f); // muted, normal weight

    public EmailData Data        => _data;
    public int       BoundIndex  => _boundIndex;

    // ── Public API ─────────────────────────────────────────────────────────

    public void Bind(EmailData data, System.Action<EmailData> onSelect, int boundIndex = -1)
    {
        _data       = data;
        _onSelect   = onSelect;
        _isSelected = false;
        _boundIndex = boundIndex;

        if (senderLabel    != null) senderLabel.text    = data.sender;
        if (timestampLabel != null) timestampLabel.text = data.timestamp;

        // FIX (Subject/Preview hierarchy): Subject and preview are now two distinct elements
        // (separate rows), each independently ellipsis-truncated. Subject gets the full row
        // width to itself, so it stays well clear of the original clipping bug even though it
        // no longer shares a line with the (much longer) combined string.
        if (subjectLabel != null)
        {
            subjectLabel.text             = data.subject ?? "";
            subjectLabel.enableAutoSizing = false;
            subjectLabel.overflowMode     = TMPro.TextOverflowModes.Ellipsis;
        }

        if (previewLabel != null)
        {
            previewLabel.text             = data.preview ?? "";
            previewLabel.enableAutoSizing = false;
            previewLabel.overflowMode     = TMPro.TextOverflowModes.Ellipsis;
        }

        // Pooled-row reuse: a fade from the PREVIOUS binding must never bleed into the
        // freshly-bound row's colors.
        if (_bgFadeRoutine != null) { StopCoroutine(_bgFadeRoutine); _bgFadeRoutine = null; }

        ApplyUnreadState(data.isUnread);
        SetSelected(false, animate: false); // fresh bind — snap, never fade
    }

    public void MarkRead()
    {
        _data.isUnread = false;
        ApplyUnreadState(false);
        if (!_isSelected)
            SetBackgroundInstant(BgNormal);
    }

    public void SetSelected(bool selected) => SetSelected(selected, animate: true);

    private void SetSelected(bool selected, bool animate)
    {
        _isSelected = selected;
        var target  = selected ? BgSelected : (_data.isUnread ? BgUnread : BgNormal);
        if (animate) FadeBackgroundTo(target, SelectFadeDuration);
        else         SetBackgroundInstant(target);
    }

    // ── Pointer events ────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        if (!_isSelected)
            FadeBackgroundTo(BgHover, HoverFadeDuration);
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!_isSelected)
            FadeBackgroundTo(_data.isUnread ? BgUnread : BgNormal, HoverFadeDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _onSelect?.Invoke(_data);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ApplyUnreadState(bool unread)
    {
        if (unreadDot != null) unreadDot.SetActive(unread);
        if (!_isSelected)
            SetBackgroundInstant(unread ? BgUnread : BgNormal);

        // Batch UI-7: sender is the primary visual element — bold + brighter when unread,
        // normal weight + slightly muted once read. Gives an obvious unread/read contrast
        // at the most important glance-point in the row.
        if (senderLabel != null)
        {
            senderLabel.fontStyle = unread ? FontStyles.Bold : FontStyles.Normal;
            senderLabel.color     = unread ? SenderUnreadColor : SenderReadColor;
        }
    }

    private void SetBackgroundInstant(Color c)
    {
        if (background != null) background.color = c;
    }

    /// <summary>Linear fade of the row background toward a target color. Replaces any
    /// in-flight fade (e.g. fast mouse-in/out) so the latest target always wins.</summary>
    private void FadeBackgroundTo(Color target, float duration)
    {
        if (background == null) return;
        if (!gameObject.activeInHierarchy) { background.color = target; return; } // can't StartCoroutine on inactive GO
        if (_bgFadeRoutine != null) StopCoroutine(_bgFadeRoutine);
        _bgFadeRoutine = StartCoroutine(BgFadeRoutine(target, duration));
    }

    private IEnumerator BgFadeRoutine(Color target, float duration)
    {
        Color start = background.color;
        if (duration <= 0f) { background.color = target; _bgFadeRoutine = null; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            background.color = Color.LerpUnclamped(start, target, Mathf.Clamp01(t));
            yield return null;
        }
        background.color = target;
        _bgFadeRoutine = null;
    }
}
