using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the same GO as SnippingCapture (SnippingToolAppWindow).
///
/// Wires a click on PreviewImage → opens a fullscreen modal showing
/// the captured Texture2D at its native resolution inside an AspectRatioFitter.
///
/// Also exposes OpenModal(Texture2D) so FsAppRouter can open any PNG file
/// in preview mode without routing to Paint.
///
/// ESC or the X button closes the modal.
/// </summary>
[RequireComponent(typeof(SnippingCapture))]
public sealed class SnippingExpandPreview : MonoBehaviour
{
    [Tooltip("The RawImage that shows the small preview. Clicking it opens the modal.")]
    [SerializeField] private RawImage previewImage;

    private SnippingCapture _capture;
    private GameObject      _modal;
    private RawImage        _modalImage;

    private void Awake()
    {
        _capture = GetComponent<SnippingCapture>();
    }

    private void Start()
    {
        if (previewImage != null)
        {
            var trigger = previewImage.gameObject.GetComponent<EventTrigger>()
                          ?? previewImage.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => OpenModal());
            trigger.triggers.Add(entry);
            previewImage.raycastTarget = true;
        }
    }

    private void Update()
    {
        if (_modal != null && _modal.activeSelf
            && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseModal();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// Open modal with the last snip capture (called by preview click).
    public void OpenModal()
    {
        if (_capture == null || _capture.LastCapture == null) return;
        OpenModal(_capture.LastCapture);
    }

    /// Open modal with any external texture (called by FsAppRouter for PNG files).
    public void OpenModal(Texture2D texture)
    {
        if (texture == null) return;

        EnsureModalBuilt();

        _modalImage.texture = texture;

        var fitter = _modalImage.GetComponent<AspectRatioFitter>();
        if (fitter != null)
        {
            fitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)texture.width / texture.height;
        }

        _modal.SetActive(true);
        _modal.transform.SetAsLastSibling();
    }

    public void CloseModal()
    {
        if (_modal != null) _modal.SetActive(false);
    }

    // ── Modal builder ─────────────────────────────────────────────────────
    private void EnsureModalBuilt()
    {
        if (_modal != null) return;

        var fakeDesktop = FindFakeDesktop();
        if (fakeDesktop == null) { Debug.LogError("[SnippingExpandPreview] FakeDesktop not found."); return; }

        // Backdrop
        _modal = new GameObject("SnippingExpandModal");
        _modal.transform.SetParent(fakeDesktop.transform, false);
        _modal.layer = LayerMask.NameToLayer("UI");
        var modalRT = _modal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero; modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = modalRT.offsetMax = Vector2.zero;
        _modal.AddComponent<CanvasRenderer>();
        var bg = _modal.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        bg.raycastTarget = true;

        // Close button
        var closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(_modal.transform, false);
        closeGO.layer = LayerMask.NameToLayer("UI");
        var closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f); closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-12f, -12f); closeRT.sizeDelta = new Vector2(36f, 36f);
        closeGO.AddComponent<CanvasRenderer>();
        closeGO.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);
        closeGO.AddComponent<Button>().onClick.AddListener(CloseModal);
        var closeLbl = new GameObject("Label");
        closeLbl.transform.SetParent(closeGO.transform, false);
        var clRT = closeLbl.AddComponent<RectTransform>();
        clRT.anchorMin = Vector2.zero; clRT.anchorMax = Vector2.one;
        clRT.offsetMin = clRT.offsetMax = Vector2.zero;
        var clTMP = closeLbl.AddComponent<TMPro.TextMeshProUGUI>();
        clTMP.text = "X"; clTMP.fontSize = 18f; clTMP.color = Color.white;
        clTMP.alignment = TMPro.TextAlignmentOptions.Center; clTMP.raycastTarget = false;

        // Image container
        var imgContainer = new GameObject("ImageContainer");
        imgContainer.transform.SetParent(_modal.transform, false);
        imgContainer.layer = LayerMask.NameToLayer("UI");
        var icRT = imgContainer.AddComponent<RectTransform>();
        icRT.anchorMin = Vector2.zero; icRT.anchorMax = Vector2.one;
        icRT.offsetMin = new Vector2(48f, 48f); icRT.offsetMax = new Vector2(-48f, -48f);

        // RawImage
        var imgGO = new GameObject("ExpandedImage");
        imgGO.transform.SetParent(imgContainer.transform, false);
        imgGO.layer = LayerMask.NameToLayer("UI");
        var imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero; imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;
        imgGO.AddComponent<CanvasRenderer>();
        _modalImage = imgGO.AddComponent<RawImage>();
        _modalImage.color = Color.white; _modalImage.raycastTarget = false;
        var fitter = imgGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        _modal.SetActive(false);
    }

    private static GameObject FindFakeDesktop()
    {
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name == "FakeDesktop") return r;
            var found = r.transform.Find("FakeDesktop");
            if (found != null) return found.gameObject;
        }
        return null;
    }
}
