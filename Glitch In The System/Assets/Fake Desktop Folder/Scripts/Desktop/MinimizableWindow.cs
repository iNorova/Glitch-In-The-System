using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Add this component to any window that should support minimize-to-taskbar.
/// 
/// Inspector wiring:
///   - windowId       : unique string key (e.g. "SocialMedia", "ContentModerator")
///   - windowRoot     : the root GameObject to hide/show (usually 'this' or the window panel)
///   - taskbarIcon    : Sprite shown in the taskbar when minimized
///   - minimizeButton : the UI Button that triggers minimize (in TopBar)
///   - windowAnimator : optional WindowAnimator on windowRoot
///
/// The script will automatically locate TaskbarManager.Instance at runtime.
/// </summary>
public sealed class MinimizableWindow : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Unique ID used to track this window in the taskbar. Must be unique per window.")]
    [SerializeField] private string windowId = "Window";

    [Header("References")]
    [Tooltip("The root panel to hide when minimized. Defaults to this GameObject if blank.")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("Icon that appears in the taskbar when this window is minimized.")]
    [SerializeField] private Sprite taskbarIcon;

    [Tooltip("The minimize button. Wire the Button's onClick to Minimize() OR leave blank and call Minimize() manually.")]
    [SerializeField] private Button minimizeButton;

    [Tooltip("If present, uses animation when hiding/showing.")]
    [SerializeField] private WindowAnimator windowAnimator;

    public string WindowId => windowId;

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsMinimized { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (windowRoot == null) windowRoot = gameObject;
        if (windowAnimator == null) windowAnimator = windowRoot.GetComponent<WindowAnimator>();

        if (minimizeButton != null)
            minimizeButton.onClick.AddListener(Minimize);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Hides the window and adds its icon to the taskbar.
    /// Safe to call even if already minimized (no-op).
    /// </summary>
    public void Minimize()
    {
        if (IsMinimized) return;
        if (windowRoot == null || !windowRoot.activeSelf) return;

        IsMinimized = true;

        // Hide the window
        if (windowAnimator != null)
            windowAnimator.AnimateClose(() => windowRoot.SetActive(false));
        else
            windowRoot.SetActive(false);

        // Register in taskbar
        var mgr = TaskbarManager.Instance;
        if (mgr != null)
            mgr.RegisterMinimizedApp(windowId, taskbarIcon, Restore);
        else
            Debug.LogWarning("[MinimizableWindow] TaskbarManager.Instance is null. " +
                             "Make sure a TaskbarManager exists in the scene.");
    }

    /// <summary>
    /// Restores the window and removes the taskbar icon.
    /// Called when the player clicks the taskbar icon.
    /// </summary>
    public void Restore()
    {
        if (!IsMinimized) return;

        IsMinimized = false;

        // Unregister from taskbar
        var mgr = TaskbarManager.Instance;
        if (mgr != null) mgr.UnregisterMinimizedApp(windowId);

        // Show the window
        if (windowAnimator != null)
            windowAnimator.AnimateOpen();
        else
        {
            windowRoot.SetActive(true);
        }

        // Bring to front
        if (windowRoot != null)
            windowRoot.transform.SetAsLastSibling();
    }

    /// <summary>
    /// If the window is minimized, restore it. If it's open, minimize it.
    /// Useful for launcher/taskbar toggle logic.
    /// </summary>
    public void ToggleMinimize()
    {
        if (IsMinimized) Restore();
        else Minimize();
    }

    // ── Icon override ─────────────────────────────────────────────────────────
    /// <summary>
    /// Lets other scripts (e.g. DesktopAppWindow) override the taskbar icon at runtime.
    /// </summary>
    public void SetTaskbarIcon(Sprite icon) => taskbarIcon = icon;
}
