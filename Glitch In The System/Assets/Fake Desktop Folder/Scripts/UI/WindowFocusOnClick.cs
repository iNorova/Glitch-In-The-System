using UnityEngine;
using UnityEngine.EventSystems;

namespace GlitchInTheSystem.UI
{
    /// <summary>
    /// Bring a window root to front when clicked/dragged.
    /// Add on a visible child panel and set Target to the window root.
    /// </summary>
    public sealed class WindowFocusOnClick : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
    {
        [SerializeField] private RectTransform target;

        public void SetTarget(RectTransform t) => target = t;

        public void OnPointerDown(PointerEventData eventData) => BringToFront();
        public void OnBeginDrag(PointerEventData eventData) => BringToFront();

        private void BringToFront()
        {
            var t = target != null ? target : transform as RectTransform;
            if (t != null) t.SetAsLastSibling();
        }
    }
}
