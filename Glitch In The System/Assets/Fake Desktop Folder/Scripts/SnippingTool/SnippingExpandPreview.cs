using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GlitchInTheSystem.UI;   // DragPanel namespace

/// <summary>
/// Attach to SnippingToolAppWindow alongside SnippingCapture.
///
/// Clicking the small PreviewImage OR calling OpenModal(texture) opens a separate
/// draggable preview window that matches the desktop window style exactly:
///   - Same WindowAnimator open/close animation (scale + fade)
///   - DragPanel on TopBar for free dragging
///   - SimpleAppWindow for correct z-order management
///   - Centered on open, movable afterward
///   - Close button + ESC to dismiss
///
/// PUBLIC API is unchanged - FsAppRouter can call OpenModal(Texture2D) as before.
/// ZERO changes to SnippingCapture, SnippingToolApp, or FsAppRouter.
///
/// The preview window is built once and reused - texture swapped on each open.
/// </summary>
[RequireComponent(typeof(SnippingCapture))]
public sealed class SnippingExpandPreview : MonoBehaviour
{
    [Tooltip("The RawImage that shows the small preview. Clicking it opens the preview window.")]
    [SerializeField] private RawImage previewImage;

    private SnippingCapture _capture;

    // Persistent window objects - built once, reused every open.
    private GameObject      _windowShell;   // FakeDesktop-level shell (like "Snipping Tool" GO)
    private GameObject      _windowRoot;    // the actual window GO - has WindowAnimator, SimpleAppWindow
    private RawImage        _previewImg;    // RawImage inside the window body
    private AspectRatioFitter _fitter;
    private WindowAnimator  _animator;
    private SimpleAppWindow _appWindow;
    private TMPro.TextMeshProUGUI _titleTMP;
    private ResizableWindow       _resizable;
    private string                _lastFileName;
    private Texture2D             _ownedPreviewTexture; // textures loaded by FsAppRouter; we destroy these

    // -- Lifecycle ----------------------------------------------------------
    private void Awake()
    {
        _capture = GetComponent<SnippingCapture>();
    }

