using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GlitchInTheSystem.GameData;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Random work-session interruptions: error popups + captcha, with moderation locked until resolved.
    /// </summary>
    public sealed class InterruptionManager : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] private int minimumDayToStart = 2;
        [SerializeField] private int interruptionsPerDay = 3;
        [SerializeField] private Vector2 randomTriggerRangeSeconds = new(35f, 90f);

        [Header("Popups")]
        [SerializeField] [Range(1, 8)] private int popupCount = 4;
        [SerializeField] private RectTransform popupContainer;
        [SerializeField] private ErrorPopup popupPrefab;
        [Tooltip("Keep popups away from screen edges (pixels).")]
        [SerializeField] private float popupScreenPadding = 120f;
        [Tooltip("Minimum distance between popup centers (pixels). Increase if they overlap.")]
        [SerializeField] private float popupMinSeparation = 320f;
        [Tooltip("Fallback size if container layout is not ready yet.")]
        [SerializeField] private Vector2 popupAreaFallbackSize = new(1920f, 1080f);

        [Header("UI")]
        [SerializeField] private GameObject interruptionOverlayRoot;
        [SerializeField] private MinigameManager minigameManager;
        [SerializeField] private WorkDashboardController workDashboard;
        [SerializeField] private Image overlayBlinkImage;

        [Header("Feedback")]
        [SerializeField] private float invalidClickBlinkDuration = 0.12f;
        [SerializeField] private Color invalidClickBlinkColor = new(1f, 0.35f, 0.35f, 0.35f);

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip invalidClickClip;
        [SerializeField] private AudioClip popupCloseClip;
        [SerializeField] private AudioClip captchaSuccessClip;
        [SerializeField] private AudioClip captchaFailureClip;

        [Header("Debug")]
        [SerializeField] private bool allowDebugTriggerKey = true;
        [SerializeField] private KeyCode debugTriggerKey = KeyCode.I;

        private int _interruptionsTriggeredToday;
        private int _remainingPopups;
        private bool _interruptionActive;
        private float _nextTriggerTime;
        private Color _overlayBaseColor;
        private readonly List<Vector2> _spawnedPopupPositions = new();

        /// <summary>Called by <see cref="InterruptionSceneBootstrap"/> when systems are created at runtime.</summary>
        public void Configure(
            GameObject overlayRoot,
            RectTransform container,
            MinigameManager minigame,
            WorkDashboardController dashboard,
            Image blinkImage)
        {
            interruptionOverlayRoot = overlayRoot;
            popupContainer = container;
            minigameManager = minigame;
            workDashboard = dashboard;
            overlayBlinkImage = blinkImage;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (popupPrefab == null)
                popupPrefab = LoadDefaultPopupPrefab();
        }

        public void SetPopupPrefab(ErrorPopup prefab) => popupPrefab = prefab;

        private static ErrorPopup LoadDefaultPopupPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ErrorPopup>(
                "Assets/Error/CriticalErrorUi 1.prefab");
#else
            return null;
