using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lightweight compose modal — built at runtime inside EmailAppWindow.
/// No Update(). Opens/closes via Show()/Hide().
/// OnSaveDraft callback delivers the completed EmailData to EmailApp.
/// </summary>
public sealed class ComposeEmailWindow : MonoBehaviour
{
    // Serialized so they survive domain reload during play-mode iteration
    [SerializeField] private TMP_InputField toField;
    [SerializeField] private TMP_InputField subjectField;
    [SerializeField] private TMP_InputField bodyField;
    [SerializeField] private Button         saveDraftButton;
    [SerializeField] private Button         cancelButton;
    [SerializeField] private Button         closeButton;   // title-bar X — same behaviour as Cancel

    private Action<EmailData> _onSaveDraft;

    private WindowAnimator _panelAnimator;
    private CanvasGroup    _backdropCG;
    private Coroutine      _backdropFade;

    // Separate coroutine that deactivates the modal root (mo) after the close animation.
    // We cannot use AnimateClose(onComplete: ...) for this because SnapClosedVisuals()
    // inside CloseRoutine calls gameObject.SetActive(false) on the Panel GO, which
    // immediately suspends the coroutine — so onComplete never fires and mo stays active.
    // Running deactivation from a coroutine on mo itself avoids this:
    // gameObject.SetActive(false) is the last statement, so nothing after it is lost.
    private Coroutine _hideRoutine;

    private const float OpenDuration  = 0.18f; // must match WindowAnimator config in BuildComposeModal
    private const float CloseDuration = 0.12f;

    // ── Public API ────────────────────────────────────────────────────────

    public void Init(Action<EmailData> onSaveDraft)
    {
        _onSaveDraft = onSaveDraft;

        saveDraftButton?.onClick.RemoveAllListeners();
        cancelButton   ?.onClick.RemoveAllListeners();
        closeButton    ?.onClick.RemoveAllListeners();
        saveDraftButton?.onClick.AddListener(OnSaveDraftClicked);
        cancelButton   ?.onClick.AddListener(Hide);
        closeButton    ?.onClick.AddListener(Hide);

        _panelAnimator = GetComponentInChildren<WindowAnimator>(true);
        _backdropCG    = transform.Find("Backdrop")?.GetComponent<CanvasGroup>();

        // FIX (Compose Modal Backdrop bug): the "reuse existing scene instance" path in
        // EmailApp.OpenCompose() resets Panel's CanvasGroup to the closed state but never
        // touched Backdrop — confirmed live: Backdrop CanvasGroup.alpha was stuck at 1 with
        // blocksRaycasts=true even while ComposeModal/Panel were correctly closed. That stale
        // alpha meant the very first Show() snapped the backdrop instantly to full opacity
        // instead of animating in. Init() is the single wiring entrypoint for both the
        // freshly-built and reused-instance paths, so resetting Backdrop here (alongside the
        // existing Panel reset in EmailApp.cs) guarantees the closed baseline is always
        // alpha=0 / non-blocking before anything can be shown.
        if (_backdropCG != null)
        {
            _backdropCG.alpha          = 0f;
            _backdropCG.blocksRaycasts = false;
        }

        // Cancel any in-flight hide before forcing inactive
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
        gameObject.SetActive(false);
    }

