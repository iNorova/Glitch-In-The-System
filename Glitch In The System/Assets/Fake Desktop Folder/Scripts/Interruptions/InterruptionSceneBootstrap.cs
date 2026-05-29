using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Recreates <c>InterruptionSystems</c> and wires references if missing (e.g. after scene reload).
    /// Runs even when <c>InterruptionOverlay</c> starts disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterruptionSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private ErrorPopup popupPrefab;

        private bool _systemsEnsured;

        private void Awake()
        {
            EnsureSystemsOnce();
        }

        private void EnsureSystemsOnce()
        {
            if (_systemsEnsured) return;
            _systemsEnsured = true;
            EnsureSystems();
        }

        private void EnsureSystems()
        {
            var overlayRoot = gameObject;
            var overlayTransform = overlayRoot.transform;

            Transform popupContainer = overlayTransform.Find("PopupContainer");
            Transform blocker = overlayTransform.Find("BlockerPanel");
            Transform minigameRoot = overlayTransform.Find("MinigameRoot");

            if (popupContainer == null || blocker == null)
            {
                Debug.LogError("[InterruptionSceneBootstrap] PopupContainer or BlockerPanel missing under InterruptionOverlay.");
                return;
            }

            EnsureFullStretch((RectTransform)popupContainer);
            EnsureFullStretch((RectTransform)blocker);

            // Scene copy should not live here — only runtime spawns from prefab.
            var sceneCopy = overlayTransform.Find("CriticalErrorUi");
            if (sceneCopy != null)
                sceneCopy.gameObject.SetActive(false);

            if (popupPrefab == null)
                popupPrefab = LoadDefaultPopupPrefab();

            var manager = FindFirstObjectByType<InterruptionManager>();
            MinigameManager minigameManager = FindFirstObjectByType<MinigameManager>();

            if (manager == null)
            {
                var systemsGo = new GameObject("InterruptionSystems");
                minigameManager = systemsGo.AddComponent<MinigameManager>();
                manager = systemsGo.AddComponent<InterruptionManager>();

                if (systemsGo.GetComponent<AudioSource>() == null)
                    systemsGo.AddComponent<AudioSource>();

                WireManager(manager, minigameManager, overlayRoot, popupContainer, blocker, minigameRoot);
                Debug.Log("[InterruptionSceneBootstrap] Created InterruptionSystems and wired references.");
            }
            else if (minigameManager == null)
            {
                minigameManager = manager.gameObject.AddComponent<MinigameManager>();
                WireManager(manager, minigameManager, overlayRoot, popupContainer, blocker, minigameRoot);
            }
            else
            {
                WireManager(manager, minigameManager, overlayRoot, popupContainer, blocker, minigameRoot);
            }

            if (popupPrefab != null)
                manager.SetPopupPrefab(popupPrefab);

            var blockerScript = blocker.GetComponent<InterruptionInputBlocker>();
            if (blockerScript == null)
                blockerScript = blocker.gameObject.AddComponent<InterruptionInputBlocker>();

            blockerScript.SetManager(manager);

            overlayRoot.SetActive(false);

            var captchaPanel = minigameRoot != null ? minigameRoot.Find("CaptchaMinigamePanel") : null;
            if (captchaPanel != null)
                captchaPanel.gameObject.SetActive(false);
        }

        private static void WireManager(
            InterruptionManager manager,
            MinigameManager minigameManager,
            GameObject overlayRoot,
            Transform popupContainer,
            Transform blocker,
            Transform minigameRoot)
        {
            var workDashboard = FindFirstObjectByType<WorkDashboardController>();
            var socialFeed = FindFirstObjectByType<SocialMediaFeedController>();
            var blockerImage = blocker.GetComponent<Image>();

            CaptchaMinigame captcha = null;
            if (minigameRoot != null)
            {
                var captchaPanel = minigameRoot.Find("CaptchaMinigamePanel");
                if (captchaPanel != null)
                {
                    captcha = captchaPanel.GetComponent<CaptchaMinigame>();
                    if (captcha == null)
                        captcha = captchaPanel.gameObject.AddComponent<CaptchaMinigame>();
                }
            }

            minigameManager.Configure(captcha);
            manager.Configure(
                overlayRoot,
                (RectTransform)popupContainer,
                minigameManager,
                workDashboard,
                socialFeed,
                blockerImage);
        }

        private static void EnsureFullStretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static ErrorPopup LoadDefaultPopupPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ErrorPopup>(
                "Assets/Error/CriticalErrorUi 1.prefab");
#else
            return null;
#endif
        }
    }
}
