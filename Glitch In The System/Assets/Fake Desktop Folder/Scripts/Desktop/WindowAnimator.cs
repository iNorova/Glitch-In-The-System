using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a modern-OS-style open/close animation on a UI window:
///   Open  – scale 0.85→1.0 + alpha 0→1, ~0.18 s ease-out
///   Close – scale 1.0→0.85 + alpha 1→0, ~0.13 s ease-in
///
/// Attach to the same GameObject as the window's RectTransform.
/// A CanvasGroup is added automatically if one is not already present.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class WindowAnimator : MonoBehaviour
{
    [Header("Open animation")]
    [SerializeField] private float openDuration   = 0.18f;
    [SerializeField] private float openStartScale = 0.85f;

    [Header("Close animation")]
    [SerializeField] private float closeDuration = 0.13f;
    [SerializeField] private float closeEndScale = 0.85f;

    private CanvasGroup _canvasGroup;
    private Coroutine   _current;

    /// <summary>Window is open or opening (not closing/closed).</summary>
    public bool IsLogicallyOpen { get; private set; }

    public bool IsAnimating => _current != null;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (gameObject.activeSelf && _canvasGroup.alpha > 0.5f)
            IsLogicallyOpen = true;
    }

    public void AnimateOpen()
    {
        IsLogicallyOpen = true;
        StopCurrentAnimation();
        gameObject.SetActive(true);
        if (openDuration <= 0f)
        {
            SnapOpenVisuals();
            return;
        }
        _current = StartCoroutine(OpenRoutine());
    }

    public void AnimateClose(Action onComplete = null)
    {
        if (!gameObject.activeSelf)
        {
            IsLogicallyOpen = false;
            onComplete?.Invoke();
            return;
        }
        IsLogicallyOpen = false;
        StopCurrentAnimation();
        if (closeDuration <= 0f)
        {
            SnapClosedVisuals();
            onComplete?.Invoke();
            return;
        }
        _current = StartCoroutine(CloseRoutine(onComplete));
    }

    /// <summary>Show immediately with no animation (for desktop/start-menu launchers).</summary>
    public void SnapOpen()
    {
        IsLogicallyOpen = true;
        StopCurrentAnimation();
        gameObject.SetActive(true);
        SnapOpenVisuals();
    }

    public void SnapClosed(Action onComplete = null)
    {
        IsLogicallyOpen = false;
        StopCurrentAnimation();
        SnapClosedVisuals();
        onComplete?.Invoke();
    }

    private void SnapOpenVisuals()
    {
        _canvasGroup.alpha          = 1f;
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
        transform.localScale        = Vector3.one;
    }

    private void SnapClosedVisuals()
    {
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
        transform.localScale        = Vector3.one;
        gameObject.SetActive(false);
    }

    private IEnumerator OpenRoutine()
    {
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = true;
        transform.localScale        = Vector3.one * openStartScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / openDuration;
            float e = EaseOutCubic(Mathf.Clamp01(t));
            _canvasGroup.alpha   = e;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(openStartScale, 1f, e);
            yield return null;
        }

        SnapOpenVisuals();
        _current = null;
    }

    private IEnumerator CloseRoutine(Action onComplete)
    {
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        float startAlpha = _canvasGroup.alpha;
        float startScale = transform.localScale.x;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / closeDuration;
            float e = EaseInCubic(Mathf.Clamp01(t));
            _canvasGroup.alpha   = Mathf.LerpUnclamped(startAlpha, 0f, e);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, closeEndScale, e);
            yield return null;
        }

        SnapClosedVisuals();
        _current = null;
        onComplete?.Invoke();
    }

    private void StopCurrentAnimation()
    {
        if (_current != null)
        {
            StopCoroutine(_current);
            _current = null;
        }
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t)  => t * t * t;
}