#endif
        }

        private void Start()
        {
            if (interruptionOverlayRoot != null)
                interruptionOverlayRoot.SetActive(false);

            if (overlayBlinkImage != null)
                _overlayBaseColor = overlayBlinkImage.color;

            ScheduleNextTrigger();
        }

        private void Update()
        {
            if (allowDebugTriggerKey && WasDebugTriggerPressed())
            {
                StartInterruption();
                return;
            }

            if (_interruptionActive) return;

            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            if (day < minimumDayToStart) return;
            if (_interruptionsTriggeredToday >= interruptionsPerDay) return;

            if (Time.time >= _nextTriggerTime)
                StartInterruption();
        }

        private void ScheduleNextTrigger()
        {
            float delay = Random.Range(randomTriggerRangeSeconds.x, randomTriggerRangeSeconds.y);
            _nextTriggerTime = Time.time + delay;
        }

        public void StartInterruption()
        {
            if (_interruptionActive) return;

            if (popupPrefab == null)
                popupPrefab = LoadDefaultPopupPrefab();

            if (popupPrefab == null)
            {
                Debug.LogError("[InterruptionManager] Popup Prefab missing. Put CriticalErrorUi prefab in Assets/Error/.");
                return;
            }

            _interruptionActive = true;
            _interruptionsTriggeredToday++;

            workDashboard?.SetModerationLocked(true);

            if (interruptionOverlayRoot != null)
                interruptionOverlayRoot.SetActive(true);

            SpawnPopups();

            if (minigameManager != null)
            {
                minigameManager.StartCaptcha(
                    onSuccess: () =>
                    {
                        PlayClip(captchaSuccessClip);
                        minigameManager.MarkCompleted();
                        TryEndInterruption();
                    },
                    onFailure: () => PlayClip(captchaFailureClip));
            }
        }

        private void SpawnPopups()
        {
            if (popupPrefab == null || popupContainer == null)
            {
                Debug.LogWarning("[InterruptionManager] Popup prefab or container not assigned.");
                _remainingPopups = 0;
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupContainer);

            GetPopupSpawnBounds(out float minX, out float maxX, out float minY, out float maxY);
            _spawnedPopupPositions.Clear();

            _remainingPopups = popupCount;
            for (int i = 0; i < popupCount; i++)
            {
                ErrorPopup popup = Instantiate(popupPrefab, popupContainer);
                popup.Initialize(this);
                PlacePopupRandom((RectTransform)popup.transform, i, popupCount, minX, maxX, minY, maxY);
            }
        }

        private void GetPopupSpawnBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            EnsurePopupContainerFillsOverlay();

            float width = popupContainer.rect.width;
            float height = popupContainer.rect.height;

            // Tiny container (e.g. 100x100 center box) forces all popups into one cluster.
            if (width < 400f || height < 400f)
            {
                RectTransform overlay = interruptionOverlayRoot != null
                    ? interruptionOverlayRoot.GetComponent<RectTransform>()
                    : popupContainer.parent as RectTransform;

                if (overlay != null)
                {
                    width = overlay.rect.width;
                    height = overlay.rect.height;
                }

                if (width < 400f || height < 400f)
                {
                    width = popupAreaFallbackSize.x;
                    height = popupAreaFallbackSize.y;
                }
            }

            float halfW = width * 0.5f - popupScreenPadding;
            float halfH = height * 0.5f - popupScreenPadding;
            minX = -halfW;
            maxX = halfW;
            minY = -halfH;
            maxY = halfH;
        }

        /// <summary>Runtime safety: PopupContainer must be full-screen stretch, not a small centered box.</summary>
        private void EnsurePopupContainerFillsOverlay()
        {
            if (popupContainer == null) return;

            const float minFullScreen = 400f;
            if (popupContainer.rect.width >= minFullScreen && popupContainer.rect.height >= minFullScreen)
                return;

            popupContainer.anchorMin = Vector2.zero;
            popupContainer.anchorMax = Vector2.one;
            popupContainer.pivot = new Vector2(0.5f, 0.5f);
            popupContainer.anchoredPosition = Vector2.zero;
            popupContainer.sizeDelta = Vector2.zero;
            popupContainer.localScale = Vector3.one;

            LayoutRebuilder.ForceRebuildLayoutImmediate(popupContainer);
        }

        private void PlacePopupRandom(RectTransform rt, int index, int total, float minX, float maxX, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            Vector2 pos = PickSeparatedPosition(index, total, minX, maxX, minY, maxY);
            rt.anchoredPosition = pos;
            _spawnedPopupPositions.Add(pos);
        }

        private Vector2 PickSeparatedPosition(int index, int total, float minX, float maxX, float minY, float maxY)
        {
            float minDist = popupMinSeparation;
            float minDistSq = minDist * minDist;

            for (int attempt = 0; attempt < 48; attempt++)
            {
                float x = Random.Range(minX, maxX);
                float y = Random.Range(minY, maxY);
                var candidate = new Vector2(x, y);

                if (IsFarEnough(candidate, minDistSq))
                    return candidate;
            }

            // Even spread on a ring + jitter when random placement fails (many popups / tight area).
            float t = total <= 1 ? 0.5f : index / (float)total;
            float angle = t * Mathf.PI * 2f + Random.Range(-0.35f, 0.35f);
            float radius = Mathf.Min(maxX - minX, maxY - minY) * Random.Range(0.45f, 0.95f) * 0.5f;
            var ringPos = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            return ringPos;
        }

        private bool IsFarEnough(Vector2 candidate, float minDistSq)
        {
            for (int i = 0; i < _spawnedPopupPositions.Count; i++)
            {
                if ((_spawnedPopupPositions[i] - candidate).sqrMagnitude < minDistSq)
                    return false;
            }

            return true;
        }

        public void NotifyPopupClosed(ErrorPopup popup)
        {
            PlayClip(popupCloseClip);
            _remainingPopups = Mathf.Max(0, _remainingPopups - 1);
            TryEndInterruption();
        }

        private void TryEndInterruption()
        {
            if (minigameManager == null || !minigameManager.IsCompleted) return;
            if (_remainingPopups > 0) return;

            if (interruptionOverlayRoot != null)
                interruptionOverlayRoot.SetActive(false);

            _interruptionActive = false;
            workDashboard?.SetModerationLocked(false);
            ScheduleNextTrigger();
        }

        public void OnInvalidClick()
        {
            PlayClip(invalidClickClip);
            if (overlayBlinkImage != null)
            {
                StopAllCoroutines();
                StartCoroutine(BlinkOverlayRoutine());
            }
        }

        private IEnumerator BlinkOverlayRoutine()
        {
            overlayBlinkImage.color = invalidClickBlinkColor;
            yield return new WaitForSeconds(invalidClickBlinkDuration);
            overlayBlinkImage.color = _overlayBaseColor;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip);
        }

        private bool WasDebugTriggerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;

            if (debugTriggerKey >= KeyCode.A && debugTriggerKey <= KeyCode.Z)
            {
                var k = (Key)((int)Key.A + (debugTriggerKey - KeyCode.A));
                return kb[k].wasPressedThisFrame;
            }

            if (debugTriggerKey == KeyCode.I)
                return kb.iKey.wasPressedThisFrame;

            return false;
#else
            return Input.GetKeyDown(debugTriggerKey);
#endif
        }
    }
}
