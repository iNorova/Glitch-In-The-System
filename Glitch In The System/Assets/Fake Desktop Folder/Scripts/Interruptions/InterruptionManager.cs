using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using GlitchInTheSystem.GameData;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GlitchInTheSystem.Interruptions
{
    [System.Serializable]
    public sealed class MinigameBgmTrack
    {
        [Tooltip("Looping audio file for this BGM track.")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.35f;
    }

    /// <summary>
    /// Random work-session interruptions: error popups + captcha, with moderation locked until resolved.
    /// </summary>
    public sealed class InterruptionManager : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] private int minimumDayToStart = 2;
        [SerializeField] private int interruptionsPerDay = 3;
        [SerializeField] private Vector2 randomTriggerRangeSeconds = new(35f, 90f);
        [Tooltip("If true, random interruptions only run while the Content Moderator or Social Feed window is open.")]
        [SerializeField] private bool requireWorkDashboardOpen = true;
        [Tooltip("First auto-trigger wait after day 2+ begins or work opens (seconds).")]
        [SerializeField] private float firstAutoTriggerDelaySeconds = 20f;

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
        [Tooltip("Delay between each error popup appearing.")]
        [SerializeField] private float popupStaggerSeconds = 0.55f;
        [Tooltip("Pause after the last error popup before the captcha window appears.")]
        [SerializeField] private float captchaRevealDelaySeconds = 0.4f;

        [Header("UI")]
        [SerializeField] private GameObject interruptionOverlayRoot;
        [Tooltip("Center loading icon before the gray overlay appears. Run menu: Build Interruption Loading Spinner.")]
        [SerializeField] private GameObject interruptionLoadingRoot;
        [SerializeField] private MinigameManager minigameManager;
        [SerializeField] private WorkDashboardController workDashboard;
        [SerializeField] private SocialMediaFeedController socialFeed;
        [SerializeField] private Image overlayBlinkImage;

        [Header("Not responding intro")]
        [Tooltip("How long the loading icon shows after intro audio (seconds).")]
        [SerializeField] private Vector2 loadingSpinnerDurationSeconds = new(2f, 3f);
        [Tooltip("Desktop wallpaper flicker during spinner, then inverted until interruption ends.")]
        [SerializeField] private InterruptionDesktopBackground desktopBackground;

        [Header("Feedback")]
        [SerializeField] private float invalidClickBlinkDuration = 0.12f;
        [SerializeField] private Color invalidClickBlinkColor = new(1f, 0.35f, 0.35f, 0.35f);

        [Header("Audio — SFX (one-shots)")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Plays each time an error popup window appears.")]
        [SerializeField] private AudioClip popupAppearClip;
        [SerializeField] [Range(0f, 1f)] private float popupAppearVolume = 1f;
        [SerializeField] private AudioClip invalidClickClip;
        [SerializeField] private AudioClip popupCloseClip;
        [SerializeField] private AudioClip captchaSuccessClip;
        [Tooltip("Plays when captcha answer is wrong or timer runs out (one-shot, not looped).")]
        [SerializeField] private AudioClip captchaFailureClip;

        [Header("Audio — Minigame Intro")]
        [Tooltip("Plays first (desktop still normal), then loading spinner, then gray overlay.")]
        [SerializeField] private AudioClip minigameIntroClip;
        [SerializeField] [Range(0f, 1f)] private float minigameIntroVolume = 1f;
        [Tooltip("Extra pause after the intro clip ends, before popups / captcha audio.")]
        [SerializeField] private float minigameIntroPostDelaySeconds = 0f;

        [Header("Audio — Minigame BGM (2 tracks, loop together)")]
        [SerializeField] private MinigameBgmTrack bgmTrack1 = new() { volume = 0.35f };
        [SerializeField] private MinigameBgmTrack bgmTrack2 = new() { volume = 0.35f };
        [Tooltip("Fade out background music when the minigame ends (seconds).")]
        [SerializeField] private float minigameMusicFadeOutSeconds = 0.35f;

        [HideInInspector] [FormerlySerializedAs("minigameMusicSourceA")] [SerializeField] private AudioSource minigameMusicSourceA;
        [HideInInspector] [FormerlySerializedAs("minigameMusicSource")] [SerializeField] private AudioSource minigameMusicSourceLegacy;
        [HideInInspector] [FormerlySerializedAs("minigameBgmClipA")] [SerializeField] private AudioClip legacyBgmClipA;
        [HideInInspector] [FormerlySerializedAs("minigameBackgroundClip")] [SerializeField] private AudioClip legacyBgmClipOlder;
        [HideInInspector] [FormerlySerializedAs("minigameBgmVolumeA")] [SerializeField] private float legacyBgmVolumeA = 0.35f;
        [HideInInspector] [FormerlySerializedAs("minigameBackgroundVolume")] [SerializeField] private float legacyBgmVolumeOlder = 0.35f;
        [HideInInspector] [FormerlySerializedAs("minigameBgmClipB")] [SerializeField] private AudioClip legacyBgmClipB;
        [HideInInspector] [FormerlySerializedAs("minigameBgmVolumeB")] [SerializeField] private float legacyBgmVolumeB = 0.35f;
        [HideInInspector] [FormerlySerializedAs("minigameMusicSourceB")] [SerializeField] private AudioSource minigameMusicSourceB;

        [Header("Debug")]
        [SerializeField] private bool allowDebugTriggerKey = true;
        [SerializeField] private KeyCode debugTriggerKey = KeyCode.I;

        private int _interruptionsTriggeredToday;
        private int _trackedNarrativeDay = -1;
        private bool _eligibleAppWasOpen;
        private int _remainingPopups;
        private bool _interruptionActive;
        private float _nextTriggerTime;
        private Color _overlayBaseColor;
        private readonly List<Vector2> _spawnedPopupPositions = new();
        private Coroutine _sequenceRoutine;
        private float _spawnMinX;
        private float _spawnMaxX;
        private float _spawnMinY;
        private float _spawnMaxY;
        private Coroutine _musicFadeRoutine;
        private Coroutine _blinkRoutine;
        private bool _captchaMusicActive;
        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private const string MinigameBgmRootName = "MinigameBgmAudio";
        private const string BgmLayerAName = "BgmLayerA";
        private const string BgmLayerBName = "BgmLayerB";

        /// <summary>Called by <see cref="InterruptionSceneBootstrap"/> when systems are created at runtime.</summary>
        public void Configure(
            GameObject overlayRoot,
            RectTransform container,
            MinigameManager minigame,
            WorkDashboardController dashboard,
            SocialMediaFeedController feed,
            Image blinkImage)
        {
            interruptionOverlayRoot = overlayRoot;
            popupContainer = container;
            minigameManager = minigame;
            workDashboard = dashboard;
            socialFeed = feed;
            overlayBlinkImage = blinkImage;

            MigrateLegacyBgmFields();
            EnsureAudioSources();

            if (popupPrefab == null)
                popupPrefab = LoadDefaultPopupPrefab();
        }

        private void Awake()
        {
            MigrateLegacyBgmFields();
            EnsureAudioSources();
            EnsureDesktopBackground();

            if (socialFeed == null)
                socialFeed = FindFirstObjectByType<SocialMediaFeedController>();
        }

        private void MigrateLegacyBgmFields()
        {
            if (bgmTrack1.clip == null)
            {
                if (legacyBgmClipA != null)
                    bgmTrack1.clip = legacyBgmClipA;
                else if (legacyBgmClipOlder != null)
                    bgmTrack1.clip = legacyBgmClipOlder;

                if (legacyBgmVolumeA > 0f)
                    bgmTrack1.volume = legacyBgmVolumeA;
                else if (legacyBgmVolumeOlder > 0f)
                    bgmTrack1.volume = legacyBgmVolumeOlder;
            }

            if (bgmTrack2.clip == null && legacyBgmClipB != null)
            {
                bgmTrack2.clip = legacyBgmClipB;
                bgmTrack2.volume = legacyBgmVolumeB;
            }
        }

        private void EnsureAudioSources()
        {
            EnsureSfxSourceReady();

            Transform musicRoot = transform.Find(MinigameBgmRootName);
            if (musicRoot == null)
            {
                var musicGo = new GameObject(MinigameBgmRootName);
                musicGo.transform.SetParent(transform, false);
                musicRoot = musicGo.transform;
            }

            var legacySourceA = minigameMusicSourceA != null ? minigameMusicSourceA : minigameMusicSourceLegacy;
            _bgmSourceA = EnsureBgmLayerSource(musicRoot, BgmLayerAName, legacySourceA);
            _bgmSourceB = EnsureBgmLayerSource(musicRoot, BgmLayerBName, minigameMusicSourceB);

            if (_bgmSourceA == audioSource || _bgmSourceB == audioSource)
            {
                Debug.LogError(
                    "[InterruptionManager] SFX and minigame music must use different AudioSource components.",
                    this);
            }

            ConfigureMusicSource(_bgmSourceA);
            ConfigureMusicSource(_bgmSourceB);
            _bgmSourceA.priority = 0;
            _bgmSourceB.priority = 0;
            audioSource.priority = 256;
        }

        private static AudioSource EnsureBgmLayerSource(Transform musicRoot, string layerName, AudioSource assigned)
        {
            Transform layer = musicRoot.Find(layerName);
            if (layer == null)
            {
                var layerGo = new GameObject(layerName);
                layerGo.transform.SetParent(musicRoot, false);
                layer = layerGo.transform;
            }

            if (assigned != null && assigned.gameObject == layer.gameObject)
                return assigned;

            var source = layer.GetComponent<AudioSource>();
            if (source == null)
                source = layer.gameObject.AddComponent<AudioSource>();

            return source;
        }

        private void EnsureSfxSourceReady()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            ConfigureSfxSource(audioSource);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            WarnIfBgmClipMatchesSfx(bgmTrack1.clip, "1");
            WarnIfBgmClipMatchesSfx(bgmTrack2.clip, "2");
        }

        private void WarnIfBgmClipMatchesSfx(AudioClip bgmClip, string layerLabel)
        {
            if (bgmClip == null) return;
            if (bgmClip == popupAppearClip || bgmClip == captchaFailureClip)
            {
                Debug.LogWarning(
                    $"[InterruptionManager] Minigame BGM Layer {layerLabel} should be loopable BGM, " +
                    "not the same clip as popup appear / wrong captcha.",
                    this);
            }
        }
#endif

        private static void ConfigureSfxSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
        }

        private static void ConfigureMusicSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
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

            SetLoadingSpinnerVisible(false);

            if (overlayBlinkImage != null)
                _overlayBaseColor = overlayBlinkImage.color;

            SyncNarrativeDay(GetNarrativeDay());
        }

        private void Update()
        {
            if (allowDebugTriggerKey && WasDebugTriggerPressed())
            {
                StartInterruption();
                return;
            }

            if (_interruptionActive)
            {
                if (_captchaMusicActive)
                    MaintainMinigameMusic();
                return;
            }

            int day = GetNarrativeDay();
            SyncNarrativeDay(day);

            if (day < minimumDayToStart)
                return;

            bool eligibleOpen = IsInterruptionEligibleOpen();
            if (requireWorkDashboardOpen && !eligibleOpen)
            {
                _eligibleAppWasOpen = false;
                return;
            }

            if (!_eligibleAppWasOpen && eligibleOpen)
                ScheduleNextTrigger(useFirstDayDelay: true);

            _eligibleAppWasOpen = eligibleOpen;

            if (_interruptionsTriggeredToday >= interruptionsPerDay)
                return;

            if (Time.time >= _nextTriggerTime)
                StartInterruption();
        }

        /// <summary>Called when the player finishes a day and the narrative day increments.</summary>
        public void OnNarrativeDayAdvanced()
        {
            SyncNarrativeDay(GetNarrativeDay(), forceReschedule: true);
        }

        /// <summary>Called when the content moderator or social feed opens so day 2+ timers can start.</summary>
        public void OnEligibleAppOpened()
        {
            if (GetNarrativeDay() < minimumDayToStart)
                return;

            if (!IsInterruptionEligibleOpen())
                return;

            ScheduleNextTrigger(useFirstDayDelay: true);
            _eligibleAppWasOpen = true;
        }

        /// <summary>Called when the work dashboard opens so day 2+ timers can start.</summary>
        public void OnWorkDashboardOpened() => OnEligibleAppOpened();

        private static int GetNarrativeDay()
        {
            if (GameDatabase.Instance?.Config != null)
                return GameDatabase.Instance.Config.currentDay;

            if (GameManager.Instance != null)
                return GameManager.Instance.CurrentDay;

            return 1;
        }

        private bool IsInterruptionEligibleOpen() =>
            IsWorkDashboardOpen() || IsSocialFeedOpen();

        private bool IsWorkDashboardOpen()
        {
            if (workDashboard == null)
                return true;

            return workDashboard.isActiveAndEnabled && workDashboard.gameObject.activeInHierarchy;
        }

        private bool IsSocialFeedOpen()
        {
            if (socialFeed == null)
                return false;

            return socialFeed.isActiveAndEnabled && socialFeed.gameObject.activeInHierarchy;
        }

        private void SyncNarrativeDay(int day, bool forceReschedule = false)
        {
            if (!forceReschedule && _trackedNarrativeDay == day)
                return;

            _trackedNarrativeDay = day;
            _interruptionsTriggeredToday = 0;

            if (day >= minimumDayToStart && (!requireWorkDashboardOpen || IsInterruptionEligibleOpen()))
                ScheduleNextTrigger(useFirstDayDelay: true);
        }

        private void ScheduleNextTrigger(bool useFirstDayDelay = false)
        {
            float delay = useFirstDayDelay
                ? firstAutoTriggerDelaySeconds
                : Random.Range(randomTriggerRangeSeconds.x, randomTriggerRangeSeconds.y);
            _nextTriggerTime = Time.time + Mathf.Max(0f, delay);
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
                interruptionOverlayRoot.SetActive(false);

            SetLoadingSpinnerVisible(false);

            minigameManager?.HideCaptcha();
            _captchaMusicActive = false;
            StopMinigameBackground();

            if (_sequenceRoutine != null)
                StopCoroutine(_sequenceRoutine);

            _sequenceRoutine = StartCoroutine(InterruptionSequenceRoutine());
        }

        private IEnumerator InterruptionSequenceRoutine()
        {
            if (popupPrefab == null || popupContainer == null)
            {
                Debug.LogWarning("[InterruptionManager] Popup prefab or container not assigned.");
                _remainingPopups = 0;
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupContainer);
            GetPopupSpawnBounds(out _spawnMinX, out _spawnMaxX, out _spawnMinY, out _spawnMaxY);
            _spawnedPopupPositions.Clear();
            _remainingPopups = 0;

            yield return PlayMinigameIntroRoutine();
            yield return PlayLoadingSpinnerRoutine();
            LockDesktopInvertedBackground();
            ShowInterruptionOverlay();

            for (int i = 0; i < popupCount; i++)
            {
                SpawnSinglePopup(i);
                _remainingPopups++;

                if (i < popupCount - 1 && popupStaggerSeconds > 0f)
                    yield return new WaitForSecondsRealtime(popupStaggerSeconds);
            }

            if (captchaRevealDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(captchaRevealDelaySeconds);

            StartMinigameBackground();

            if (minigameManager != null)
            {
                minigameManager.StartCaptcha(
                    onSuccess: () =>
                    {
                        PlayClip(captchaSuccessClip);
                        minigameManager.MarkCompleted();
                        TryEndInterruption();
                    },
                    onFailure: OnCaptchaFailure);
            }

            _sequenceRoutine = null;
        }

        private void SpawnSinglePopup(int index)
        {
            ErrorPopup popup = Instantiate(popupPrefab, popupContainer);
            popup.Initialize(this);
            PlacePopupRandom((RectTransform)popup.transform, index, popupCount, _spawnMinX, _spawnMaxX, _spawnMinY, _spawnMaxY);
            PlayClip(popupAppearClip, popupAppearVolume);
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

            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            if (interruptionOverlayRoot != null)
                interruptionOverlayRoot.SetActive(false);

            SetLoadingSpinnerVisible(false);
            RestoreDesktopBackground();

            minigameManager?.HideCaptcha();
            _captchaMusicActive = false;
            StopMinigameBackground();

            _interruptionActive = false;
            workDashboard?.SetModerationLocked(false);
            ScheduleNextTrigger();
        }

        public void OnInvalidClick()
        {
            PlayClip(invalidClickClip);
            MaintainMinigameMusic();

            if (minigameManager != null && minigameManager.IsCaptchaRunning)
            {
                minigameManager.BlinkCaptchaPanel();
                return;
            }

            if (overlayBlinkImage != null)
            {
                if (_blinkRoutine != null)
                    StopCoroutine(_blinkRoutine);
                _blinkRoutine = StartCoroutine(BlinkOverlayRoutine());
            }
        }

        private IEnumerator BlinkOverlayRoutine()
        {
            overlayBlinkImage.color = invalidClickBlinkColor;
            yield return new WaitForSeconds(invalidClickBlinkDuration);
            overlayBlinkImage.color = _overlayBaseColor;
            _blinkRoutine = null;
        }

        private void OnCaptchaFailure()
        {
            PlayIsolatedOneShot(captchaFailureClip);
            MaintainMinigameMusic();
        }

        private IEnumerator PlayMinigameIntroRoutine()
        {
            if (minigameIntroClip == null)
                yield break;

            PlayIsolatedOneShot(minigameIntroClip, minigameIntroVolume);
            yield return new WaitForSecondsRealtime(minigameIntroClip.length);

            if (minigameIntroPostDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(minigameIntroPostDelaySeconds);
        }

        private IEnumerator PlayLoadingSpinnerRoutine()
        {
            EnsureLoadingSpinnerRoot();

            if (interruptionLoadingRoot != null)
            {
                interruptionLoadingRoot.transform.SetAsLastSibling();
                SetLoadingSpinnerVisible(true);
            }

            if (desktopBackground != null)
                yield return desktopBackground.PlaySpinnerFlickerSequence();
            else
            {
                float fallback = Random.Range(loadingSpinnerDurationSeconds.x, loadingSpinnerDurationSeconds.y);
                if (fallback > 0f)
                    yield return new WaitForSecondsRealtime(fallback);
            }

            SetLoadingSpinnerVisible(false);
        }

        private void EnsureDesktopBackground()
        {
            if (desktopBackground != null)
                return;

            var bgObject = GameObject.Find("DesktopBackground");
            if (bgObject == null)
                return;

            desktopBackground = bgObject.GetComponent<InterruptionDesktopBackground>();
            if (desktopBackground == null)
                desktopBackground = bgObject.AddComponent<InterruptionDesktopBackground>();
        }

        private void LockDesktopInvertedBackground() => desktopBackground?.LockInvertedBackground();

        private void RestoreDesktopBackground() => desktopBackground?.RestoreNormalBackground();

        private void ShowInterruptionOverlay()
        {
            if (interruptionOverlayRoot != null)
                interruptionOverlayRoot.SetActive(true);
        }

        private void SetLoadingSpinnerVisible(bool visible)
        {
            if (interruptionLoadingRoot != null)
                interruptionLoadingRoot.SetActive(visible);
        }

        private void EnsureLoadingSpinnerRoot()
        {
            if (interruptionLoadingRoot != null)
                return;

            if (interruptionOverlayRoot == null)
                return;

            Transform desktop = interruptionOverlayRoot.transform.parent;
            if (desktop == null)
                return;

            Transform existing = desktop.Find(InterruptionLoadingSpinnerFactory.RootName);
            if (existing != null)
            {
                interruptionLoadingRoot = existing.gameObject;
                return;
            }

            interruptionLoadingRoot = InterruptionLoadingSpinnerFactory.Create(desktop);
        }

        /// <summary>One-shot that never uses the BGM AudioSource (avoids voice / clip conflicts).</summary>
        private static void PlayIsolatedOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource.PlayClipAtPoint(clip, GetListenerPosition(), Mathf.Clamp01(volumeScale));
        }

        private static Vector3 GetListenerPosition()
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null)
                return listener.transform.position;

            if (Camera.main != null)
                return Camera.main.transform.position;

            return Vector3.zero;
        }

        private void MaintainMinigameMusic()
        {
            if (!_captchaMusicActive || !HasAnyBgmClip())
                return;

            if (minigameManager != null && minigameManager.IsCompleted)
                return;

            EnsureAudioSources();
            MaintainBgmLayer(_bgmSourceA, bgmTrack1.clip, bgmTrack1.volume);
            MaintainBgmLayer(_bgmSourceB, bgmTrack2.clip, bgmTrack2.volume);
        }

        private static void MaintainBgmLayer(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null)
                return;

            if (source.clip != clip)
                source.clip = clip;

            source.loop = true;
            source.priority = 0;
            source.volume = volume;

            if (!source.isPlaying)
                source.Play();
        }

        private bool HasAnyBgmClip() => bgmTrack1.clip != null || bgmTrack2.clip != null;

        private bool IsAnyBgmPlaying()
        {
            return (_bgmSourceA != null && _bgmSourceA.isPlaying) ||
                   (_bgmSourceB != null && _bgmSourceB.isPlaying);
        }

        private void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            EnsureSfxSourceReady();
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private void StartMinigameBackground()
        {
            EnsureAudioSources();

            if (!HasAnyBgmClip())
            {
                Debug.LogWarning(
                    "[InterruptionManager] No minigame BGM clips assigned. " +
                    "Assign Layer A and/or Layer B on Interruption Manager.",
                    this);
                _captchaMusicActive = false;
                return;
            }

            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }

            _captchaMusicActive = true;
            StartBgmLayer(_bgmSourceA, bgmTrack1.clip, bgmTrack1.volume);
            StartBgmLayer(_bgmSourceB, bgmTrack2.clip, bgmTrack2.volume);
        }

        private static void StartBgmLayer(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null)
                return;

            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.loop = true;
            source.priority = 0;
            source.Play();
        }

        private void StopMinigameBackground()
        {
            _captchaMusicActive = false;

            EnsureAudioSources();

            if (!IsAnyBgmPlaying())
                return;

            if (_musicFadeRoutine != null)
                StopCoroutine(_musicFadeRoutine);

            if (minigameMusicFadeOutSeconds <= 0f || !isActiveAndEnabled)
            {
                StopBgmLayer(_bgmSourceA, bgmTrack1.volume);
                StopBgmLayer(_bgmSourceB, bgmTrack2.volume);
                return;
            }

            _musicFadeRoutine = StartCoroutine(FadeOutMinigameMusicRoutine());
        }

        private static void StopBgmLayer(AudioSource source, float restoreVolume)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.volume = restoreVolume;
        }

        private IEnumerator FadeOutMinigameMusicRoutine()
        {
            float startVolumeA = _bgmSourceA != null ? _bgmSourceA.volume : 0f;
            float startVolumeB = _bgmSourceB != null ? _bgmSourceB.volume : 0f;
            float elapsed = 0f;

            while (elapsed < minigameMusicFadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / minigameMusicFadeOutSeconds);

                if (_bgmSourceA != null && _bgmSourceA.isPlaying)
                    _bgmSourceA.volume = Mathf.Lerp(startVolumeA, 0f, t);

                if (_bgmSourceB != null && _bgmSourceB.isPlaying)
                    _bgmSourceB.volume = Mathf.Lerp(startVolumeB, 0f, t);

                yield return null;
            }

            StopBgmLayer(_bgmSourceA, bgmTrack1.volume);
            StopBgmLayer(_bgmSourceB, bgmTrack2.volume);
            _musicFadeRoutine = null;
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
