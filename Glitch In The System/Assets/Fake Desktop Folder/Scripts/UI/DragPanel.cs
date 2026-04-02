using UnityEngine;
using UnityEngine.EventSystems;

namespace GlitchInTheSystem.UI
{
    /// <summary>
    /// Add to a RectTransform (e.g. title bar) to make a panel draggable like a Windows window.
    /// Assign the window root in "Target" — the panel that moves. Leave blank to move this object.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("The panel that moves when dragging. Leave blank to move this object.")]
        [SerializeField] private RectTransform target;

        [Tooltip("Optional: clamp the panel inside this rect (e.g. desktop area).")]
        [SerializeField] private RectTransform clampTo;

        private Canvas _canvas;
        private float _scaleFactor = 1f;

        private void Awake()
        {
            if (target == null) target = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null) _scaleFactor = _canvas.scaleFactor;
            if (_scaleFactor < 0.001f) _scaleFactor = 1f;
        }

        public void SetTarget(RectTransform t) => target = t;
        public void SetClamp(RectTransform c) => clampTo = c;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;

            target.anchoredPosition += eventData.delta / _scaleFactor;

            if (clampTo != null)
                ClampInside(target, clampTo);
        }

        private static void ClampInside(RectTransform rect, RectTransform bounds)
        {
            if (rect.parent != bounds.parent) return;

            Rect rectRect = rect.rect;
            Rect boundsRect = bounds.rect;
            Vector2 pivot = rect.pivot;
            Vector2 size = rectRect.size;

            float minX = bounds.anchoredPosition.x - boundsRect.width * bounds.pivot.x;
            float maxX = minX + boundsRect.width;
            float minY = bounds.anchoredPosition.y - boundsRect.height * bounds.pivot.y;
            float maxY = minY + boundsRect.height;

            float left = rect.anchoredPosition.x - size.x * pivot.x;
            float right = left + size.x;
            float bottom = rect.anchoredPosition.y - size.y * pivot.y;
            float top = bottom + size.y;

            float dx = 0f, dy = 0f;
            if (left < minX) dx = minX - left;
            if (right > maxX) dx = maxX - right;
            if (bottom < minY) dy = minY - bottom;
            if (top > maxY) dy = maxY - top;

            rect.anchoredPosition += new Vector2(dx, dy);
        }
    }
}
