using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Swaps the fake desktop wallpaper during the interruption intro (spinner flicker, then inverted lock).
    /// Lives on <c>DesktopBackground</c> — separate from the gray <c>InterruptionOverlay</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class InterruptionDesktopBackground : MonoBehaviour
    {
        [SerializeField] private Image desktopImage;
        [SerializeField] private Sprite normalBackground;
        [SerializeField] private Sprite invertedBackground;
        [Tooltip("How fast the wallpaper alternates during the loading spinner.")]
        [SerializeField] private float swapIntervalSeconds = 0.35f;

        private Coroutine _swapRoutine;
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
        }

        public void BeginSpinnerFlicker()
        {
            if (desktopImage == null || normalBackground == null || invertedBackground == null)
                return;

            StopSpinnerFlicker();
            _swapRoutine = StartCoroutine(SpinnerFlickerRoutine());
        }

        public void StopSpinnerFlicker()
        {
            if (_swapRoutine != null)
            {
                StopCoroutine(_swapRoutine);
                _swapRoutine = null;
            }
        }

        /// <summary>Stop flicker and keep the creepy wallpaper for the rest of the interruption.</summary>
        public void LockInvertedBackground()
        {
            StopSpinnerFlicker();

            if (desktopImage != null && invertedBackground != null)
                desktopImage.sprite = invertedBackground;
        }

        public void RestoreNormalBackground()
        {
            StopSpinnerFlicker();

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

        private IEnumerator SpinnerFlickerRoutine()
        {
            float interval = Mathf.Max(0.08f, swapIntervalSeconds);
            bool showInverted = false;

            while (true)
            {
                desktopImage.sprite = showInverted ? invertedBackground : normalBackground;
                showInverted = !showInverted;
                yield return new WaitForSecondsRealtime(interval);
            }
        }
    }
}
