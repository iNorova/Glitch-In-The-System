using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Interruptions
{
    /// <summary>
    /// Builds the centered loading spinner UI under FakeDesktop.
    /// </summary>
    public static class InterruptionLoadingSpinnerFactory
    {
        public const string RootName = "InterruptionLoading";
        public const string IconName = "SpinnerIcon";
        private const string SpinnerSpriteResourcePath = "UI/InterruptionSpinnerRing";

        public static GameObject Create(Transform fakeDesktopParent)
        {
            var root = new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            root.transform.SetParent(fakeDesktopParent, false);
            root.transform.SetAsLastSibling();
            Stretch(root.GetComponent<RectTransform>());

            var rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = true;

            var iconGo = CreateSpinnerIcon(root.transform);
            root.SetActive(false);
            return root;
        }

        public static GameObject CreateSpinnerIcon(Transform parent)
        {
            var iconGo = new GameObject(
                IconName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(InterruptionLoadingSpinner));
            iconGo.transform.SetParent(parent, false);

            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(64f, 64f);

            var image = iconGo.GetComponent<Image>();
            image.sprite = LoadSpinnerSprite();
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;

            return iconGo;
        }

        public static Sprite LoadSpinnerSprite()
        {
            var sprite = Resources.Load<Sprite>(SpinnerSpriteResourcePath);
            if (sprite != null)
                return sprite;

#if UNITY_EDITOR
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/UI/InterruptionSpinnerRing.png");
            if (sprite != null)
                return sprite;
#endif

            return null;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
