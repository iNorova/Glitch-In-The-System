using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Simple type-the-code captcha with countdown timer.
    /// </summary>
    public sealed class CaptchaMinigame : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI")]
        [SerializeField] private TMP_Text captchaText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Image panelImage;

        [Header("Settings")]
        [SerializeField] private float timeLimitSeconds = 12f;
        [SerializeField] private int captchaLength = 5;

        [Header("Outside-click feedback")]
        [SerializeField] private Color outsideClickBlinkColor = new(1f, 0.35f, 0.35f, 0.92f);
        [SerializeField] private float outsideClickBlinkDuration = 0.1f;
        [SerializeField] [Range(1, 4)] private int outsideClickBlinkCount = 2;

        [Header("Typing stress glitch")]
        [SerializeField] private Color typingGlitchColor = new(0.92f, 0.18f, 0.22f, 0.86f);
        [SerializeField] private Vector2 glitchIntervalRangeSeconds = new(0.25f, 0.85f);
        [SerializeField] private float glitchPulseDuration = 0.08f;
        [SerializeField] private float glitchJitterPixels = 6f;

        private float _timeRemaining;
        private string _currentCaptcha;
        private bool _running;
        private Action _onSuccess;
        private Action _onFailure;
        private Color _panelBaseColor;
        private Coroutine _panelBlinkRoutine;
        private Coroutine _typingGlitchRoutine;
        private Coroutine _glitchPulseRoutine;
        private RectTransform _captchaRect;
        private RectTransform _inputRect;
        private RectTransform _timerRect;
        private Vector2 _captchaBasePos;
        private Vector2 _inputBasePos;
        private Vector2 _timerBasePos;

        public bool IsRunning => _running;

        private void Awake()
        {
            if (panelImage == null)
                panelImage = GetComponent<Image>();

            if (panelImage != null)
                _panelBaseColor = panelImage.color;

            if (submitButton != null)
                submitButton.onClick.AddListener(OnSubmitClicked);

            if (inputField != null)
            {
                inputField.onSubmit.AddListener(_ => OnSubmitClicked());
                _inputRect = inputField.GetComponent<RectTransform>();
            }

            _captchaRect = captchaText != null ? captchaText.rectTransform : null;
            _timerRect = timerText != null ? timerText.rectTransform : null;
            CacheGlitchBasePositions();
        }

        private void OnDisable()
        {
            if (_panelBlinkRoutine != null)
            {
                StopCoroutine(_panelBlinkRoutine);
                _panelBlinkRoutine = null;
            }

            if (_typingGlitchRoutine != null)
            {
                StopCoroutine(_typingGlitchRoutine);
                _typingGlitchRoutine = null;
            }

            if (_glitchPulseRoutine != null)
            {
                StopCoroutine(_glitchPulseRoutine);
                _glitchPulseRoutine = null;
            }

            RestoreGlitchTargets();

            if (panelImage != null)
                panelImage.color = _panelBaseColor;
        }

        /// <summary>Clicks on the panel backdrop (not captcha controls) also call this via <see cref="IPointerClickHandler"/>.</summary>
        public void PlayOutsideClickBlink()
        {
            if (!_running || panelImage == null)
                return;

            if (_panelBlinkRoutine != null)
                StopCoroutine(_panelBlinkRoutine);

            _panelBlinkRoutine = StartCoroutine(PanelBlinkRoutine());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_running || eventData == null)
                return;

            // Only the panel backdrop — interactive children consume their own clicks.
            var hit = eventData.pointerPressRaycast.gameObject;
            if (hit == gameObject)
                PlayOutsideClickBlink();
        }

        private IEnumerator PanelBlinkRoutine()
        {
            for (int i = 0; i < outsideClickBlinkCount; i++)
            {
                panelImage.color = outsideClickBlinkColor;
                yield return new WaitForSecondsRealtime(outsideClickBlinkDuration);
                panelImage.color = _panelBaseColor;
                yield return new WaitForSecondsRealtime(outsideClickBlinkDuration * 0.5f);
            }

            _panelBlinkRoutine = null;
        }

        private void OnValidate()
        {
            if (captchaText == null || inputField == null || submitButton == null || timerText == null)
                Debug.LogWarning(
                    "[CaptchaMinigame] UI references missing on " + name +
                    ". Run menu: Glitch In The System → UI → Build Captcha Minigame Panel",
                    this);
        }

        public void StartCaptcha(Action onSuccess, Action onFailure)
        {
            _onSuccess = onSuccess;
            _onFailure = onFailure;
            _running = true;

            GenerateCaptcha();
            if (inputField != null)
            {
                inputField.lineType = TMP_InputField.LineType.SingleLine;
                inputField.text = string.Empty;
                inputField.Select();
                inputField.ActivateInputField();
            }

            _timeRemaining = timeLimitSeconds;
            RefreshTimerLabel();

            if (panelImage != null)
                _panelBaseColor = panelImage.color;

            CacheGlitchBasePositions();
            if (_typingGlitchRoutine != null)
                StopCoroutine(_typingGlitchRoutine);
            _typingGlitchRoutine = StartCoroutine(TypingStressGlitchLoop());
        }

        private void Update()
        {
            if (!_running) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                RefreshTimerLabel();
                HandleFailure();
                return;
            }

            RefreshTimerLabel();
        }

        private void RefreshTimerLabel()
        {
            if (timerText != null)
                timerText.text = Mathf.CeilToInt(_timeRemaining).ToString();
        }

        private void GenerateCaptcha()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new StringBuilder(captchaLength);
            for (int i = 0; i < captchaLength; i++)
                sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);

            _currentCaptcha = sb.ToString();
            if (captchaText != null)
                captchaText.text = _currentCaptcha;
        }

        private void OnSubmitClicked()
        {
            if (!_running || inputField == null) return;

            if (string.Equals(inputField.text.Trim(), _currentCaptcha, StringComparison.OrdinalIgnoreCase))
            {
                _running = false;
                _onSuccess?.Invoke();
            }
            else
            {
                PlayOutsideClickBlink();
                TriggerStressGlitch(1.8f);
                HandleFailure();
            }
        }

        private void HandleFailure()
        {
            _onFailure?.Invoke();
            GenerateCaptcha();
            if (inputField != null)
            {
                inputField.lineType = TMP_InputField.LineType.SingleLine;
                inputField.text = string.Empty;
                inputField.Select();
                inputField.ActivateInputField();
            }
            _timeRemaining = timeLimitSeconds;
            RefreshTimerLabel();
        }

        private void CacheGlitchBasePositions()
        {
            if (_captchaRect != null)
                _captchaBasePos = _captchaRect.anchoredPosition;
            if (_inputRect != null)
                _inputBasePos = _inputRect.anchoredPosition;
            if (_timerRect != null)
                _timerBasePos = _timerRect.anchoredPosition;
        }

        private void RestoreGlitchTargets()
        {
            if (_captchaRect != null)
                _captchaRect.anchoredPosition = _captchaBasePos;
            if (_inputRect != null)
                _inputRect.anchoredPosition = _inputBasePos;
            if (_timerRect != null)
                _timerRect.anchoredPosition = _timerBasePos;
        }

        private IEnumerator TypingStressGlitchLoop()
        {
            while (_running)
            {
                float wait = UnityEngine.Random.Range(glitchIntervalRangeSeconds.x, glitchIntervalRangeSeconds.y);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, wait));

                if (!_running)
                    yield break;

                TriggerStressGlitch(1f);
            }
        }

        private void TriggerStressGlitch(float strength)
        {
            if (_glitchPulseRoutine != null)
                StopCoroutine(_glitchPulseRoutine);
            _glitchPulseRoutine = StartCoroutine(StressGlitchPulseRoutine(Mathf.Max(0.3f, strength)));
        }

        private IEnumerator StressGlitchPulseRoutine(float strength)
        {
            float duration = Mathf.Max(0.03f, glitchPulseDuration * strength);
            float jitter = Mathf.Max(0f, glitchJitterPixels) * strength;
            float elapsed = 0f;
            bool tintPanel = panelImage != null && _panelBlinkRoutine == null;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                float envelope = 1f - Mathf.Abs(2f * t - 1f);

                Vector2 offset = UnityEngine.Random.insideUnitCircle * jitter * envelope;
                if (_captchaRect != null)
                    _captchaRect.anchoredPosition = _captchaBasePos + offset;
                if (_inputRect != null)
                    _inputRect.anchoredPosition = _inputBasePos + (Vector2)(UnityEngine.Random.insideUnitCircle * jitter * 0.75f * envelope);
                if (_timerRect != null)
                    _timerRect.anchoredPosition = _timerBasePos + (Vector2)(UnityEngine.Random.insideUnitCircle * jitter * 0.45f * envelope);

                if (tintPanel)
                    panelImage.color = Color.Lerp(_panelBaseColor, typingGlitchColor, envelope * 0.95f);

                yield return null;
            }

            RestoreGlitchTargets();
            if (panelImage != null && _panelBlinkRoutine == null)
                panelImage.color = _panelBaseColor;

            _glitchPulseRoutine = null;
        }
    }
}
