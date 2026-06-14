using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Batch 6 — Clickable breadcrumb path bar for File Explorer.
/// Attach to the PathBar GameObject (done by FileExplorerApp.EnsureBreadcrumb).
/// Call Rebuild(currentPath, navigateTo) from FileExplorerApp on every navigation.
///
/// FIX LOG:
///   Bug 1: EnsureLayout() now sets ignoreLayout=true on pre-existing PathLabel/__BorderLeft
///          children so the HLG never displaces them.
///   Bug 2: Removed all SetSiblingIndex() calls from Rebuild(). Pool children are appended
///          in correct alternating order (seg, sep, seg, sep…) at creation time and never
///          reordered. Rebuild() only toggles SetActive — zero hierarchy mutations.
///   Bug 3: HLG now uses childControlWidth=true so it correctly reads LE.preferredWidth.
///   Bug 4: ContentSizeFitter removed from BCSeg — it returned 0 without a LayoutGroup.
///          Width is now set directly: LE.preferredWidth = lbl.preferredWidth + 10f.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class FsBreadcrumb : MonoBehaviour
{
    // ── Colors ─────────────────────────────────────────────────────────────
    private static readonly Color SegmentNormal  = new Color(0.72f, 0.70f, 0.67f, 1f);
    private static readonly Color SegmentHover   = new Color(0.95f, 0.93f, 0.90f, 1f);
    private static readonly Color SegmentCurrent = new Color(0.90f, 0.88f, 0.84f, 1f);
    private static readonly Color SeparatorColor = new Color(0.45f, 0.44f, 0.42f, 1f);

    // ── Pool ───────────────────────────────────────────────────────────────
    // Alternating order in the hierarchy: seg0, sep0, seg1, sep1, …, segN
    // Pool grows, never shrinks — unused entries are hidden via SetActive(false).
    // Order is established at creation time and NEVER changed again (no SetSiblingIndex).
    private readonly List<(TextMeshProUGUI label, Button btn, BreadcrumbHover hover)> _segments = new(8);
    private readonly List<TextMeshProUGUI> _seps = new(8);

    private HorizontalLayoutGroup _hlg;

    // ── Init ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        EnsureLayout();

        // Hide the original PathLabel — breadcrumb takes over display.
        // Also stamp ignoreLayout so HLG never repositions pre-existing children.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            // Mark ALL pre-existing children as layout-ignored (belt+suspenders for __BorderLeft)
            var le = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            if (child.name == "PathLabel")
                child.gameObject.SetActive(false);
        }
    }

    private void EnsureLayout()
    {
        _hlg = GetComponent<HorizontalLayoutGroup>();
        if (_hlg != null) return;

        _hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        _hlg.padding               = new RectOffset(10, 10, 0, 0);
        _hlg.spacing               = 2f;
        _hlg.childAlignment        = TextAnchor.MiddleLeft;
        _hlg.childControlWidth     = true;   // FIX Bug 3: must be true to read CSF output
        _hlg.childControlHeight    = false;
        _hlg.childForceExpandWidth  = false;
        _hlg.childForceExpandHeight = true;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuild breadcrumb from a virtual path string like "/Desktop/Screenshots".
    /// navigateTo is called with the target fullPath when a segment is clicked.
    /// No hierarchy mutations — only text/color/interactable/SetActive changes.
    /// </summary>
    public void Rebuild(string fullPath, Action<string> navigateTo)
    {
        // Parse path into segments
        var parts = new List<(string label, string path)>();
        parts.Add(("File Explorer", ""));

        if (!string.IsNullOrEmpty(fullPath))
        {
            string[] segs = fullPath.Trim('/').Split('/');
            string cumulative = "";
            foreach (var seg in segs)
            {
                if (string.IsNullOrEmpty(seg)) continue;
                cumulative += "/" + seg;
                parts.Add((seg, cumulative));
            }
        }

        // ── Grow pool if needed ─────────────────────────────────────────────
        // Segments and separators are appended in correct order:
        //   seg0, sep0, seg1, sep1, ..., segN
        // They are NEVER reordered after creation.
        while (_segments.Count < parts.Count)
        {
            // If we need a sep before this new segment (all except the first)
            if (_segments.Count > 0)
                _seps.Add(BuildSeparator());
            _segments.Add(BuildSegment());
        }
        // Grow seps pool if somehow behind (should not happen with the above, but be safe)
        while (_seps.Count < parts.Count - 1)
            _seps.Add(BuildSeparator());

        // ── Wire segments and separators (NO hierarchy changes) ─────────────
        for (int i = 0; i < parts.Count; i++)
        {
            var (lbl, btn, hover) = _segments[i];
            var part     = parts[i];
            bool isCurrent = (i == parts.Count - 1);

            lbl.text  = part.label;
            lbl.color = isCurrent ? SegmentCurrent : SegmentNormal;
            // Force TMP to recompute mesh + preferredWidth immediately so CSF reads
            // the correct width in the same frame — eliminates the 1-frame width pop.
            lbl.ForceMeshUpdate();
            // Option A fix: push measured TMP width directly into LayoutElement.
            // 10f = 5px left padding (anchoredPosition.x=5) + 5px right margin.
            // HLG reads LE.preferredWidth correctly — BCSep proves this pattern works.
            var segLE = lbl.transform.parent.GetComponent<LayoutElement>();
            if (segLE != null) segLE.preferredWidth = lbl.preferredWidth + 10f;

            hover.normalColor = isCurrent ? SegmentCurrent : SegmentNormal;

            btn.onClick.RemoveAllListeners();
            if (!isCurrent)
            {
                var targetPath = part.path;
                btn.onClick.AddListener(() => navigateTo(targetPath));
                btn.interactable = true;
            }
            else
            {
                btn.interactable = false;
            }

            lbl.gameObject.SetActive(true);

            if (i > 0)
                _seps[i - 1].gameObject.SetActive(true);
        }

        // ── Hide unused pool entries ────────────────────────────────────────
        for (int i = parts.Count; i < _segments.Count; i++)
            _segments[i].label.gameObject.SetActive(false);
        for (int i = parts.Count - 1; i < _seps.Count; i++)
            _seps[i].gameObject.SetActive(false);
    }

    // ── Builders ───────────────────────────────────────────────────────────
    private (TextMeshProUGUI label, Button btn, BreadcrumbHover hover) BuildSegment()
    {
        var go = new GameObject("BCSeg",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(LayoutElement),
            typeof(BreadcrumbHover));
        go.transform.SetParent(transform, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 22f;
        // preferredWidth is set per-frame in Rebuild() from lbl.preferredWidth + 10f.
        // ContentSizeFitter was removed — it returned 0 without a LayoutGroup on BCSeg.

        var img = go.GetComponent<Image>();
        img.color = Color.clear;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;

        var hover = go.GetComponent<BreadcrumbHover>();
        hover.normalColor = SegmentNormal;
        hover.hoverColor  = SegmentHover;

        // Label child — stretches the full segment GO
        var lblGO = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = lblGO.GetComponent<RectTransform>();
        // LEFT-anchored (NOT stretch) so TMP.GetPreferredValues() measures against
        // a fixed large width instead of BCSeg's own width.
        // This breaks the CSF→TMP→CSF circular dependency that caused multi-frame jitter.
        lblRT.anchorMin        = new Vector2(0f, 0.5f);
        lblRT.anchorMax        = new Vector2(0f, 0.5f);
        lblRT.pivot            = new Vector2(0f, 0.5f);
        lblRT.anchoredPosition = new Vector2(5f, 0f);   // 5px left padding
        lblRT.sizeDelta        = new Vector2(2000f, 22f); // wide enough to never clip

        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize         = 12;
        tmp.color            = SegmentNormal;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget    = false;
        tmp.enableAutoSizing = false;

        hover.label = tmp;

        return (tmp, btn, hover);
    }

    private TextMeshProUGUI BuildSeparator()
    {
        var go = new GameObject("BCSep",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(transform, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = 14f;
        le.preferredHeight = 22f;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = "\u203a";
        tmp.fontSize      = 12;
        tmp.color         = SeparatorColor;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return tmp;
    }
}

/// <summary>Hover tint for breadcrumb segment buttons.</summary>
internal sealed class BreadcrumbHover : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler
{
    internal Color normalColor;
    internal Color hoverColor;
    internal TextMeshProUGUI label;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        if (label != null && GetComponent<UnityEngine.UI.Button>().interactable)
            label.color = hoverColor;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
    {
        if (label != null)
            label.color = normalColor;
    }
}
