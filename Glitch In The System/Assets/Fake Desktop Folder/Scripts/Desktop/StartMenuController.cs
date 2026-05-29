using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ensures Start menu behaves like a desktop menu:
/// - Start button toggles open/close with a Windows-style pop animation
///   (scale + fade only — layout position matches the Scene View)
/// - Clicking FL/W closes the menu
/// </summary>
public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private GameObject startMenuPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button desktopIconButton;
    [SerializeField] private Button fileExplorerButton; // FL
    [SerializeField] private Button workDashboardButton; // W
    [SerializeField] private DesktopAppWindow workDashboardWindow;
    [SerializeField] private SimpleAppWindow socialMediaWindow;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.18f;
    [Tooltip("When true, hides the menu on play start using the scene-authored RectTransform.")]
    [SerializeField] private bool hideMenuOnPlayStart = true;

    private StartButtonToggleProxy _toggleProxy;
    private CanvasGroup _canvasGroup;
    private RectTransform _menuRect;
    private Coroutine _animCoroutine;

    // Scene-authored layout captured once — never modify anchors/pivot at runtime.
    private Vector2 _openAnchoredPosition;
    private Vector2 _openSizeDelta;
    private Vector2 _openAnchorMin;
    private Vector2 _openAnchorMax;
    private Vector2 _openPivot;
    private Vector3 _openLocalScale;
    private bool _openLayoutCaptured;

    private void Awake()
    {
        AutoBind();
        CaptureOpenLayout();

        if (hideMenuOnPlayStart && startMenuPanel != null && startMenuPanel.activeSelf)
        {
            RestoreOpenLayout();
            startMenuPanel.SetActive(false);
        }

        WireStartButton();
        SetupAnimation();
    }

    private void Start()
    {
        DesktopLauncherHub.EnsureInitialized();
    }

    private void OnEnable()
    {
        AutoBind();
        if (!_openLayoutCaptured)
            CaptureOpenLayout();
        WireStartButton();
        SetupAnimation();
    }

    private void SetupAnimation()
    {
        if (startMenuPanel == null) return;

        _menuRect ??= startMenuPanel.GetComponent<RectTransform>();

        _canvasGroup = startMenuPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = startMenuPanel.AddComponent<CanvasGroup>();
    }

    private void CaptureOpenLayout()
    {
        if (startMenuPanel == null) return;

        _menuRect = startMenuPanel.GetComponent<RectTransform>();
        if (_menuRect == null) return;

        _openAnchoredPosition = _menuRect.anchoredPosition;
        _openSizeDelta = _menuRect.sizeDelta;
        _openAnchorMin = _menuRect.anchorMin;
        _openAnchorMax = _menuRect.anchorMax;
        _openPivot = _menuRect.pivot;
        _openLocalScale = _menuRect.localScale;
        _openLayoutCaptured = true;
    }

    /// <summary>Restores the exact RectTransform values authored in the scene.</summary>
    private void RestoreOpenLayout()
    {
        if (_menuRect == null || !_openLayoutCaptured) return;

        _menuRect.anchorMin = _openAnchorMin;
        _menuRect.anchorMax = _openAnchorMax;
        _menuRect.pivot = _openPivot;
        _menuRect.anchoredPosition = _openAnchoredPosition;
        _menuRect.sizeDelta = _openSizeDelta;
        _menuRect.localScale = _openLocalScale;
    }

    // Run after UI click handlers so launcher buttons still receive the same click.
    private void LateUpdate()
    {
        if (startMenuPanel == null || !startMenuPanel.activeSelf) return;
        if (!IsPrimaryClickDown()) return;

        if (IsPointerOver(startMenuPanel.transform) || (startButton != null && IsPointerOver(startButton.transform)))
            return;

        if (IsPointerOverLauncherButton())
            return;

        AnimateClose();
    }

    private bool IsPointerOverLauncherButton()
    {
        if (workDashboardButton != null && IsPointerOver(workDashboardButton.transform)) return true;
        if (fileExplorerButton != null && IsPointerOver(fileExplorerButton.transform)) return true;
        if (desktopIconButton != null && IsPointerOver(desktopIconButton.transform)) return true;

        return false;
    }

    private void AutoBind()
    {
        if (startMenuPanel == null)
        {
            var t = FindObjectByNameInLoadedScenes("StartMenu");
            if (t != null) startMenuPanel = t.gameObject;
        }

        if (startButton == null)
            startButton = FindButtonByName("StartButton");

        if (startMenuPanel != null)
        {
            if (fileExplorerButton == null)
                fileExplorerButton = FindButtonInTransformByName(startMenuPanel.transform, "File Explorer Button");

            if (workDashboardButton == null)
                workDashboardButton = FindButtonInTransformByName(startMenuPanel.transform, "WorkDashboardButton");
        }

        if (fileExplorerButton == null)
            fileExplorerButton = FindButtonByName("File Explorer Button");
        if (workDashboardButton == null)
            workDashboardButton = FindButtonByName("WorkDashboardButton");

        if (desktopIconButton == null)
            desktopIconButton = FindButtonByName("Desktop Icon");

        if (workDashboardWindow == null)
            workDashboardWindow = DesktopAppLocator.Find<DesktopAppWindow>("ContentModerator", "WorkDashboard");

        if (socialMediaWindow == null)
            socialMediaWindow = DesktopAppLocator.Find<SimpleAppWindow>("SocialMedia", "SocialMediaApp");
    }

    private void WireStartButton()
    {
        if (startMenuPanel == null || startButton == null) return;

        _toggleProxy ??= startButton.gameObject.GetComponent<StartButtonToggleProxy>();
        if (_toggleProxy == null) _toggleProxy = startButton.gameObject.AddComponent<StartButtonToggleProxy>();
        _toggleProxy.SetTarget(startMenuPanel);
        _toggleProxy.SetController(this);

        // Replace legacy scene bindings that force SetActive(true) and fight the toggle.
        DesktopUIButtonWiring.SetSingleClickListener(startButton, _toggleProxy.ApplyToggleFromPreClickState);
    }

    private void LaunchWorkDashboardApp()
    {
        if (startMenuPanel != null && startMenuPanel.activeSelf)
            AnimateClose();
        DesktopLauncherHub.OpenWorkDashboard();
    }

    private void LaunchSocialMediaApp()
    {
        if (startMenuPanel != null && startMenuPanel.activeSelf)
            AnimateClose();
        DesktopLauncherHub.OpenSocialMedia();
    }

    public void AnimateOpen()
    {
        if (startMenuPanel == null) return;
        SetupAnimation();
        if (!_openLayoutCaptured)
            CaptureOpenLayout();

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);

        startMenuPanel.SetActive(true);
        RestoreOpenLayout();

        _animCoroutine = StartCoroutine(RunAnimation(opening: true));
    }

    public void AnimateClose()
    {
        if (startMenuPanel == null || !startMenuPanel.activeSelf) return;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(RunAnimation(opening: false));
    }

    private IEnumerator RunAnimation(bool opening)
    {
        if (_menuRect == null || _canvasGroup == null) yield break;

        RestoreOpenLayout();

        float elapsed = 0f;

        Vector3 scaleFrom = opening ? new Vector3(0.85f, 0.85f, 1f) : Vector3.one;
        Vector3 scaleTo = opening ? _openLocalScale : new Vector3(0.85f, 0.85f, 1f);

        float alphaFrom = opening ? 0f : 1f;
        float alphaTo = opening ? 1f : 0f;

        _menuRect.localScale = scaleFrom;
        _canvasGroup.alpha = alphaFrom;

        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            float ease = EaseOutCubic(t);

            _menuRect.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, ease);
            _canvasGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, ease);

            yield return null;
        }

        _menuRect.localScale = scaleTo;
        _canvasGroup.alpha = alphaTo;

        if (opening)
            RestoreOpenLayout();
        else
            startMenuPanel.SetActive(false);

        _animCoroutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private Button FindButtonByName(string objName)
    {
        foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (b == null || !b.gameObject.scene.IsValid()) continue;
            if (b.name == objName) return b;
        }
        return null;
    }

    private static Transform FindObjectByNameInLoadedScenes(string objName)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.name == objName) return t;
        }
        return null;
    }

    private static Button FindButtonInTransformByName(Transform root, string objName)
    {
        if (root == null) return null;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
            if (b.name == objName) return b;
        return null;
    }

    private static bool IsPrimaryClickDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool IsPointerOver(Transform root)
    {
        if (root == null || EventSystem.current == null) return false;

        var data = new PointerEventData(EventSystem.current) { position = GetPointerPosition() };
        var results = new List<RaycastResult>(16);
        EventSystem.current.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            var t = results[i].gameObject != null ? results[i].gameObject.transform : null;
            if (t != null && (t == root || t.IsChildOf(root))) return true;
        }
        return false;
    }

    private sealed class StartButtonToggleProxy : MonoBehaviour, IPointerDownHandler
    {
        private GameObject _target;
        private StartMenuController _controller;
        private bool _wasOpenBeforeClick;

        public void SetTarget(GameObject target) => _target = target;
        public void SetController(StartMenuController controller) => _controller = controller;

        public void OnPointerDown(PointerEventData eventData)
        {
            _wasOpenBeforeClick = _target != null && _target.activeSelf;
        }

        public void ApplyToggleFromPreClickState()
        {
            if (_target == null || _controller == null) return;

            if (_wasOpenBeforeClick)
                _controller.AnimateClose();
            else
                _controller.AnimateOpen();
        }
    }
}
