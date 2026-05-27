using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Plays a click sound on mouse button press across all scenes.
/// Add to one bootstrap object in the first scene (e.g. LoginScene).
/// </summary>
[DisallowMultipleComponent]
public sealed class GlobalClickAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip clickClip;
    [SerializeField] [Range(0f, 1f)] private float clickVolume = 0.5f;

    [Header("Mouse Buttons")]
    [SerializeField] private bool leftClick = true;
    [SerializeField] private bool rightClick;
    [SerializeField] private bool middleClick;

    [Header("Behavior")]
    [Tooltip("If true, this object survives scene loads and works across all scenes.")]
    [SerializeField] private bool persistAcrossScenes = true;
    [Tooltip("If true, click sounds only play when cursor is over UI.")]
    [SerializeField] private bool uiOnly = false;

    private static GlobalClickAudio _instance;
    private AudioSource _audioSource;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (clickClip == null || _audioSource == null)
            return;

        if (uiOnly && !IsPointerOverUi())
            return;

        bool clicked = IsConfiguredMouseClickPressed();

        if (clicked)
            _audioSource.PlayOneShot(clickClip, clickVolume);
    }

    private bool IsConfiguredMouseClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;

        return (leftClick && mouse.leftButton.wasPressedThisFrame) ||
               (rightClick && mouse.rightButton.wasPressedThisFrame) ||
               (middleClick && mouse.middleButton.wasPressedThisFrame);
#else
        return (leftClick && Input.GetMouseButtonDown(0)) ||
               (rightClick && Input.GetMouseButtonDown(1)) ||
               (middleClick && Input.GetMouseButtonDown(2));
#endif
    }

    private static bool IsPointerOverUi()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return false;

        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
