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

        private InterruptionManager _manager;

        public void Initialize(InterruptionManager manager)
        {
            _manager = manager;

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

        private void Close()
        {
            _manager?.NotifyPopupClosed(this);
            Destroy(gameObject);
        }
    }
}
