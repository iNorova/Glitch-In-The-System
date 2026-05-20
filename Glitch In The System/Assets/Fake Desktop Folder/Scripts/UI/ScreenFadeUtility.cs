using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.UI
{
    /// <summary>Shared full-screen fade helpers for intro handoffs and in-game day shifts.</summary>
    public static class ScreenFadeUtility
    {
        public static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (go == null) return null;
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        public static void ApplyFullBleed(RectTransform rt, Image background = null)
        {
            if (rt == null) return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.SetAsLastSibling();

            if (background != null)
            {
                background.color = Color.black;
                background.raycastTarget = true;
            }
        }

        public static IEnumerator Fade(CanvasGroup group, float endAlpha, float duration)
        {
            if (group == null)
                yield break;

            float start = group.alpha;
            endAlpha = Mathf.Clamp01(endAlpha);
            if (duration <= 0f)
            {
                group.alpha = endAlpha;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, endAlpha, Mathf.Clamp01(t / duration));
                yield return null;
            }

            group.alpha = endAlpha;
        }
    }
}
