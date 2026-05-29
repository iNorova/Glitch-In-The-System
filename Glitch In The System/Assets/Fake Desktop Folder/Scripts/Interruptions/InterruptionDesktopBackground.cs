using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    [Serializable]
    public struct DesktopBackgroundFlickerStep
    {
        [Tooltip("Wait on the normal wallpaper before this inverted pulse.")]
        public float delayBeforeSeconds;
        [Tooltip("How long the inverted wallpaper stays visible.")]
        public float invertedHoldSeconds;
    }

    /// <summary>
    /// Pulses the inverted desktop wallpaper during the loading spinner, then locks it for the overlay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class InterruptionDesktopBackground : MonoBehaviour
    {
        [SerializeField] private Image desktopImage;
        [SerializeField] private Sprite normalBackground;
        [SerializeField] private Sprite invertedBackground;

        [Header("Inverted pulses (then interruption overlay)")]
        [SerializeField] private DesktopBackgroundFlickerStep[] flickerSteps =
        {
            new() { delayBeforeSeconds = 0f, invertedHoldSeconds = 0.4f },
            new() { delayBeforeSeconds = 0.3f, invertedHoldSeconds = 0.2f },
            new() { delayBeforeSeconds = 0.1f, invertedHoldSeconds = 0.1f },
        };

        private Sprite _runtimeNormal;

        private void Awake()
        {
            if (desktopImage == null)
                desktopImage = GetComponent<Image>();

            CacheNormalFromImage();
        }

        private void OnValidate()
        {
            if (desktopImage == null)
                desktopImage = GetComponent<Image>();

            if (normalBackground == null && desktopImage != null)
                normalBackground = desktopImage.sprite;

            if (flickerSteps == null || flickerSteps.Length == 0)
            {
                flickerSteps = new[]
                {
                    new DesktopBackgroundFlickerStep { delayBeforeSeconds = 0f, invertedHoldSeconds = 0.4f },
                    new DesktopBackgroundFlickerStep { delayBeforeSeconds = 0.3f, invertedHoldSeconds = 0.2f },
                    new DesktopBackgroundFlickerStep { delayBeforeSeconds = 0.1f, invertedHoldSeconds = 0.1f },
                };
            }
        }

        /// <summary>Runs each configured pulse, ends on inverted, then the overlay can appear.</summary>
        public IEnumerator PlaySpinnerFlickerSequence()
        {
            if (desktopImage == null || normalBackground == null || invertedBackground == null)
                yield break;

            if (flickerSteps == null || flickerSteps.Length == 0)
                yield break;

            desktopImage.sprite = normalBackground;

            int lastIndex = flickerSteps.Length - 1;
            for (int i = 0; i < flickerSteps.Length; i++)
            {
                DesktopBackgroundFlickerStep step = flickerSteps[i];

                if (step.delayBeforeSeconds > 0f)
                {
                    desktopImage.sprite = normalBackground;
                    yield return new WaitForSecondsRealtime(step.delayBeforeSeconds);
                }

                desktopImage.sprite = invertedBackground;

                if (step.invertedHoldSeconds > 0f)
                    yield return new WaitForSecondsRealtime(step.invertedHoldSeconds);

                if (i < lastIndex)
                    desktopImage.sprite = normalBackground;
            }

            desktopImage.sprite = invertedBackground;
        }

        public void StopSpinnerFlicker()
        {
        }

        public void LockInvertedBackground()
        {
            if (desktopImage != null && invertedBackground != null)
                desktopImage.sprite = invertedBackground;
        }

        public void RestoreNormalBackground()
        {
            if (desktopImage == null)
                return;

            Sprite restore = normalBackground != null ? normalBackground : _runtimeNormal;
            if (restore != null)
                desktopImage.sprite = restore;
        }

        private void CacheNormalFromImage()
        {
            if (desktopImage == null)
                return;

            if (normalBackground != null)
                _runtimeNormal = normalBackground;
            else if (desktopImage.sprite != null)
            {
                _runtimeNormal = desktopImage.sprite;
                normalBackground = _runtimeNormal;
            }
        }
    }
}
