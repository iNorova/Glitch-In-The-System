using UnityEngine;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Simple rotating loading icon (uses unscaled time so it spins during interruption intro).
    /// </summary>
    public sealed class InterruptionLoadingSpinner : MonoBehaviour
    {
        [SerializeField] private RectTransform spinnerTransform;
        [SerializeField] private float degreesPerSecond = 360f;

        private void Awake()
        {
            if (spinnerTransform == null)
                spinnerTransform = transform as RectTransform;
        }

        private void Update()
        {
            if (spinnerTransform == null)
                return;

            spinnerTransform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
