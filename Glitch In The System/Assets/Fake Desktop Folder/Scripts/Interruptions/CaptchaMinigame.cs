using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Simple type-the-code captcha with countdown timer.
    /// </summary>
    public sealed class CaptchaMinigame : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text captchaText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text timerText;

        [Header("Settings")]
        [SerializeField] private float timeLimitSeconds = 12f;
        [SerializeField] private int captchaLength = 5;

        private float _timeRemaining;
        private string _currentCaptcha;
        private bool _running;
        private Action _onSuccess;
        private Action _onFailure;

        private void Awake()
        {
            if (submitButton != null)
                submitButton.onClick.AddListener(OnSubmitClicked);
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
                inputField.text = string.Empty;
                inputField.Select();
                inputField.ActivateInputField();
            }

            _timeRemaining = timeLimitSeconds;
            RefreshTimerLabel();
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
                HandleFailure();
            }
        }

        private void HandleFailure()
        {
            _onFailure?.Invoke();
            GenerateCaptcha();
            if (inputField != null)
            {
                inputField.text = string.Empty;
                inputField.Select();
                inputField.ActivateInputField();
            }
            _timeRemaining = timeLimitSeconds;
            RefreshTimerLabel();
        }
    }
}
