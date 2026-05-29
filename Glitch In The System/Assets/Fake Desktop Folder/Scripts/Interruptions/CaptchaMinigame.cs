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

        private float _timeRemaining;
        private string _currentCaptcha;
        private bool _running;
        private Action _onSuccess;
        private Action _onFailure;
        private Color _panelBaseColor;
        private Coroutine _panelBlinkRoutine;

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
                inputField.onSubmit.AddListener(_ => OnSubmitClicked());
        }

        private void OnDisable()
        {
            if (_panelBlinkRoutine != null)
            {
                StopCoroutine(_panelBlinkRoutine);
                _panelBlinkRoutine = null;
            }

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
    }
}
