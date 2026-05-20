using System.Collections;
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event-driven algorithm "presence" on the Work Dashboard: dimming, approve nudge, patience messages.
/// No Update loop — coroutines start/stop when posts change.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkDashboardAlgorithmUI : MonoBehaviour
{
    [Header("Post panel")]
    [SerializeField] private CanvasGroup postPanelCanvasGroup;
    [SerializeField] private TMP_Text postBodyText;
    [SerializeField] private TMP_Text engagementRowText;

    [Header("Decision buttons")]
    [SerializeField] private Button approveButton;
    [SerializeField] private LayoutElement approveButtonLayout;

    [Header("Patience")]
    [SerializeField] private float patienceCheckInterval = 2f;

    private Coroutine _patienceRoutine;
    private Coroutine _nudgeRoutine;
    private string _activePostId;

    private void Reset() => AutoBind();

    private void Awake() => AutoBind();

    /// <summary>Called when a new moderation item is shown.</summary>
    public void OnPostDisplayed(PostData post)
    {
        StopAllCoroutines();
        _patienceRoutine = null;
        _nudgeRoutine = null;
        _activePostId = post?.id;

        ResetPanelAlpha();
        ResetApproveScale();

        if (post == null) return;

        ApplySubtleBias(post);
        _patienceRoutine = StartCoroutine(PatienceWatch(post.id));
    }

    /// <summary>Called after a decision is recorded.</summary>
    public void OnPostClosed()
    {
        StopAllCoroutines();
        _patienceRoutine = null;
        _nudgeRoutine = null;
        _activePostId = null;
        ResetPanelAlpha();
        ResetApproveScale();
    }

    /// <summary>Call after algorithm rewrites text so attentive players may notice.</summary>
    public void OnPostTextAltered(TMP_Text target, bool rewrite)
    {
        if (target == null) target = postBodyText;
        AlgorithmGlitchHighlight.FlashTmpAfterLayout(target, rewrite, frameDelay: 1);
        if (engagementRowText != null && !rewrite)
            AlgorithmGlitchHighlight.FlashTmpAfterLayout(engagementRowText, false, frameDelay: 1);
    }

    private void ApplySubtleBias(PostData post)
    {
        float trust = AlgorithmManager.Instance != null ? AlgorithmManager.Instance.AlgorithmTrust : 50f;
        int day = GameDatabase.Instance?.Config != null ? GameDatabase.Instance.Config.currentDay : 1;

        bool heavy = post.category == PostCategory.Misinformation
            || post.category == PostCategory.Narrative
            || post.severity >= 2;

        bool emotional = post.category == PostCategory.GrayArea
            && (post.text != null && (post.text.Contains("cry") || post.text.Contains("can't")));

        if (postPanelCanvasGroup != null)
        {
            if (heavy && day >= 3 && trust < 70f)
                postPanelCanvasGroup.alpha = 0.82f;
            else if (emotional && trust < 45f)
                postPanelCanvasGroup.alpha = 0.9f;
        }

        if (approveButton != null && trust < 40f && day >= 5 && heavy)
            _nudgeRoutine = StartCoroutine(NudgeApproveButton());
    }

    private IEnumerator PatienceWatch(string postId)
    {
        float hesitation = 12f;
        if (AlgorithmManager.Instance != null)
        {
            var bootstrap = FindFirstObjectByType<GlitchInTheSystem.GameData.GameBootstrap>();
            var settings = bootstrap != null ? bootstrap.AlgorithmTrustSettings : null;
            if (settings != null)
                hesitation = settings.hesitationSeconds;
        }

        float elapsed = 0f;
        bool warned = false;

        while (_activePostId == postId)
        {
            yield return new WaitForSecondsRealtime(patienceCheckInterval);
            elapsed += patienceCheckInterval;

            if (warned || elapsed < hesitation) continue;

            warned = true;
            AlgorithmManager.Instance?.NotifyPlayerHesitation();
            AlgorithmNotification.Instance?.Show(AlgorithmVoice.PatienceNudge(postId, elapsed), 2.8f);
        }
    }

    private IEnumerator NudgeApproveButton()
    {
        if (approveButton == null) yield break;

        var rt = approveButton.transform as RectTransform;
        if (rt == null) yield break;

        Vector3 baseScale = Vector3.one;
        for (int i = 0; i < 3; i++)
        {
            rt.localScale = baseScale * 1.06f;
            yield return new WaitForSecondsRealtime(0.12f);
            rt.localScale = baseScale;
            yield return new WaitForSecondsRealtime(0.18f);
        }
    }

    private void ResetPanelAlpha()
    {
        if (postPanelCanvasGroup != null)
            postPanelCanvasGroup.alpha = 1f;
    }

    private void ResetApproveScale()
    {
        if (approveButton != null)
            approveButton.transform.localScale = Vector3.one;
    }

    private void AutoBind()
    {
        if (postPanelCanvasGroup == null)
        {
            var panel = transform.Find("FloatingPanel/Body/RightPanel");
            if (panel != null)
                postPanelCanvasGroup = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        }

        postBodyText ??= FindTmp("PostText");
        engagementRowText ??= FindTmp("EngagementRow");
        approveButton ??= FindButton("ApproveButton");
        if (approveButton != null)
            approveButtonLayout ??= approveButton.GetComponent<LayoutElement>();
    }

    private TMP_Text FindTmp(string name)
    {
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            if (t.name == name) return t;
        return null;
    }

    private Button FindButton(string name)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
        return null;
    }
}
