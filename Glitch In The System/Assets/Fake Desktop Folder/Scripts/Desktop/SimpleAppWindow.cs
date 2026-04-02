using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Generic app window open/close wrapper (no dashboard-specific dependency).
/// </summary>
public sealed class SimpleAppWindow : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
{
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private bool startClosed = false;

    private void Awake()
    {
        if (windowRoot == null) windowRoot = gameObject;
        if (startClosed && windowRoot != null) windowRoot.SetActive(false);
    }

    public void Open()
    {
        if (windowRoot == null) return;
        BringToFront();
        windowRoot.SetActive(true);
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