    private void Start()
    {
        if (previewImage != null)
        {
            // Wire click on the inline preview → open the preview window
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
        // ESC closes the preview window if it is open and this window is in focus
        if (_windowRoot != null && _windowRoot.activeSelf
            && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseModal();
    }

    // -- Public API ---------------------------------------------------------
    /// Open preview window with the last snip capture (called by preview click).
    public void OpenModal()
    {
        if (_capture == null || _capture.LastCapture == null) return;
        // Use the timestamp as the filename for snipping-tool captures
        _lastFileName = $"Snip_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        OpenModal(_capture.LastCapture);
    }

    /// Open preview window with any external texture (called by FsAppRouter for PNG files).
    public void OpenModal(Texture2D texture)
    {
        if (texture == null) return;

        // Destroy the previously owned texture before assigning a new one.
        // Only textures loaded externally (FsAppRouter) are owned here.
        // _capture.LastCapture is managed by SnippingCapture — never destroy it here.
        bool isSnipCapture = _capture != null && texture == _capture.LastCapture;
        if (!isSnipCapture)
        {
            if (_ownedPreviewTexture != null && _ownedPreviewTexture != texture)
            {
                Destroy(_ownedPreviewTexture);
                _ownedPreviewTexture = null;
            }
            _ownedPreviewTexture = texture;
        }

        EnsureWindowBuilt();
        if (_windowShell == null) return; // FakeDesktop not found


        // Update texture + aspect ratio
        _previewImg.texture = texture;
        if (_fitter != null)
        {
            _fitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = (float)texture.width / Mathf.Max(1, texture.height);
        }

        // Update title: prefer explicit filename, fall back to dimensions
        if (_titleTMP != null)
        {
            _titleTMP.text = !string.IsNullOrEmpty(_lastFileName)
                ? _lastFileName
                : $"Preview - {texture.width}\u00d7{texture.height}";
        }

        // Open via WindowAnimator - matches every other desktop window animation exactly.
        // SimpleAppWindow.OpenIfClosed() handles: EnsureActive, BringToFront, AnimateOpen.
        // Check parent chain before OpenIfClosed
        _appWindow.OpenIfClosed();

        // If already open (e.g. FsAppRouter called again), just bring to front + swap texture.
        _windowRoot.transform.SetAsLastSibling();
    }

    /// Open preview with an explicit filename shown in the title bar.
    public void OpenModal(Texture2D texture, string fileName)
    {
        _lastFileName = fileName;
        OpenModal(texture);
    }

    public void CloseModal()
    {
        if (_appWindow != null) _appWindow.Close();
    }

    // -- Lifecycle (owned texture cleanup)
    private void OnDestroy()
    {
        if (_ownedPreviewTexture != null)
        {
            Destroy(_ownedPreviewTexture);
            _ownedPreviewTexture = null;
        }
    }

    // -- Window builder -----------------------------------------------------
    /// Builds the preview window once, parented to FakeDesktop at app-shell level.
    /// Hierarchy mirrors every other desktop app:
    ///   FakeDesktop
    ///     SnipPreview [shell, stretch-fill, no components]
    ///       SnipPreviewWindow [WindowAnimator, SimpleAppWindow, CanvasGroup, Image(bg)]
    ///         FloatingPanel [Image(panel)]
    ///           TopBar [Image, DragPanel, HLG] → TitleText + CloseButton
    ///           Body   [Image, padding] → ImageContainer → RawImage + AspectRatioFitter
    private void EnsureWindowBuilt()
    {
        if (_windowRoot != null) return;

        var fd = FindFakeDesktop();
        if (fd == null) { Debug.LogError("[SnippingExpandPreview] FakeDesktop not found."); return; }

        int uiLayer = LayerMask.NameToLayer("UI");
        const float W = 960f;   // ~half of 1920×1080
        const float H = 540f;

        // -- Shell (stretch-fill, no components - matches other app shells) --
        _windowShell = MakeGO("SnipPreview", fd.transform, uiLayer);
        SetRT(_windowShell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        // -- Window root (WindowAnimator + SimpleAppWindow + CanvasGroup) ---
        _windowRoot = MakeGO("SnipPreviewWindow", _windowShell.transform, uiLayer);
        SetRT(_windowRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
              Vector2.zero, new Vector2(W, H), new Vector2(0.5f, 0.5f));

        var rootImg = _windowRoot.AddComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0f); // transparent root - FloatingPanel provides the bg
        rootImg.raycastTarget = false;

        _windowRoot.AddComponent<CanvasGroup>(); // required by WindowAnimator

        // -- Deactivate BEFORE AddComponent so Awake() is deferred ----------
        // SimpleAppWindow.Awake() fires synchronously on AddComponent if the GO is active.
        // At that moment our serialized fields aren't set yet → EnsureInit() falls back
        // to windowRoot=self with wrong cached values that are never corrected.
        // Deactivating first means Awake() runs on the FIRST SetActive(true) call,
        // by which time reflection has already written the correct field values.
        _windowRoot.SetActive(false);

        _animator   = _windowRoot.AddComponent<WindowAnimator>();   // Awake deferred
        _appWindow  = _windowRoot.AddComponent<SimpleAppWindow>();  // Awake deferred
        _resizable  = _windowRoot.AddComponent<ResizableWindow>();  // resize from edges

        // Set serialized fields via reflection before first activation.
        var rbf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public;
        var windowRootField  = typeof(SimpleAppWindow).GetField("windowRoot",  rbf);
        var startClosedField = typeof(SimpleAppWindow).GetField("startClosed", rbf);
        windowRootField? .SetValue(_appWindow, _windowRoot);
        startClosedField?.SetValue(_appWindow, true);

        // Wire ResizableWindow — mirrors StickyNotesAppWindow setup
        if (_resizable != null)
        {
            var rwRbf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                      | System.Reflection.BindingFlags.Public;
            typeof(ResizableWindow).GetField("windowRect",  rwRbf)?.SetValue(_resizable, _windowRoot.GetComponent<RectTransform>());
            typeof(ResizableWindow).GetField("minWidth",    rwRbf)?.SetValue(_resizable, 480f);
            typeof(ResizableWindow).GetField("minHeight",   rwRbf)?.SetValue(_resizable, 270f);
            typeof(ResizableWindow).GetField("handleSize",  rwRbf)?.SetValue(_resizable, 18f);
        }

        // -- FloatingPanel -------------------------------------------------
        var fp = MakeGO("FloatingPanel", _windowRoot.transform, uiLayer);
        SetRT(fp, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        AddImage(fp, new Color(0.14f, 0.14f, 0.18f, 1f));

        // -- TopBar --------------------------------------------------------
        var topBar = MakeGO("TopBar", fp.transform, uiLayer);
        SetRT(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f),
              Vector2.zero, new Vector2(0f, 32f), new Vector2(0.5f, 1f));
        AddImage(topBar, new Color(0f, 0f, 0.502f, 1f));

        // DragPanel - target is the window root so dragging moves the whole window
        var dp = topBar.AddComponent<DragPanel>();
        dp.SetTarget(_windowRoot.GetComponent<RectTransform>());

        // HLG for TopBar children
        var hlg = topBar.AddComponent<HorizontalLayoutGroup>();
        hlg.padding               = new RectOffset(10, 4, 0, 0);
        hlg.spacing               = 4;
        hlg.childAlignment        = TextAnchor.MiddleLeft;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight= true;

        // Title label
        var titleGO = MakeGO("TitleText", topBar.transform, uiLayer);
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth  = 1f;
        titleLE.flexibleHeight = 1f;
        _titleTMP = titleGO.AddComponent<TMPro.TextMeshProUGUI>();
        _titleTMP.text      = "Preview";
        _titleTMP.fontSize  = 12f;
        _titleTMP.fontStyle = TMPro.FontStyles.Normal;
        _titleTMP.color     = Color.white;
        _titleTMP.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        _titleTMP.raycastTarget = false;

        // Close button
        var closeBtnGO = MakeGO("CloseButton", topBar.transform, uiLayer);
        var closeBtnLE = closeBtnGO.AddComponent<LayoutElement>();
        closeBtnLE.preferredWidth  = 32f; closeBtnLE.flexibleWidth  = 0f;
        closeBtnLE.preferredHeight = 24f; closeBtnLE.flexibleHeight = 0f;
        AddImage(closeBtnGO, new Color(0.70f, 0.10f, 0.10f, 1f));
        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseModal);
        var closeLblGO = MakeGO("Label", closeBtnGO.transform, uiLayer);
        var clRT = closeLblGO.GetComponent<RectTransform>(); // MakeGO already added RT - AddComponent returns null
        clRT.anchorMin = Vector2.zero; clRT.anchorMax = Vector2.one;
        clRT.offsetMin = clRT.offsetMax = Vector2.zero;
        var clTMP = closeLblGO.AddComponent<TMPro.TextMeshProUGUI>();
        clTMP.text = "X"; clTMP.fontSize = 13f; clTMP.color = Color.white;
        clTMP.alignment = TMPro.TextAlignmentOptions.Center; clTMP.raycastTarget = false;

        // -- Body ----------------------------------------------------------
        var body = MakeGO("Body", fp.transform, uiLayer);
        SetRT(body, new Vector2(0f, 0f), new Vector2(1f, 1f),
              new Vector2(0f, -16f), new Vector2(0f, -32f), new Vector2(0.5f, 0.5f));
        AddImage(body, new Color(0.10f, 0.10f, 0.13f, 1f));

        // Image container - 12px padding inside body
        var imgContainer = MakeGO("ImageContainer", body.transform, uiLayer);
        SetRT(imgContainer, Vector2.zero, Vector2.one,
              Vector2.zero, new Vector2(-24f, -24f), new Vector2(0.5f, 0.5f));

        // RawImage + AspectRatioFitter - fills container, preserves ratio
        var imgGO = MakeGO("PreviewImage", imgContainer.transform, uiLayer);
        SetRT(imgGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        _previewImg = imgGO.AddComponent<RawImage>();
        _previewImg.color = Color.white;
        _previewImg.raycastTarget = false;
        _fitter = imgGO.AddComponent<AspectRatioFitter>();
        _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        // Start hidden - SimpleAppWindow.startClosed handles this via SetActive(false) in Awake
        _windowRoot.SetActive(false);
    }

    // -- Static helpers -----------------------------------------------------
    private static GameObject MakeGO(string goName, Transform parent, int layer)
    {
        var go = new GameObject(goName);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void SetRT(GameObject go,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta, Vector2 pivot)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static GameObject FindFakeDesktop()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                                        .GetRootGameObjects())
        {
            if (root.name == "FakeDesktop") return root;
            var found = root.transform.Find("FakeDesktop");
            if (found != null) return found.gameObject;
        }
        return null;
    }
}
