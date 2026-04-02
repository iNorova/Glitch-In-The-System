using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple open/close wrapper for a UI panel acting like a desktop app.
/// </summary>
public sealed class DesktopAppWindow : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
{
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private bool startClosed = true;
    [SerializeField] private WorkDashboardController workDashboardController;

    private void Awake()
    {
        if (windowRoot == null) windowRoot = gameObject;
        if (startClosed) windowRoot.SetActive(false);
    }

    public void Open()
    {
        if (windowRoot == null) return;
        BringToFront();
        windowRoot.SetActive(true);
        if (workDashboardController != null) workDashboardController.StartSession();
    }

    public void Close()
    {
        if (windowRoot == null) return;
        windowRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (windowRoot == null) return;
        if (windowRoot.activeSelf) Close();
        else Open();
    }

    public void OnPointerDown(PointerEventData eventData) => BringToFront();
    public void OnBeginDrag(PointerEventData eventData) => BringToFront();

    private void BringToFront()
    {
        if (windowRoot == null) return;
        windowRoot.transform.SetAsLastSibling();
    }
}

