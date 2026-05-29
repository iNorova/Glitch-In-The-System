using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// One fake "Critical Error" window. X and OKAY both close it and notify <see cref="InterruptionManager"/>.
    /// </summary>
    public sealed class ErrorPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button okayButton;
        [SerializeField] private float popInDuration = 0.22f;

        private InterruptionManager _manager;
        private CanvasGroup _canvasGroup;

        public void Initialize(InterruptionManager manager)
        {
            _manager = manager;
            PlayPopIn();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (okayButton != null)
            {
                okayButton.onClick.RemoveAllListeners();
                okayButton.onClick.AddListener(Close);
            }
        }

        private void PlayPopIn()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            StopAllCoroutines();
            StartCoroutine(PopInRoutine());
        }

        private IEnumerator PopInRoutine()
        {
            var rt = (RectTransform)transform;
            Vector3 targetScale = rt.localScale;
            rt.localScale = targetScale * 0.88f;
            _canvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < popInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popInDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rt.localScale = Vector3.Lerp(targetScale * 0.88f, targetScale, eased);
                _canvasGroup.alpha = eased;
                yield return null;
            }

            rt.localScale = targetScale;
            _canvasGroup.alpha = 1f;
        }

        private void Close()
        {
            _manager?.NotifyPopupClosed(this);
            Destroy(gameObject);
        }
    }
}
