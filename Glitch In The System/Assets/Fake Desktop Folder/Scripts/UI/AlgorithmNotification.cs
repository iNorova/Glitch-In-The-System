using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.UI
{
    /// <summary>
    /// CMD/terminal-style popup the Algorithm uses to "talk" to the player.
    /// </summary>
    public sealed class AlgorithmNotification : MonoBehaviour
    {
        public static AlgorithmNotification Instance { get; private set; }

        [Header("Style")]
        [SerializeField] private Color backgroundColor = new(0.05f, 0.06f, 0.08f, 0.95f);
        [SerializeField] private Color textColor = new(0.2f, 1f, 0.4f, 1f); // terminal green
        [SerializeField] private Color borderColor = new(0.2f, 0.8f, 0.3f, 0.5f);
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeDuration = 0.3f;

        private GameObject _root;
        private TMP_Text _text;
        private CanvasGroup _canvasGroup;
        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Show a CMD-style message from the Algorithm.
        /// </summary>
        public void Show(string message, float duration = -1f)
        {
            if (_root == null) BuildUI();
            if (_root == null) return;

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);

            _root.SetActive(true);
            _text.text = message;
            _text.color = textColor;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            float d = duration > 0 ? duration : displayDuration;
            _hideRoutine = StartCoroutine(HideAfter(d));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_canvasGroup != null)
            {
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    _canvasGroup.alpha = 1f - (t / fadeDuration);
                    yield return null;
                }
            }
            _root.SetActive(false);
            _hideRoutine = null;
        }

        private void BuildUI()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("NotificationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            _root = new GameObject("AlgorithmNotification_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            _root.transform.SetParent(canvas.transform, false);

            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -20);
            rt.sizeDelta = new Vector2(520, 120);

            var img = _root.GetComponent<Image>();
            img.color = backgroundColor;
            img.raycastTarget = true; // Required for DragPanel

            _canvasGroup = _root.GetComponent<CanvasGroup>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(_root.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12, 12);
            textRt.offsetMax = new Vector2(-12, -12);

            _text = textGo.GetComponent<TextMeshProUGUI>();
            _text.fontSize = 16;
            _text.color = textColor;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _text.raycastTarget = false;
            _text.text = "> System ready.";

            // Make the notification draggable (Windows-style)
            _root.AddComponent<DragPanel>();

            _root.SetActive(false);
        }
    }
}
