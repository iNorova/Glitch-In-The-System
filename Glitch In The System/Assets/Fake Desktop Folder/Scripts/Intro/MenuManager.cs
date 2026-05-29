using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GlitchInTheSystem.Intro
{
    /// <summary>
    /// Handles fake-desktop main menu startup, login flow, and shutdown flow.
    /// Attach to a dedicated object in FakeDesktopScene and assign references in Inspector.
    /// </summary>
    public sealed class MenuManager : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string nextSceneName = "WorkDashboard";

        [Header("Core UI")]
        [Tooltip("CanvasGroup on a fullscreen black image.")]
        [SerializeField] private CanvasGroup blackOverlay;
        [Tooltip("CanvasGroup on your login panel root (panel + buttons + title).")]
        [SerializeField] private CanvasGroup loginPanelGroup;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button shutdownButton;
        [Tooltip("Status label under/near login button, e.g. \"Authenticating...\"")]
        [SerializeField] private TMP_Text statusText;

        [Header("Startup Images")]
        [Tooltip("Image (CanvasGroup) that shows during the initial fade-in. Example: your title_screen-draft image.")]
        [SerializeField] private CanvasGroup startImageGroup;
        [Tooltip("How long the login panel fades in AFTER the black overlay is removed.")]
        [SerializeField] private float loginPanelFadeInSeconds = 0.45f;

        [Header("Welcome Image (after Login click)")]
        [Tooltip("Image (CanvasGroup) that fades in when the player clicks Login. Example: welcome_screen-draft image.")]
        [SerializeField] private CanvasGroup welcomeImageGroup;
        [Tooltip("Optional TMP text shown during welcome stage (e.g. 'Please Wait...').")]
        [SerializeField] private TMP_Text welcomeStatusText;
        [SerializeField] private string welcomeStatusLabel = "Please Wait";
        [SerializeField] private float welcomeStatusDelaySeconds = 1f;
        [Tooltip("If true, welcome image fades in while the fake authentication dots are playing.")]
        [SerializeField] private bool fadeWelcomeDuringAuth = true;
        [Tooltip("When fadeWelcomeDuringAuth is false, this is how long the welcome image stays before scene load.")]
        [SerializeField] private float welcomeHoldSecondsAfterAuth = 0.9f;
        [Tooltip("If true, hides the login panel while the welcome image is visible.")]
        [SerializeField] private bool hideLoginPanelDuringWelcome = true;

        [Header("Optional Overlay Effects")]
        [Tooltip("CanvasGroup on subtle scanline/noise overlay image.")]
        [SerializeField] private CanvasGroup crtOverlayGroup;
        [SerializeField] private bool useSubtleIdleFlicker = true;
        [SerializeField] private float idleFlickerEverySeconds = 3f;
        [SerializeField] private float idleFlickerMaxAlpha = 0.16f;

        [Header("Startup Timing")]
        [SerializeField] private float bootBlackHoldSeconds = 0.45f;
        [SerializeField] private float bootFlickerSeconds = 0.75f;
        [SerializeField] private float fadeInSeconds = 1.2f;

        [Header("Login Flow")]
        [SerializeField] private string authenticatingLabel = "Authenticating";
        [SerializeField] private float authenticationSeconds = 1.4f;
        [SerializeField] private float loadingDotStepSeconds = 0.25f;
        [Header("Button Polish")]
        [SerializeField] private float hoverScaleMultiplier = 1.05f;
        [SerializeField] private float clickScaleMultiplier = 0.94f;
        [SerializeField] private float buttonScaleSpeed = 12f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField] private AudioClip buttonHoverClip;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip loginProceedClip;
        [SerializeField] private AudioClip startupClip;
        [SerializeField] private AudioClip shutdownClip;

        [Header("Shutdown")]
        [SerializeField] private float shutdownFlashSeconds = 0.55f;
        [SerializeField] private float shutdownHoldBeforeQuit = 0.18f;

        private bool _busy;
        private Vector3 _loginBaseScale;
        private Vector3 _shutdownBaseScale;

        private void Awake()
        {
            EnsureAudioSource();

            if (loginButton != null) _loginBaseScale = loginButton.transform.localScale;
            if (shutdownButton != null) _shutdownBaseScale = shutdownButton.transform.localScale;

            if (statusText != null) statusText.text = string.Empty;

            if (blackOverlay != null)
            {
                blackOverlay.alpha = 1f;
                blackOverlay.blocksRaycasts = true;
                blackOverlay.interactable = false;
            }

            if (startImageGroup != null)
            {
                startImageGroup.alpha = 0f;
                startImageGroup.blocksRaycasts = false;
                startImageGroup.interactable = false;
            }

            if (loginPanelGroup != null)
            {
                loginPanelGroup.alpha = 0f;
                loginPanelGroup.blocksRaycasts = false;
                loginPanelGroup.interactable = false;
            }

            if (welcomeImageGroup != null)
            {
                welcomeImageGroup.alpha = 0f;
                welcomeImageGroup.blocksRaycasts = false;
                welcomeImageGroup.interactable = false;
            }

            SetWelcomeStatusVisible(false);

            if (crtOverlayGroup != null)
            {
                crtOverlayGroup.alpha = 0f;
                crtOverlayGroup.blocksRaycasts = false;
                crtOverlayGroup.interactable = false;
            }
        }

        private void Start()
        {
            StartCoroutine(StartupSequence());
            if (useSubtleIdleFlicker && crtOverlayGroup != null)
                StartCoroutine(IdleFlickerLoop());
        }

        private IEnumerator StartupSequence()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, bootBlackHoldSeconds));

            PlayClip(startupClip);

            float t = 0f;
            while (t < bootFlickerSeconds)
            {
                t += Time.unscaledDeltaTime;
                if (crtOverlayGroup != null)
                    crtOverlayGroup.alpha = Random.Range(0f, 0.38f);
                yield return null;
            }

            if (crtOverlayGroup != null)
                crtOverlayGroup.alpha = 0f;

            t = 0f;
            while (t < fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(t / fadeInSeconds);

                if (blackOverlay != null) blackOverlay.alpha = 1f - k;
                if (startImageGroup != null) startImageGroup.alpha = k;
                yield return null;
            }

            // Remove fade -> then fade in the login panel automatically.
            if (blackOverlay != null)
            {
                blackOverlay.alpha = 0f;
                blackOverlay.blocksRaycasts = false;
            }

            if (loginPanelGroup != null)
            {
                loginPanelGroup.blocksRaycasts = true;
                loginPanelGroup.interactable = true;
                loginPanelGroup.alpha = 0f;
            }

            float fadeLoginT = 0f;
            float loginFade = Mathf.Max(0.01f, loginPanelFadeInSeconds);
            while (fadeLoginT < loginFade)
            {
                fadeLoginT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(fadeLoginT / loginFade);
                if (loginPanelGroup != null) loginPanelGroup.alpha = k;
                yield return null;
            }

            if (loginPanelGroup != null) loginPanelGroup.alpha = 1f;
            if (startImageGroup != null) startImageGroup.alpha = 1f;
        }

        public void OnLoginClicked()
        {
            if (_busy) return;
            StartCoroutine(LoginFlow());
        }

        public void OnShutdownClicked()
        {
            if (_busy) return;
            StartCoroutine(ShutdownFlow());
        }

        public void OnLoginHoverEnter()
        {
            if (loginButton == null) return;
            PlayClip(buttonHoverClip);
            StopCoroutine(nameof(ScaleTo));
            StartCoroutine(ScaleTo(loginButton.transform, _loginBaseScale * hoverScaleMultiplier));
        }

        public void OnLoginHoverExit()
        {
            if (loginButton == null) return;
            StopCoroutine(nameof(ScaleTo));
            StartCoroutine(ScaleTo(loginButton.transform, _loginBaseScale));
        }

        public void OnShutdownHoverEnter()
        {
            if (shutdownButton == null) return;
            PlayClip(buttonHoverClip);
            StopCoroutine(nameof(ScaleTo));
            StartCoroutine(ScaleTo(shutdownButton.transform, _shutdownBaseScale * hoverScaleMultiplier));
        }

        public void OnShutdownHoverExit()
        {
            if (shutdownButton == null) return;
            StopCoroutine(nameof(ScaleTo));
            StartCoroutine(ScaleTo(shutdownButton.transform, _shutdownBaseScale));
        }

        private IEnumerator LoginFlow()
        {
            _busy = true;
            SetButtonsInteractable(false);

            PlayClip(buttonClickClip);
            if (loginButton != null)
                yield return ClickPulse(loginButton.transform, _loginBaseScale);

            PlayClip(loginProceedClip);

            if (welcomeImageGroup != null)
            {
                welcomeImageGroup.alpha = 0f;
                welcomeImageGroup.blocksRaycasts = false;
                welcomeImageGroup.interactable = false;
            }
            SetWelcomeStatusVisible(false);

            if (loginPanelGroup != null && hideLoginPanelDuringWelcome)
            {
                loginPanelGroup.interactable = false;
                loginPanelGroup.blocksRaycasts = false;
            }

            float elapsed = 0f;
            int dots = 0;
            float welcomeVisibleAt = -1f;
            while (elapsed < authenticationSeconds)
            {
                elapsed += loadingDotStepSeconds;
                dots = (dots + 1) % 4;
                if (statusText != null)
                    statusText.text = authenticatingLabel + new string('.', dots);

                if (welcomeImageGroup != null && fadeWelcomeDuringAuth)
                {
                    float p = authenticationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / authenticationSeconds);
                    // Ease-in for a smoother UI fade.
                    float eased = p * p * (3f - 2f * p);
                    welcomeImageGroup.alpha = eased;
                    if (eased > 0.01f)
                    {
                        if (welcomeVisibleAt < 0f)
                            welcomeVisibleAt = elapsed;

                        bool showWait = (elapsed - welcomeVisibleAt) >= Mathf.Max(0f, welcomeStatusDelaySeconds);
                        SetWelcomeStatusVisible(showWait);
                        if (showWait)
                            UpdateWelcomeStatusWithDots(dots);
                    }

                    if (loginPanelGroup != null && hideLoginPanelDuringWelcome)
                        loginPanelGroup.alpha = 1f - eased;
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, loadingDotStepSeconds));
            }

            // If welcome fade is disabled, show welcome AFTER auth timer finishes.
            if (welcomeImageGroup != null && !fadeWelcomeDuringAuth)
            {
                welcomeImageGroup.alpha = 1f;
                SetWelcomeStatusVisible(false);
                if (loginPanelGroup != null && hideLoginPanelDuringWelcome)
                    loginPanelGroup.alpha = 0f;

                float holdElapsed = 0f;
                int waitDots = 0;
                float waitStep = Mathf.Max(0.05f, loadingDotStepSeconds);
                float waitHold = Mathf.Max(0f, welcomeHoldSecondsAfterAuth);
                while (holdElapsed < waitHold)
                {
                    holdElapsed += waitStep;
                    waitDots = (waitDots + 1) % 4;
                    bool showWait = holdElapsed >= Mathf.Max(0f, welcomeStatusDelaySeconds);
                    SetWelcomeStatusVisible(showWait);
                    if (showWait)
                        UpdateWelcomeStatusWithDots(waitDots);
                    yield return new WaitForSecondsRealtime(waitStep);
                }
            }

            // No extra fade-out here: keep welcome/auth state visible, then continue.
            if (blackOverlay != null)
                blackOverlay.blocksRaycasts = true;

            if (!string.IsNullOrWhiteSpace(nextSceneName))
                SceneManager.LoadScene(nextSceneName);

            _busy = false;
        }

        private void SetWelcomeStatusVisible(bool visible)
        {
            if (welcomeStatusText == null) return;
            welcomeStatusText.gameObject.SetActive(visible);
            if (visible)
                welcomeStatusText.text = welcomeStatusLabel;
        }

        private void UpdateWelcomeStatusWithDots(int dotCount)
        {
            if (welcomeStatusText == null || !welcomeStatusText.gameObject.activeSelf) return;
            int clamped = Mathf.Clamp(dotCount, 0, 3);
            welcomeStatusText.text = welcomeStatusLabel + new string('.', clamped);
        }

        private IEnumerator ShutdownFlow()
        {
            _busy = true;
            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = "Shutting down...";

            PlayClip(buttonClickClip);
            if (shutdownButton != null)
                yield return ClickPulse(shutdownButton.transform, _shutdownBaseScale);
            PlayClip(shutdownClip);

            float t = 0f;
            while (t < shutdownFlashSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = shutdownFlashSeconds <= 0f ? 1f : Mathf.Clamp01(t / shutdownFlashSeconds);
                if (crtOverlayGroup != null) crtOverlayGroup.alpha = Mathf.Sin(k * Mathf.PI) * 0.6f;
                if (blackOverlay != null) blackOverlay.alpha = k;
                yield return null;
            }

            if (crtOverlayGroup != null) crtOverlayGroup.alpha = 0f;
            if (blackOverlay != null) blackOverlay.alpha = 1f;

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, shutdownHoldBeforeQuit));

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loginButton != null) loginButton.interactable = interactable;
            if (shutdownButton != null) shutdownButton.interactable = interactable;
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;

            EnsureAudioSource();
            audioSource.PlayOneShot(clip, sfxVolume);
        }

        private IEnumerator ClickPulse(Transform target, Vector3 baseScale)
        {
            Vector3 pressed = baseScale * clickScaleMultiplier;
            float inTime = 0.06f;
            float outTime = 0.1f;
            float t = 0f;

            while (t < inTime)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(baseScale, pressed, t / inTime);
                yield return null;
            }

            t = 0f;
            while (t < outTime)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(pressed, baseScale, t / outTime);
                yield return null;
            }

            target.localScale = baseScale;
        }

        private IEnumerator ScaleTo(Transform target, Vector3 desiredScale)
        {
            while (Vector3.Distance(target.localScale, desiredScale) > 0.0025f)
            {
                target.localScale = Vector3.Lerp(target.localScale, desiredScale, Time.unscaledDeltaTime * buttonScaleSpeed);
                yield return null;
            }

            target.localScale = desiredScale;
        }

        private IEnumerator IdleFlickerLoop()
        {
            while (true)
            {
                float wait = Mathf.Max(0.6f, idleFlickerEverySeconds + Random.Range(-0.45f, 0.45f));
                yield return new WaitForSecondsRealtime(wait);

                float peak = Random.Range(0.03f, Mathf.Max(0.04f, idleFlickerMaxAlpha));
                float t = 0f;
                const float pulseDuration = 0.11f;
                while (t < pulseDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / pulseDuration);
                    if (crtOverlayGroup != null)
                        crtOverlayGroup.alpha = (1f - Mathf.Abs(2f * k - 1f)) * peak;
                    yield return null;
                }

                if (crtOverlayGroup != null)
                    crtOverlayGroup.alpha = 0f;
            }
        }
    }
}
