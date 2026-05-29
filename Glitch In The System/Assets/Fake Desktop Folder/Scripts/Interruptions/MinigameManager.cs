using System;
using UnityEngine;

namespace GlitchInTheSystem.Interruptions
{
    public sealed class MinigameManager : MonoBehaviour
    {
        [SerializeField] private CaptchaMinigame captchaMinigame;

        public bool IsCompleted { get; private set; }

        public bool IsCaptchaRunning =>
            captchaMinigame != null && captchaMinigame.isActiveAndEnabled && captchaMinigame.IsRunning;

        public void Configure(CaptchaMinigame captcha) => captchaMinigame = captcha;

        public void BlinkCaptchaPanel() => captchaMinigame?.PlayOutsideClickBlink();

        public void HideCaptcha()
        {
            IsCompleted = false;
            SetCaptchaVisible(false);
        }

        public void StartCaptcha(Action onSuccess, Action onFailure)
        {
            IsCompleted = false;

            if (captchaMinigame == null)
            {
                Debug.LogWarning("[MinigameManager] CaptchaMinigame not assigned — skipping minigame step.");
                IsCompleted = true;
                onSuccess?.Invoke();
                return;
            }

            if (captchaMinigame.transform.parent != null)
                captchaMinigame.transform.parent.gameObject.SetActive(true);

            captchaMinigame.gameObject.SetActive(true);
            DesktopUiStackOrder.RaiseInterruptionStack();
            captchaMinigame.StartCaptcha(onSuccess, onFailure);
        }

        public void MarkCompleted()
        {
            IsCompleted = true;
            SetCaptchaVisible(false);
        }

        private void SetCaptchaVisible(bool visible)
        {
            if (captchaMinigame == null) return;

            if (visible && captchaMinigame.transform.parent != null)
                captchaMinigame.transform.parent.gameObject.SetActive(true);

            captchaMinigame.gameObject.SetActive(visible);

            if (!visible && captchaMinigame.transform.parent != null)
                captchaMinigame.transform.parent.gameObject.SetActive(false);
        }
    }
}
