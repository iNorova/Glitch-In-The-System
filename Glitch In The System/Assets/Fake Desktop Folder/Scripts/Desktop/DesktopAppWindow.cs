using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Open/close wrapper for a UI panel acting like a desktop app.
/// Use <see cref="OpenIfClosed"/> for desktop icons and start-menu launchers (single-click, animated, no toggle).
/// </summary>
public sealed class DesktopAppWindow : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
{
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private bool startClosed = true;
    [SerializeField] private bool openActsAsToggleWhenAlreadyOpen = true;
    [SerializeField] private WorkDashboardController workDashboardController;

    private WindowAnimator _animator;
    private MinimizableWindow _minimizable;

    private void Awake()
    {
        if (windowRoot == null) windowRoot = gameObject;

        _animator    = windowRoot.GetComponent<WindowAnimator>();
        _minimizable = windowRoot.GetComponent<MinimizableWindow>()
                    ?? GetComponent<MinimizableWindow>();

        if (startClosed) windowRoot.SetActive(false);

        DesktopWindowLayer.PrepareWindowRoot(windowRoot);
    }

    public bool IsOpen => IsWindowFullyOpen();

    private bool IsWindowFullyOpen()
    {
        if (windowRoot == null) return false;
        if (_animator != null)
            return _animator.IsLogicallyOpen && !_animator.IsAnimating;
        return windowRoot.activeSelf;
    }

    /// <summary>Launcher entry point: always opens on one click, never toggles closed.</summary>
    public void OpenInstant()
    {
        if (windowRoot == null) return;

        if (_minimizable != null && _minimizable.IsMinimized)
        {
            _minimizable.Restore();
            return;
        }

        DesktopWindowLayer.PrepareWindowRoot(windowRoot);
        BringToFront();

        if (_animator != null)
            _animator.SnapOpen();
        else
            windowRoot.SetActive(true);
    }

    public void Open()
    {
        if (windowRoot == null) return;

        if (_minimizable != null && _minimizable.IsMinimized)
        {
            _minimizable.Restore();
            return;
        }

        // Mid-close: reopen instead of treating activeSelf as "already open".
        if (_animator != null && windowRoot.activeSelf && !_animator.IsLogicallyOpen)
        {
            DesktopWindowLayer.PrepareWindowRoot(windowRoot);
            BringToFront();
            _animator.AnimateOpen();
            return;
        }

        if (openActsAsToggleWhenAlreadyOpen && IsWindowFullyOpen())
        {
            Close();
            return;
        }

        OpenAnimated();
    }

    /// <summary>Single-click launcher entry (animated open, never toggles closed).</summary>
    public void OpenFromLauncher() => OpenIfClosed();

    public void OpenIfClosed()
    {
        if (windowRoot == null) return;

        if (_minimizable != null && _minimizable.IsMinimized)
        {
            _minimizable.Restore();
            return;
        }

        if (_animator != null && windowRoot.activeSelf && !_animator.IsLogicallyOpen)
        {
            OpenAnimated();
            return;
        }

        if (IsWindowFullyOpen())
            return;

        OpenAnimated();
    }

    private void OpenAnimated()
    {
        if (_animator == null && windowRoot != null)
            _animator = windowRoot.GetComponent<WindowAnimator>();

        DesktopHierarchy.EnsureActive(windowRoot);
        DesktopWindowLayer.PrepareWindowRoot(windowRoot);
        BringToFront();

        if (_animator != null)
            _animator.AnimateOpen();
        else if (!windowRoot.activeSelf)
            windowRoot.SetActive(true);
    }

    public void Close()
    {
        if (windowRoot == null) return;

        if (_animator != null)
            _animator.AnimateClose();
        else
            windowRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (windowRoot == null) return;
        if (_minimizable != null && _minimizable.IsMinimized) { _minimizable.Restore(); return; }
        if (IsWindowFullyOpen()) Close();
        else OpenIfClosed();
    }

    public void OnPointerDown(PointerEventData eventData) => BringToFront();
    public void OnBeginDrag(PointerEventData eventData)   => BringToFront();

    private void BringToFront()
    {
        if (windowRoot == null) return;

        var canvas = windowRoot.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Transform shell = windowRoot.transform;
            while (shell.parent != null && shell.parent != canvas.transform)
                shell = shell.parent;
            DesktopUiStackOrder.BringAppShellForward(shell);
        }

        windowRoot.transform.SetAsLastSibling();
    }
}
