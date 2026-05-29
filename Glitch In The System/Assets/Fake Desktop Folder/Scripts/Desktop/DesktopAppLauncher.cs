using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single-click desktop/start-menu launcher for app windows.
/// Clears duplicate scene onClick bindings and opens the target window on one click (animated).
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class DesktopAppLauncher : MonoBehaviour
{
    [SerializeField] private DesktopAppWindow desktopApp;
    [SerializeField] private SimpleAppWindow simpleApp;
    [SerializeField] private bool closeStartMenuAfterLaunch = true;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        DisableChildRaycastBlockers();
        DesktopUIButtonWiring.SetSingleClickListener(_button, Launch);
    }

    private void Launch()
    {
        if (closeStartMenuAfterLaunch)
            CloseStartMenuIfOpen();

        if (desktopApp != null)
            desktopApp.OpenFromLauncher();
        else if (simpleApp != null)
            simpleApp.OpenFromLauncher();
    }

    private void DisableChildRaycastBlockers()
    {
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.gameObject != gameObject)
                tmp.raycastTarget = false;
        }

        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject == gameObject) continue;
            if (graphic.GetComponent<Button>() != null) continue;
            graphic.raycastTarget = false;
        }
    }

    private static void CloseStartMenuIfOpen()
    {
        var menu = GameObject.Find("StartMenu");
        if (menu != null && menu.activeSelf)
            menu.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (desktopApp == null)
            desktopApp = FindWindow<DesktopAppWindow>();
        if (simpleApp == null)
            simpleApp = FindWindow<SimpleAppWindow>();
    }

    private T FindWindow<T>() where T : Component
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.gameObject.scene.IsValid())
                return c;
        }
        return null;
    }
#endif
}
