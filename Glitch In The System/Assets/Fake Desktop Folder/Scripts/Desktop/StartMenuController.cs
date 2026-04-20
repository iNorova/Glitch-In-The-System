using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ensures Start menu behaves like a desktop menu:
/// - Start button toggles open/close
/// - Clicking FL/W closes the menu
/// Works even if scene has persistent StartButton -> SetActive(true) binding.
/// </summary>
public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private GameObject startMenuPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button fileExplorerButton; // FL
    [SerializeField] private Button workDashboardButton; // W

    private StartButtonToggleProxy _toggleProxy;

    private void Awake()
    {
        AutoBind();
        Wire();
    }

    private void OnEnable()
    {
        AutoBind();
        Wire();
    }

    private void Update()
    {
        if (startMenuPanel == null || !startMenuPanel.activeSelf) return;
        if (!IsPrimaryClickDown()) return;

        var panelRt = startMenuPanel.transform as RectTransform;
        var startRt = startButton != null ? startButton.transform as RectTransform : null;
        Vector2 screenPoint = GetPointerPosition();

        bool insidePanel = panelRt != null && RectTransformUtility.RectangleContainsScreenPoint(panelRt, screenPoint, null);
        bool insideStart = startRt != null && RectTransformUtility.RectangleContainsScreenPoint(startRt, screenPoint, null);
        if (!insidePanel && !insideStart)
            CloseStartMenu();
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

        // Fallbacks if start menu children were renamed or moved.
        if (fileExplorerButton == null)
            fileExplorerButton = FindButtonByName("File Explorer Button");
        if (workDashboardButton == null)
            workDashboardButton = FindButtonByName("WorkDashboardButton");
    }

    private void Wire()
    {
        if (startMenuPanel == null || startButton == null) return;

        _toggleProxy ??= startButton.gameObject.GetComponent<StartButtonToggleProxy>();
        if (_toggleProxy == null) _toggleProxy = startButton.gameObject.AddComponent<StartButtonToggleProxy>();
        _toggleProxy.SetTarget(startMenuPanel);

        startButton.onClick.RemoveListener(_toggleProxy.ApplyToggleFromPreClickState);
        startButton.onClick.AddListener(_toggleProxy.ApplyToggleFromPreClickState);

        if (fileExplorerButton != null)
        {
            fileExplorerButton.onClick.RemoveListener(CloseStartMenu);
            fileExplorerButton.onClick.AddListener(CloseStartMenu);
        }

        if (workDashboardButton != null)
        {
            workDashboardButton.onClick.RemoveListener(CloseStartMenu);
            workDashboardButton.onClick.AddListener(CloseStartMenu);
        }
    }

    private void CloseStartMenu()
    {
        if (startMenuPanel != null) startMenuPanel.SetActive(false);
    }

    private Button FindButtonByName(string name)
    {
        foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (b == null || !b.gameObject.scene.IsValid()) continue;
            if (b.name == name) return b;
        }
        return null;
    }

    private static Transform FindObjectByNameInLoadedScenes(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.name == name) return t;
        }
        return null;
    }

    private static Transform FindInChildrenByName(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            var nested = FindInChildrenByName(child, name);
            if (nested != null) return nested;
        }
        return null;
    }

    private static Button FindButtonInTransformByName(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
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

    /// <summary>
    /// Captures pre-click open state on pointer-down, then applies true toggle on click.
    /// This neutralizes persistent scene binding that always sets StartMenu active true.
    /// </summary>
    private sealed class StartButtonToggleProxy : MonoBehaviour, IPointerDownHandler
    {
        private GameObject _target;
        private bool _wasOpenBeforeClick;

        public void SetTarget(GameObject target) => _target = target;

        public void OnPointerDown(PointerEventData eventData)
        {
            _wasOpenBeforeClick = _target != null && _target.activeSelf;
        }

        public void ApplyToggleFromPreClickState()
        {
            if (_target == null) return;
            _target.SetActive(!_wasOpenBeforeClick);
        }
    }
}

