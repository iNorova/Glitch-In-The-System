using System;
using UnityEngine;

namespace GlitchInTheSystem.Interruptions
{
    public sealed class MinigameManager : MonoBehaviour
    {
        [SerializeField] private CaptchaMinigame captchaMinigame;

        public bool IsCompleted { get; private set; }

        public void Configure(CaptchaMinigame captcha) => captchaMinigame = captcha;

        public void HideCaptcha()
        {
            IsCompleted = false;
            if (captchaMinigame == null) return;

            captchaMinigame.gameObject.SetActive(false);
            if (captchaMinigame.transform.parent != null)
                captchaMinigame.transform.parent.gameObject.SetActive(false);
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
            captchaMinigame.StartCaptcha(onSuccess, onFailure);
        }

        public void MarkCompleted()
        {
            IsCompleted = true;
            if (captchaMinigame != null)
                captchaMinigame.gameObject.SetActive(false);
        }
    }
}
