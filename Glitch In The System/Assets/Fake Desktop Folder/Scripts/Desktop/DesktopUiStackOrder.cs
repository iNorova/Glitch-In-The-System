using UnityEngine;

/// <summary>
/// Keeps interruption overlay / minigame UI above desktop app windows on the FakeDesktop canvas.
/// </summary>
public static class DesktopUiStackOrder
{
    private static Transform _desktopRoot;
    private static Transform _interruptionOverlay;
    private static Transform _interruptionLoading;
    private static Transform _minigameRoot;

    public static bool IsInterruptionBlocking { get; private set; }

    public static void SetInterruptionBlocking(bool blocking)
    {
        IsInterruptionBlocking = blocking;
        if (blocking)
            RaiseInterruptionStack();
    }

    public static void RaiseInterruptionStack()
    {
        RefreshCache();
        if (_desktopRoot == null) return;

        IsInterruptionBlocking = true;

        if (_interruptionOverlay != null)
            _interruptionOverlay.SetAsLastSibling();

        if (_interruptionLoading != null)
            _interruptionLoading.SetAsLastSibling();

        if (_minigameRoot != null)
            _minigameRoot.SetAsLastSibling();
    }

    public static void BringAppShellForward(Transform appShell)
    {
        if (appShell == null) return;

        RefreshCache();

        if (IsInterruptionBlocking)
        {
            PlaceAppShellBelowInterruption(appShell);
            return;
        }

        appShell.SetAsLastSibling();
    }

    public static void PlaceAppShellBelowInterruption(Transform appShell)
    {
        if (appShell == null) return;

        RefreshCache();
        if (_desktopRoot == null || appShell.parent != _desktopRoot) return;

        int cap = GetFirstInterruptionSiblingIndex();
        if (cap < 0)
            return;

        int target = Mathf.Max(0, cap - 1);
        appShell.SetSiblingIndex(target);
    }

    private static int GetFirstInterruptionSiblingIndex()
    {
        int index = int.MaxValue;

        if (_interruptionOverlay != null)
            index = Mathf.Min(index, _interruptionOverlay.GetSiblingIndex());

        if (_interruptionLoading != null)
            index = Mathf.Min(index, _interruptionLoading.GetSiblingIndex());

        if (_minigameRoot != null)
            index = Mathf.Min(index, _minigameRoot.GetSiblingIndex());

        return index == int.MaxValue ? -1 : index;
    }

    private static void RefreshCache()
    {
        if (_desktopRoot != null) return;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
            if (!canvas.gameObject.name.Contains("FakeDesktop")) continue;

            _desktopRoot = canvas.transform;
            _interruptionOverlay = _desktopRoot.Find("InterruptionOverlay");
            _interruptionLoading = _desktopRoot.Find("InterruptionLoading");

            if (_interruptionOverlay != null)
                _minigameRoot = _interruptionOverlay.Find("MinigameRoot");
            break;
        }
    }
}
