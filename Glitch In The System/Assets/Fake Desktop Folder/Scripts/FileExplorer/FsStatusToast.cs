using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lightweight auto-hiding status toast for File Explorer.
/// Attach to FileExplorerAppWindow. Call Show("message") from anywhere.
/// Builds its own UI on first call — no prefab needed.
/// </summary>
public sealed class FsStatusToast : MonoBehaviour
{
    private GameObject    _toast;
    private TextMeshProUGUI _label;
    private CanvasGroup   _cg;
    private Coroutine     _routine;

    public void Show(string message, float duration = 2.2f)
    {
        EnsureBuilt();
        _label.text = message;
        _toast.SetActive(true);
        _cg.alpha = 1f;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeOut(duration));
    }

    private IEnumerator FadeOut(float hold)
    {
        yield return new WaitForSecondsRealtime(hold);
        float t = 0f;
        const float fadeTime = 0.35f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }
        _toast.SetActive(false);
        _routine = null;
    }

    private void EnsureBuilt()
    {
        if (_toast != null) return;

        _toast = new GameObject("FsToast",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup));
        _toast.transform.SetParent(transform, false);

        var rt = _toast.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(280f, 32f);
        rt.anchoredPosition = new Vector2(0f, 12f);

        _toast.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.94f);

        _cg = _toast.GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
        _cg.interactable   = false;

        var lblGO = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(_toast.transform, false);
        var lRT = lblGO.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = new Vector2(12f, 0f); lRT.offsetMax = new Vector2(-12f, 0f);
        _label = lblGO.GetComponent<TextMeshProUGUI>();
        _label.fontSize      = 12;
        _label.color         = new Color(0.85f, 0.83f, 0.80f, 1f);
        _label.alignment     = TextAlignmentOptions.Midline;
        _label.raycastTarget = false;

        _toast.SetActive(false);
    }
}
