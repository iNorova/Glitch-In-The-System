using System.Collections;
using System.Collections.Generic;
using GlitchInTheSystem.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.UI
{
    /// <summary>
    /// Brief corrupted-color flash on UI elements when the algorithm alters content (not a popup alert).
    /// </summary>
    public sealed class AlgorithmGlitchHighlight : MonoBehaviour
    {
        public static AlgorithmGlitchHighlight Instance { get; private set; }

        [SerializeField] private Color glitchColor = new(0.78f, 0.12f, 0.95f, 1f);
        [SerializeField] private Color rewriteColor = new(0.92f, 0.18f, 0.22f, 1f);
        [SerializeField] private float flashDuration = 0.7f;
        [SerializeField] private int pulseCount = 3;

        private readonly Dictionary<int, Coroutine> _activeFlashes = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RuntimePersistency.Adopt(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void FlashTmp(TMP_Text text, bool isRewrite = false)
        {
            if (text == null) return;
            EnsureInstance();
            Instance.StartFlash(text, isRewrite ? Instance.rewriteColor : Instance.glitchColor);
        }

        public static void FlashTmpAfterLayout(TMP_Text text, bool isRewrite = false, int frameDelay = 1)
        {
            if (text == null) return;
            EnsureInstance();
            Instance.StartCoroutine(Instance.FlashAfterFrames(text, isRewrite, frameDelay));
        }

        public static void FlashImage(Image image, bool isRewrite = false)
        {
            if (image == null) return;
            EnsureInstance();
            Instance.StartFlash(image, isRewrite ? Instance.rewriteColor : Instance.glitchColor);
        }

        private static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("AlgorithmGlitchHighlight");
            RuntimePersistency.Adopt(go);
            go.AddComponent<AlgorithmGlitchHighlight>();
        }

        private void StartFlash(Graphic graphic, Color corrupt)
        {
            int id = graphic.GetInstanceID();
            if (_activeFlashes.TryGetValue(id, out var running) && running != null)
                StopCoroutine(running);
            _activeFlashes[id] = StartCoroutine(PulseGraphic(graphic, corrupt, id));
        }

        private IEnumerator FlashAfterFrames(TMP_Text text, bool isRewrite, int frameDelay)
        {
            for (int i = 0; i < Mathf.Max(1, frameDelay); i++)
                yield return null;

            if (!IsGraphicAlive(text)) yield break;
            FlashTmp(text, isRewrite);
        }

        private IEnumerator PulseGraphic(Graphic graphic, Color corrupt, int id)
        {
            if (!IsGraphicAlive(graphic)) yield break;

            Color original = graphic.color;
            float half = flashDuration / (pulseCount * 2f);

            for (int i = 0; i < pulseCount; i++)
            {
                if (!IsGraphicAlive(graphic))
                    yield break;

                graphic.color = Color.Lerp(original, corrupt, 0.95f);
                SafeForceMeshUpdate(graphic);
                yield return new WaitForSecondsRealtime(half);

                if (!IsGraphicAlive(graphic))
                    yield break;

                graphic.color = original;
                SafeForceMeshUpdate(graphic);
                yield return new WaitForSecondsRealtime(half);
            }

            if (IsGraphicAlive(graphic))
                graphic.color = original;

            _activeFlashes.Remove(id);
        }

        private static bool IsGraphicAlive(Graphic graphic) =>
            graphic != null && graphic.gameObject != null;

        private static void SafeForceMeshUpdate(Graphic graphic)
        {
            if (graphic is not TMP_Text tmp || !IsGraphicAlive(tmp))
                return;

            try
            {
                tmp.ForceMeshUpdate();
            }
            catch (MissingReferenceException)
            {
                // Feed cards are destroyed on refresh while a flash coroutine is still running.
            }
        }
    }
}
