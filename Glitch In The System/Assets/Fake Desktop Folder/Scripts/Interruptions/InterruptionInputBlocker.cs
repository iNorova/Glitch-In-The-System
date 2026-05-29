using UnityEngine;
using UnityEngine.EventSystems;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Full-screen click catcher behind popups/minigame. Clicks that reach this are "invalid".
    /// </summary>
    public sealed class InterruptionInputBlocker : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private InterruptionManager interruptionManager;

        public void SetManager(InterruptionManager manager) => interruptionManager = manager;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interruptionManager != null)
                interruptionManager.OnInvalidClick();
        }
    }
}
