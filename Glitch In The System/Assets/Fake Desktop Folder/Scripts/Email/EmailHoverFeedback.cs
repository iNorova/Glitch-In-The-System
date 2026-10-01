using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Batch UI-8 — lightweight hover/pressed feedback for Email app navigation controls
/// (sidebar buttons, category chips, Compose button, Refresh button).
///
/// Follows the same pattern already used by FileExplorer/SidebarFolderButton.cs:
/// OnPointerExit/OnPointerUp restore to the button's CURRENT correct resting color
/// (via GetBaseColor callback) rather than a hardcoded value — so active-state systems
/// like EmailApp.SetActiveSidebarButton / ApplyChipVisual remain the single source of
/// truth for the resting color, and this component only layers hover/press feedback
/// on top without fighting them.
///
/// No animation — instant color swap, per Batch UI-8 scope ("DO NOT ADD ANIMATIONS YET").
/// </summary>
public sealed class EmailHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    private Image          _target;
    private Func<Color>    _getBaseColor;
    private float          _hoverBrighten;
    private float          _pressDarken;
    private bool           _pointerDown;

    /// <summary>
    /// Wire this handler onto a button's background Image.
    /// getBaseColor: returns the button's current "resting" color (active or inactive) —
    /// called on pointer-exit/up so this component never overrides active-state logic.
    /// hoverBrighten/pressDarken: multiplicative RGB shift (alpha always preserved).
    /// </summary>
    public void Init(Image target, Func<Color> getBaseColor, float hoverBrighten = 1.18f, float pressDarken = 0.80f)
    {
        _target        = target;
        _getBaseColor  = getBaseColor;
        _hoverBrighten = hoverBrighten;
        _pressDarken   = pressDarken;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_pointerDown) return; // pressed state takes priority while held
        ApplyTint(_hoverBrighten);
    }

    public void OnPointerExit(PointerEventData e)
    {
        _pointerDown = false;
        RestoreBase();
    }

    public void OnPointerDown(PointerEventData e)
    {
        _pointerDown = true;
        ApplyTint(_pressDarken);
    }

    public void OnPointerUp(PointerEventData e)
    {
        _pointerDown = false;
        // If pointer is still over the button on release, show hover; otherwise base.
        // RectTransformUtility check keeps this consistent with Unity's own Button behaviour.
        var rt = _target != null ? _target.rectTransform : null;
        bool stillOver = rt != null && RectTransformUtility.RectangleContainsScreenPoint(
            rt, e.position, e.pressEventCamera);
        ApplyTint(stillOver ? _hoverBrighten : 1f);
        if (!stillOver) RestoreBase();
    }

    private void ApplyTint(float factor)
    {
        if (_target == null || _getBaseColor == null) return;
        var b = _getBaseColor();
        // Alpha is preserved exactly — RGB-only shift so fully transparent (alpha=0) bases
        // become a faint VISIBLE tint rather than staying invisible (alpha=0 * factor = 0
        // would be the bug if alpha were also scaled, so alpha gets a small additive floor
        // instead when the base is fully transparent).
        float a = b.a;
        if (a <= 0.001f) a = factor > 1f ? 0.10f : 0.05f; // give transparent bases a visible floor
        _target.color = new Color(
            Mathf.Clamp01(b.r * factor),
            Mathf.Clamp01(b.g * factor),
            Mathf.Clamp01(b.b * factor),
            a);
    }

    private void RestoreBase()
    {
        if (_target == null || _getBaseColor == null) return;
        _target.color = _getBaseColor();
    }
}