    // FIX (Compose Modal Backdrop bug): ComposeModal must open and close as one unit.
    // If the parent EmailAppWindow is closed/deactivated while Compose is open or mid-
    // animation (bypassing Hide() entirely), Unity disables this GameObject directly —
    // OnDisable is the one lifecycle point guaranteed to fire on every path to hidden,
    // so it's the correct place to force Backdrop and Panel back to a consistent,
    // fully-closed state (alpha=0, non-blocking, inactive) and stop any in-flight fades.
    private void OnDisable()
    {
        if (_backdropFade != null) { StopCoroutine(_backdropFade); _backdropFade = null; }
        if (_hideRoutine  != null) { StopCoroutine(_hideRoutine);  _hideRoutine  = null; }

        if (_backdropCG != null)
        {
            _backdropCG.alpha          = 0f;
            _backdropCG.blocksRaycasts = false;
        }

        var panel = transform.Find("Panel");
        if (panel != null)
        {
            var panelCG = panel.GetComponent<CanvasGroup>();
            if (panelCG != null) panelCG.alpha = 0f;
            panel.gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        // If a hide is in flight (user re-opens during close animation), cancel it
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }

        if (toField      != null) toField.text      = string.Empty;
        if (subjectField != null) subjectField.text = string.Empty;
        if (bodyField    != null) bodyField.text    = string.Empty;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_panelAnimator != null) _panelAnimator.AnimateOpen();
        FadeBackdropTo(1f, OpenDuration);

        if (toField != null) toField.Select();
    }

    public void Hide()
    {
        if (_panelAnimator == null)
        {
            // No animator — fall back to instant close
            gameObject.SetActive(false);
            return;
        }

        // Snap backdrop to invisible immediately before animating the panel closed.
        // FadeBackdropTo() runs a coroutine on this GO; DeactivateAfterClose() then calls
        // SetActive(false), which kills ALL coroutines on this GO mid-run — leaving the
        // backdrop stuck at non-zero alpha. Backdrop sits BEHIND the panel, so an instant
        // reset is invisible to the player during the panel's close animation.
        if (_backdropCG != null)
        {
            if (_backdropFade != null) { StopCoroutine(_backdropFade); _backdropFade = null; }
            _backdropCG.alpha          = 0f;
            _backdropCG.blocksRaycasts = false;
        }
        _panelAnimator.AnimateClose();

        // Deactivate the whole modal root after the close animation finishes.
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(DeactivateAfterClose());
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private IEnumerator DeactivateAfterClose()
    {
        // Wait at least CloseDuration so WindowAnimator.CloseRoutine finishes setting
        // final visuals (alpha=0, scale reset) before we deactivate the parent.
        float elapsed = 0f;
        while (elapsed < CloseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        // One extra frame ensures SnapClosedVisuals() in CloseRoutine has run
        yield return null;

        _hideRoutine = null;
        gameObject.SetActive(false); // last statement — coroutine suspends here, nothing after it
    }

    private void FadeBackdropTo(float targetAlpha, float duration)
    {
        if (_backdropCG == null) return;
        if (_backdropFade != null) StopCoroutine(_backdropFade);
        _backdropFade = StartCoroutine(BackdropFadeRoutine(targetAlpha, duration));
    }

    private IEnumerator BackdropFadeRoutine(float targetAlpha, float duration)
    {
        float start = _backdropCG.alpha;
        _backdropCG.blocksRaycasts = true;
        if (duration <= 0f)
        {
            _backdropCG.alpha = targetAlpha;
        }
        else
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                _backdropCG.alpha = Mathf.LerpUnclamped(start, targetAlpha, Mathf.Clamp01(t));
                yield return null;
            }
            _backdropCG.alpha = targetAlpha;
        }
        if (targetAlpha <= 0f) _backdropCG.blocksRaycasts = false;
        _backdropFade = null;
    }

    // ── Handlers ──────────────────────────────────────────────────────────

    private void OnSaveDraftClicked()
    {
        var draft = new EmailData
        {
            sender    = "me",
            recipient = toField      != null ? toField.text.Trim()      : string.Empty,
            subject   = subjectField != null ? subjectField.text.Trim() : string.Empty,
            body      = bodyField    != null ? bodyField.text            : string.Empty,
            preview   = bodyField    != null && bodyField.text.Length > 0
                            ? bodyField.text.Substring(0, Mathf.Min(bodyField.text.Length, 60))
                            : string.Empty,
            timestamp = "Draft",
            isUnread  = false,
            folder    = EmailFolder.Drafts,
        };
        Hide();
        _onSaveDraft?.Invoke(draft);
    }
}
