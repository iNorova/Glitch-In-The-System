using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the dynamic taskbar area for minimized app icons.
/// Attach to the Taskbar GameObject (or any persistent manager).
/// 
/// When a window is minimized, call RegisterMinimizedApp().
/// When a window is restored, call UnregisterMinimizedApp().
/// </summary>
public sealed class TaskbarManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static TaskbarManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Tooltip("The Taskbar GameObject that contains the icon slots. " +
             "Auto-found by name 'Taskbar' if left blank.")]
    [SerializeField] private Transform taskbarRoot;

    [Tooltip("Prefab used to create a dynamic taskbar button. " +
             "If blank, a button is built at runtime from the icon sprite.")]
    [SerializeField] private GameObject taskbarButtonPrefab;

    [Tooltip("Size of each taskbar icon button (width x height).")]
    [SerializeField] private Vector2 iconSize = new Vector2(50f, 50f);

    [Tooltip("X offset from StartButton right edge where the first dynamic icon starts.")]
    [SerializeField] private float startX = 350f;

    [Tooltip("Gap between dynamic icons.")]
    [SerializeField] private float iconSpacing = 8f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private readonly List<TaskbarEntry> _entries = new List<TaskbarEntry>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (taskbarRoot == null)
        {
            var go = GameObject.Find("Taskbar");
            if (go != null) taskbarRoot = go.transform;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by a minimizable window when it minimizes itself.
    /// Creates a taskbar button that restores the window on click.
    /// </summary>
    /// <param name="windowId">Unique ID so we don't add duplicates.</param>
    /// <param name="icon">Sprite shown on the taskbar button.</param>
    /// <param name="onRestore">Callback that restores the window.</param>
    public void RegisterMinimizedApp(string windowId, Sprite icon, System.Action onRestore)
    {
        // Prevent duplicates
        if (IsRegistered(windowId)) return;

        GameObject btn = CreateTaskbarButton(icon, windowId);
        if (btn == null) return;

        var button = btn.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => onRestore?.Invoke());

        var entry = new TaskbarEntry { windowId = windowId, buttonGO = btn };
        _entries.Add(entry);
        RefreshLayout();
    }

    /// <summary>
    /// Called when a window is restored (or closed).
    /// Removes the taskbar button for that window.
    /// </summary>
    public void UnregisterMinimizedApp(string windowId)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].windowId == windowId)
            {
                if (_entries[i].buttonGO != null)
                    Destroy(_entries[i].buttonGO);
                _entries.RemoveAt(i);
            }
        }
        RefreshLayout();
    }

    public bool IsRegistered(string windowId)
    {
        foreach (var e in _entries)
            if (e.windowId == windowId) return true;
        return false;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private GameObject CreateTaskbarButton(Sprite icon, string windowId)
    {
        if (taskbarRoot == null)
        {
            Debug.LogWarning("[TaskbarManager] taskbarRoot is null – cannot create taskbar button.");
            return null;
        }

        GameObject btn;

        if (taskbarButtonPrefab != null)
        {
            btn = Instantiate(taskbarButtonPrefab, taskbarRoot);
        }
        else
        {
            // Build a minimal button at runtime
            btn = new GameObject($"TaskbarIcon_{windowId}", typeof(RectTransform),
                                 typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btn.transform.SetParent(taskbarRoot, false);

            var img = btn.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.color = Color.white;

            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = iconSize;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
        }

        btn.name = $"TaskbarIcon_{windowId}";
        return btn;
    }

    private void RefreshLayout()
    {
        float x = startX;
        foreach (var entry in _entries)
        {
            if (entry.buttonGO == null) continue;
            var rt = entry.buttonGO.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.anchoredPosition = new Vector2(x, 0f);
            x += iconSize.x + iconSpacing;
        }
    }

    // ── Nested helper ─────────────────────────────────────────────────────────
    private class TaskbarEntry
    {
        public string     windowId;
        public GameObject buttonGO;
    }
}
