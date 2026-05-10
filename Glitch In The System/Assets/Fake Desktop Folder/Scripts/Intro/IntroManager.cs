using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Intro
{
    /// <summary>
    /// Lightweight onboarding flow:
    /// Boot text → Company welcome → 5 tutorial posts (Approve/Remove/Flag + gray area) → DAY 1 card → normal session.
    /// Designed to be UI-driven and under ~2 minutes.
    /// </summary>
    public sealed class IntroManager : MonoBehaviour
    {
        public const string PlayerPrefsIntroSeen = "GITMS_IntroSeen";

        [Header("Skip")]
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private KeyCode skipKey = KeyCode.Escape;

        [Header("Boot UI")]
        [SerializeField] private GameObject bootPanel;
        [SerializeField] private TMP_Text bootText;
        [SerializeField] private float bootLineDelay = 0.18f;
        [SerializeField] private float bootEndHold = 0.35f;

        [Header("Welcome UI")]
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private TMP_Text welcomeBodyText;
        [SerializeField] private Button welcomeContinueButton;

        [Header("Tutorial UI")]
        [SerializeField] private GameObject tutorialHintPanel;
        [SerializeField] private TMP_Text tutorialHintText;

        [Header("Day 1 UI")]
        [SerializeField] private GameObject dayCardPanel;
        [SerializeField] private TMP_Text dayCardText;
        [SerializeField] private float dayCardHoldSeconds = 2f;
        [SerializeField] [Range(0f, 2f)] private float dayCardFadeInSeconds = 0.45f;

        [Header("Gameplay references")]
        [Tooltip("Your existing moderation dashboard controller.")]
        [SerializeField] private WorkDashboardController workDashboard;

        [Header("Teardown")]
        [Tooltip("Optional root to disable after intro finishes or when skipping repeat play — usually the Intro Canvas GameObject. If empty, inferred from Boot/Welcome panels.")]
        [SerializeField] private GameObject introUiRoot;

        private Coroutine _flow;
        private int _tutorialDecisions;
        private bool _tutorialProceedClicked;
        private GameObject _tutorialProceedHudRoot;
        private Canvas _cachedIntroCanvas;
        /// <summary>On <see cref="Canvas"/> root; toggled so fullscreen <c>IntroBackdrop</c> does not eat clicks while moderating underneath.</summary>
        private CanvasGroup _introRootCanvasGroup;

#if ENABLE_INPUT_SYSTEM
        private bool _loggedUnmappedSkipKey;
#endif

        private void Awake()
        {
            AutoBindIfMissing();
            FixCollapsedIntroCanvasOnce();
            EnsureIntroOverlayCanvasGroup();
            SetIntroOverlayBlocksRaycasts(true);
            HideAllIntroPanels();
#if ENABLE_INPUT_SYSTEM
            LogOnceIfSkipKeyNotMapped();
#endif
        }

        /// <summary>If the intro <see cref="Canvas"/> RectTransform scale is collapsed to zero, UI never renders (common editor mistake).</summary>
        private void FixCollapsedIntroCanvasOnce()
        {
            var probe = bootPanel != null
                ? bootPanel
                : welcomePanel != null
                    ? welcomePanel
                    : tutorialHintPanel != null
                        ? tutorialHintPanel
                        : dayCardPanel;
            if (probe == null) return;

            Canvas canvas = probe.GetComponentInParent<Canvas>(true);
            if (canvas == null) return;

            _cachedIntroCanvas = canvas;

            Transform t = canvas.transform;
            Vector3 s = t.localScale;
            float m = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (m > 1e-3f) return;

            t.localScale = Vector3.one;
            Debug.LogWarning(
                $"{nameof(IntroManager)}: Intro Canvas \"{canvas.name}\" had collapsed scale ({s}); reset to (1,1,1). " +
                "Boot / welcome panels would otherwise be invisible.",
                canvas);
        }

        private void OnEnable()
        {
            AutoBindIfMissing();
            TryStartOrSkip();
        }

        private void OnDisable()
        {
            Unwire();
        }

        private void Update()
        {
            if (!allowSkip) return;

            // Only allow skipping while intro has not completed (avoid touching Input APIs after Day 1 starts).
            if (PlayerPrefs.GetInt(PlayerPrefsIntroSeen, 0) == 1)
                return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null || !WasSkipKeyPressed(kb))
                return;
#else
            if (!UnityEngine.Input.GetKeyDown(skipKey))
                return;
#endif

            SkipIntroAndStartDay1();
        }

#if ENABLE_INPUT_SYSTEM
        /// <remarks>
        /// When Player Settings uses <b>Input System Package</b> only, legacy <see cref="Input.GetKeyDown"/> throws.
        /// This maps the inspector's <see cref="KeyCode"/> to the new system's keyboard controls.
        /// </remarks>
        private bool WasSkipKeyPressed(Keyboard kb)
        {
            if (skipKey == KeyCode.Escape) return kb.escapeKey.wasPressedThisFrame;
            if (skipKey == KeyCode.Return) return kb.enterKey.wasPressedThisFrame;
            if (skipKey == KeyCode.KeypadEnter) return kb.numpadEnterKey.wasPressedThisFrame;
            if (skipKey == KeyCode.Space) return kb.spaceKey.wasPressedThisFrame;
            if (skipKey == KeyCode.Tab) return kb.tabKey.wasPressedThisFrame;
            if (skipKey >= KeyCode.A && skipKey <= KeyCode.Z)
            {
                var k = (Key)((int)Key.A + (skipKey - KeyCode.A));
                return kb[k].wasPressedThisFrame;
            }
            if (skipKey >= KeyCode.Alpha0 && skipKey <= KeyCode.Alpha9)
            {
                var k = (Key)((int)Key.Digit0 + (skipKey - KeyCode.Alpha0));
                return kb[k].wasPressedThisFrame;
            }
            if (skipKey >= KeyCode.F1 && skipKey <= KeyCode.F12)
            {
                var k = (Key)((int)Key.F1 + (skipKey - KeyCode.F1));
                return kb[k].wasPressedThisFrame;
            }

            return kb.escapeKey.wasPressedThisFrame;
        }

        /// <summary>Warn once when using Input System skip mapping for an uncommon <see cref="KeyCode"/>.</summary>
        private void LogOnceIfSkipKeyNotMapped()
        {
            if (_loggedUnmappedSkipKey) return;
            if (SupportsSkipKeyForInputSystem(skipKey)) return;

            _loggedUnmappedSkipKey = true;
            Debug.LogWarning(
                $"{nameof(IntroManager)}: skipKey '{skipKey}' has no explicit Input System mapping; " +
                $"skip fallback is Escape only. Assign Escape or extend {nameof(WasSkipKeyPressed)}.",
                this);
        }

        private static bool SupportsSkipKeyForInputSystem(KeyCode key)
        {
            if (key == KeyCode.Escape || key == KeyCode.Return || key == KeyCode.KeypadEnter) return true;
            if (key == KeyCode.Space || key == KeyCode.Tab) return true;
            if (key >= KeyCode.A && key <= KeyCode.Z) return true;
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return true;
            return key >= KeyCode.F1 && key <= KeyCode.F12;
        }
#endif

        private void TryStartOrSkip()
        {
            if (allowSkip && PlayerPrefs.GetInt(PlayerPrefsIntroSeen, 0) == 1)
            {
                StartDay1Session();
                return;
            }

            _flow = StartCoroutine(RunIntro());
        }

        private IEnumerator RunIntro()
        {
            Wire();

            yield return RunBootSequence();
            yield return RunWelcome();
            yield return RunTutorialPosts();
            yield return RunDayCard();

            MarkIntroSeen();
            StartDay1Session();
        }

        private IEnumerator RunBootSequence()
        {
            SetIntroOverlayBlocksRaycasts(true);
            ShowOnly(bootPanel);
            if (bootText != null) bootText.text = "";

            var lines = BuildBootLines();
            foreach (var line in lines)
            {
                AppendBootLine(line);
                yield return new WaitForSecondsRealtime(bootLineDelay + Random.Range(0f, 0.09f));

                // Tiny glitch flicker: very rare extra blank line.
                if (Random.value < 0.08f)
                {
                    AppendBootLine("<alpha=#66>…</alpha>");
                    yield return new WaitForSecondsRealtime(0.05f);
                }
            }

            yield return new WaitForSecondsRealtime(bootEndHold);
        }

        private IEnumerator RunWelcome()
        {
            SetIntroOverlayBlocksRaycasts(true);
            ShowOnly(welcomePanel);
            // Continue is often wired as a sibling of WelcomePanel instead of its child — show it explicitly.
            SetOrphanWelcomeContinueVisible(true);

            if (welcomeBodyText != null)
            {
                welcomeBodyText.text =
                    "<b>Welcome to Flairline Media Moderation</b>\n\n" +
                    "Your responsibility is to maintain platform safety and engagement.\n\n" +
                    "You will review posts from the Central Feed and choose what goes live.\n\n" +
                    "<alpha=#AA>Tip: Approve publishes. Remove blocks. Flag escalates uncertain cases.</alpha>";
            }

            bool clicked = false;
            if (welcomeContinueButton != null)
            {
                welcomeContinueButton.onClick.RemoveAllListeners();
                welcomeContinueButton.onClick.AddListener(() => clicked = true);
            }

            // Wait for click (or a soft timeout so onboarding never stalls if wiring is missing).
            float timeoutAt = Time.unscaledTime + 30f;
            while (!clicked && Time.unscaledTime < timeoutAt)
                yield return null;

            // Leaving welcome step: orphaned Continue buttons don't follow welcomePanel inactive.
            SetOrphanWelcomeContinueVisible(false);
        }

        private IEnumerator RunTutorialPosts()
        {
            _tutorialProceedClicked = false;
            DestroyTutorialProceedHud();

            ShowOnly(tutorialHintPanel);
            // IntroCanvas is sort order 100; IntroBackdrop fills the screen and blocks rays to Work Dashboard underneath.
            // Pass rays through until tutorial finishes — same reason Esc "fixes" interaction (Esc runs skip teardown).
            SetIntroOverlayBlocksRaycasts(false);

            // Build tutorial queue BEFORE opening the dashboard; opening triggers WorkDashboard.OnEnable otherwise.
            if (GameDatabase.Instance != null)
                GameDatabase.Instance.InitializeIntroTutorialSession();

            if (workDashboard != null)
            {
                workDashboard.SetUseGameDatabase(true);
                // Only if inactive: activating triggers OnEnable StartSession — we want one explicit StartSession after init instead.
                if (!workDashboard.gameObject.activeInHierarchy)
                    workDashboard.SuppressAutoStartSessionOnNextEnable();
            }

            // Work dashboard typically starts inactive (DesktopAppWindow.startClosed).
            EnsureWorkDashboardWindowOpen();
            yield return null; // layout / raycasts

            if (workDashboard != null)
                workDashboard.StartSession();

            _tutorialDecisions = 0;
            UpdateTutorialHint(_tutorialDecisions);

            float tutorialStartedAt = Time.unscaledTime;
            GameDatabase db = GameDatabase.Instance;
            float timeoutAt = tutorialStartedAt + 300f; // safety net (button still appears sooner if stalled)

            try
            {
                while (!ShouldAdvanceFromIntroTutorial(db) && Time.unscaledTime < timeoutAt)
                {
                    float elapsed = Time.unscaledTime - tutorialStartedAt;
                    MaybeOfferTutorialProceedBanner(db, elapsed);
                    yield return null;
                    db = GameDatabase.Instance;
                }

                // Small pause so the last decision feedback is readable.
                yield return new WaitForSecondsRealtime(0.35f);
            }
            finally
            {
                DestroyTutorialProceedHud();
            }

            // RunDayCard will block rays again — keep pass-through until then so the 5th post is clickable.
        }

        /// <summary>
        /// True when DecisionRecorded bookkeeping is in sync OR the GameDatabase queue says the tutorial slice is exhausted
        /// (covers scenes without AlgorithmDirector where decisions formerly were not persisted).
        /// </summary>
        private static bool TutorialQueueMarkedComplete(GameDatabase db)
        {
            if (db == null) return false;
            if (db.ModerationQueueCount != IntroTutorialContent.TutorialPostCount) return false;
            return db.GetDecisionsCount() >= db.ModerationQueueCount;
        }

        private bool ShouldAdvanceFromIntroTutorial(GameDatabase db)
        {
            return _tutorialProceedClicked
                   || _tutorialDecisions >= IntroTutorialContent.TutorialPostCount
                   || TutorialQueueMarkedComplete(db);
        }

        private void MaybeOfferTutorialProceedBanner(GameDatabase db, float elapsedSeconds)
        {
            bool tutorialQueueConsumed = TutorialQueueMarkedComplete(db);
            bool stalled = elapsedSeconds > 75f;

            // Offer a clickable escape hatch that lives on the Work Dashboard canvas (Intro overlay intentionally ignores raycasts here).
            if (!tutorialQueueConsumed && !stalled)
                return;

            EnsureTutorialProceedHud();
            if (_tutorialProceedHudRoot == null)
                return;

            if (!_tutorialProceedHudRoot.activeSelf)
            {
                _tutorialProceedHudRoot.SetActive(true);
                _tutorialProceedHudRoot.transform.SetAsLastSibling();
            }

            var bannerLabel = _tutorialProceedHudRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            if (bannerLabel != null)
            {
                bannerLabel.text = tutorialQueueConsumed
                    ? "Continue to Day 1"
                    : "Skip tutorial · Start Day 1";
            }
        }

        private void EnsureTutorialProceedHud()
        {
            if (_tutorialProceedHudRoot != null || workDashboard == null)
                return;

            var parentRt = workDashboard.transform as RectTransform;
            if (parentRt == null)
                return;

            _tutorialProceedHudRoot = new GameObject("IntroTutorialProceedBanner", typeof(RectTransform));

            RectTransform bannerRt = (RectTransform)_tutorialProceedHudRoot.transform;
            bannerRt.SetParent(parentRt, false);

            bannerRt.anchorMin = new Vector2(0f, 0f);
            bannerRt.anchorMax = new Vector2(1f, 0f);
            bannerRt.pivot = new Vector2(0.5f, 0f);
            bannerRt.anchoredPosition = Vector2.zero;
            bannerRt.sizeDelta = new Vector2(0f, 120f);

            var bannerImg = _tutorialProceedHudRoot.AddComponent<Image>();
            bannerImg.color = new Color(0f, 0f, 0f, 0.9f);
            bannerImg.raycastTarget = true;

            var bannerLe = _tutorialProceedHudRoot.AddComponent<LayoutElement>();
            bannerLe.flexibleHeight = 0f;
            bannerLe.preferredHeight = 120f;

            GameObject btnGo = new GameObject("ProceedToDay1", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform btnRt = (RectTransform)btnGo.transform;
            btnRt.SetParent(bannerRt, false);
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(380f, 52f);

            var btnGraphic = btnGo.GetComponent<Image>();
            btnGraphic.color = new Color(0.23f, 0.62f, 0.96f, 1f);

            Button proceed = btnGo.GetComponent<Button>();
            var colors = proceed.colors;
            colors.highlightedColor = new Color(0.35f, 0.7f, 1f);
            colors.pressedColor = new Color(0.18f, 0.52f, 0.82f);
            proceed.colors = colors;

            proceed.onClick.AddListener(() => _tutorialProceedClicked = true);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(btnRt, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;

            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            bool bannerConsumedGuess = TutorialQueueMarkedComplete(GameDatabase.Instance);
            tmp.text = bannerConsumedGuess
                ? "Continue to Day 1"
                : "Skip tutorial · Start Day 1";
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            _tutorialProceedHudRoot.SetActive(false);
        }

        private void DestroyTutorialProceedHud()
        {
            if (_tutorialProceedHudRoot == null) return;

            Destroy(_tutorialProceedHudRoot);
            _tutorialProceedHudRoot = null;
        }

        private IEnumerator RunDayCard()
        {
            CloseWorkDashboardForIntroHandoff();

            SetIntroOverlayBlocksRaycasts(true);
            ShowOnly(dayCardPanel);
            ApplyDayTransitionFullBleed();

            CanvasGroup cardGroup = EnsureCanvasGroup(dayCardPanel);
            if (cardGroup != null)
                cardGroup.ignoreParentGroups = true;
            if (cardGroup != null && dayCardFadeInSeconds > 0.01f)
            {
                cardGroup.alpha = 0f;
                yield return FadeCanvasGroupAlpha(cardGroup, 1f, dayCardFadeInSeconds);
            }
            else if (cardGroup != null)
                cardGroup.alpha = 1f;

            if (dayCardText != null)
            {
                dayCardText.enableAutoSizing = false;
                dayCardText.fontSize = 42;
                dayCardText.color = Color.white;
                dayCardText.text = "<size=52><b>DAY 1</b></size>\n\n<size=26>Beginning live moderation queue.\nYou'll keep using the same tools.</size>";
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, dayCardHoldSeconds));

            // Quick fade before gameplay handoff.
            if (cardGroup != null && dayCardFadeInSeconds > 0.01f)
                yield return FadeCanvasGroupAlpha(cardGroup, 0f, dayCardFadeInSeconds * 0.75f);
        }

        private void MarkIntroSeen()
        {
            PlayerPrefs.SetInt(PlayerPrefsIntroSeen, 1);
            PlayerPrefs.Save();
        }

        private void SkipIntroAndStartDay1()
        {
            SetIntroOverlayBlocksRaycasts(true);
            if (_flow != null) StopCoroutine(_flow);
            MarkIntroSeen();
            StartDay1Session();
        }

        private void StartDay1Session()
        {
            Unwire();
            // Must disable whole Intro Canvas (backdrop + scaler + raycaster), not only panels —
            // otherwise sorting order 100 overlay blocks clicks on the faux desktop indefinitely.
            DisableIntroOverlayForGameplay();

            if (GameManager.Instance != null)
                GameManager.Instance.SetCurrentDay(1);
            else if (GameDatabase.Instance?.Config != null)
                GameDatabase.Instance.Config.currentDay = 1;

            // Initialize the real day 1 moderation queue.
            if (GameDatabase.Instance != null)
                GameDatabase.Instance.InitializeSession();

            // If the window wakes from inactive, OnEnable auto-StartSession would run before we'd want a second call.
            if (workDashboard != null && !workDashboard.gameObject.activeInHierarchy)
                workDashboard.SuppressAutoStartSessionOnNextEnable();

            EnsureWorkDashboardWindowOpen();

            if (workDashboard != null)
            {
                workDashboard.SetUseGameDatabase(true);
                workDashboard.StartSession();
            }
        }

        private void Wire()
        {
            if (GameDatabase.Instance != null)
                GameDatabase.Instance.DecisionRecorded += OnDecisionRecorded;
        }

        private void Unwire()
        {
            if (GameDatabase.Instance != null)
                GameDatabase.Instance.DecisionRecorded -= OnDecisionRecorded;
        }

        private void OnDecisionRecorded(ModerationDecision decision)
        {
            // Do not use GetDecisionsCount() here — DecisionRecorded runs before AdvanceQueue() in Decide(),
            // so _queueIndex lags one behind the recorded decision count and the tutorial loop never completes.
            _tutorialDecisions = GameDatabase.Instance != null ? GameDatabase.Instance.Decisions.Count : (_tutorialDecisions + 1);
            UpdateTutorialHint(_tutorialDecisions);

            // When intro is done, stop showing hints so the day card feels like a clean handoff.
            if (_tutorialDecisions >= IntroTutorialContent.TutorialPostCount && tutorialHintPanel != null)
                tutorialHintPanel.SetActive(false);
        }

        private void UpdateTutorialHint(int decisionsMade)
        {
            if (tutorialHintText == null) return;

            // decisionsMade is "completed so far", so next index is decisionsMade.
            int next = Mathf.Clamp(decisionsMade, 0, IntroTutorialContent.TutorialPostCount - 1);
            tutorialHintText.text = next switch
            {
                0 => "<b>Tutorial</b>\nThis is a normal, harmless post.\nClick <b>APPROVE</b> to publish it.",
                1 => "<b>Tutorial</b>\nThis looks like spam/scam.\nClick <b>REMOVE</b> (Decline) to block it.",
                2 => "<b>Tutorial</b>\nHarmless content keeps the feed alive.\nClick <b>APPROVE</b>.",
                3 => "<b>Tutorial</b>\nNot sure? Escalate it.\nClick <b>FLAG</b> (or Remove if Flag isn’t available).",
                _ => "<b>Tutorial</b>\nThis one isn’t obvious.\nUse your best judgement — you’ll see more like this later."
            };
        }

        private List<string> BuildBootLines()
        {
            return new List<string>
            {
                "Flairline OS v2.7",
                "Initializing moderation systems...",
                "Connecting to Central Feed...",
                "Loading user safety protocols...",
                "Verifying workstation policies...",
                "Calibrating display profile...",
                "Syncing queue cache...",
                "<alpha=#88>Diagnostic: minor signal variance detected</alpha>",
                "Launching Work Dashboard...",
                "Ready."
            };
        }

        private void AppendBootLine(string line)
        {
            if (bootText == null) return;
            bootText.text = string.IsNullOrEmpty(bootText.text) ? line : $"{bootText.text}\n{line}";
        }

        private void HideAllIntroPanels()
        {
            if (bootPanel != null) bootPanel.SetActive(false);
            if (welcomePanel != null) welcomePanel.SetActive(false);
            SetOrphanWelcomeContinueVisible(false);
            if (tutorialHintPanel != null) tutorialHintPanel.SetActive(false);
            if (dayCardPanel != null) dayCardPanel.SetActive(false);
        }

        private void DisableIntroOverlayForGameplay()
        {
            HideAllIntroPanels();

            if (introUiRoot != null)
            {
                introUiRoot.SetActive(false);
                return;
            }

            Canvas canvas = ResolveIntroCanvas();
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        private Canvas ResolveIntroCanvas()
        {
            if (_cachedIntroCanvas != null) return _cachedIntroCanvas;

            var probe = bootPanel != null
                ? bootPanel
                : welcomePanel != null
                    ? welcomePanel
                    : tutorialHintPanel != null
                        ? tutorialHintPanel
                        : dayCardPanel;
            if (probe == null) return null;

            _cachedIntroCanvas = probe.GetComponentInParent<Canvas>(true);
            return _cachedIntroCanvas;
        }

        private void EnsureIntroOverlayCanvasGroup()
        {
            Canvas canvas = ResolveIntroCanvas();
            if (canvas == null) return;

            _introRootCanvasGroup = canvas.GetComponent<CanvasGroup>();
            if (_introRootCanvasGroup == null)
                _introRootCanvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            _introRootCanvasGroup.alpha = 1f;
            _introRootCanvasGroup.interactable = true;
            _introRootCanvasGroup.blocksRaycasts = true;
        }

        private void SetIntroOverlayBlocksRaycasts(bool blockRaycasts)
        {
            EnsureIntroOverlayCanvasGroup();
            if (_introRootCanvasGroup == null) return;
            _introRootCanvasGroup.blocksRaycasts = blockRaycasts;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (go == null) return null;
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static IEnumerator FadeCanvasGroupAlpha(CanvasGroup cg, float endAlpha, float duration)
        {
            if (cg == null)
                yield break;

            float start = cg.alpha;
            endAlpha = Mathf.Clamp01(endAlpha);
            if (duration <= 0f)
            {
                cg.alpha = endAlpha;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(start, endAlpha, Mathf.Clamp01(t / duration));
                yield return null;
            }

            cg.alpha = endAlpha;
        }

        private void ApplyDayTransitionFullBleed()
        {
            if (dayCardPanel == null) return;

            var rt = dayCardPanel.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }

            var img = dayCardPanel.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0f, 0f, 0f, 1f);
                img.raycastTarget = true;
            }

            dayCardPanel.transform.SetAsLastSibling();
        }

        private void CloseWorkDashboardForIntroHandoff()
        {
            if (workDashboard == null) return;
            DesktopAppWindow app = workDashboard.GetComponent<DesktopAppWindow>();
            if (app != null)
                app.Close();
            else
                workDashboard.gameObject.SetActive(false);
        }

        /// <summary>
        /// If <see cref="welcomeContinueButton"/> is not parented under <see cref="welcomePanel"/>,
        /// toggling only the welcome panel won't show/hide the button — handle that here.
        /// </summary>
        private void SetOrphanWelcomeContinueVisible(bool visible)
        {
            if (welcomeContinueButton == null) return;
            if (welcomePanel == null ||
                !welcomeContinueButton.transform.IsChildOf(welcomePanel.transform))
                welcomeContinueButton.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Same GameObject typically has both <see cref="WorkDashboardController"/> and <see cref="DesktopAppWindow"/>
        /// with <see cref="DesktopAppWindow.startClosed"/> turning the window off at boot.
        /// </summary>
        private void EnsureWorkDashboardWindowOpen()
        {
            if (workDashboard == null) return;

            var app = workDashboard.GetComponent<DesktopAppWindow>();
            if (app != null)
                app.Open();
            else if (!workDashboard.gameObject.activeInHierarchy)
                workDashboard.gameObject.SetActive(true);

            workDashboard.transform.SetAsLastSibling();
        }

        private void ShowOnly(GameObject panel)
        {
            HideAllIntroPanels();
            if (panel != null) panel.SetActive(true);
        }

        private void AutoBindIfMissing()
        {
            if (workDashboard != null) return;
#if UNITY_2023_1_OR_NEWER
            workDashboard = FindFirstObjectByType<WorkDashboardController>();
#else
            workDashboard = FindObjectOfType<WorkDashboardController>();
#endif
        }
    }
}

