using UnityEngine;
using UnityEngine.EventSystems;

namespace GlitchInTheSystem.UI
{
    /// <summary>
    /// Brings the parent window to front when any child element is clicked or dragged.
    /// Add this to any visible child panel inside a DesktopAppWindow or SimpleAppWindow.
    /// Set Target to the window root RectTransform, or leave empty to auto-detect.
    /// </summary>
    public sealed class WindowFocusOnClick : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
    {
        [SerializeField] private RectTransform target;

        public void SetTarget(RectTransform t) => target = t;

        public void OnPointerDown(PointerEventData eventData) => BringToFront();
        public void OnBeginDrag(PointerEventData eventData)   => BringToFront();

        private void BringToFront()
        {
            var windowRoot = target != null ? target : transform as RectTransform;
            if (windowRoot == null) return;

            // Walk up to find the direct child of the Canvas (the app shell)
            var canvas = windowRoot.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Transform shell = windowRoot.transform;
                while (shell.parent != null && shell.parent != canvas.transform)
                    shell = shell.parent;

                // Use DesktopUiStackOrder so interruptions always stay on top
                DesktopUiStackOrder.BringAppShellForward(shell);

                // Also raise the windowRoot within its own shell if not an interruption
                bool canRaiseWindowRoot = shell != windowRoot.transform || !DesktopUiStackOrder.IsInterruptionBlocking;
                if (canRaiseWindowRoot)
                    windowRoot.transform.SetAsLastSibling();
            }
            else
            {
                windowRoot.SetAsLastSibling();
            }
        }
    }
}
