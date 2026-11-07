using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Scale")]
public sealed class UIScaleEffect : UIFeedbackEffect
{
    [Header("目标（与呼吸使用不同节点）")]
    [SerializeField] private RectTransform target = null;
    [SerializeField, Tooltip("不可交互时，鼠标移入仍使用 Hovered 倍率；不会启用点击或呼吸")]
    private bool hoverWhenDisabled = false;
    [Header("相对初始缩放的倍率")]
    [SerializeField, Min(0f)] private float normal = 1f;
    [SerializeField, Min(0f)] private float hovered = 1.04f;
    [SerializeField, Min(0f)] private float pressed = 0.97f;
    [SerializeField, Min(0f)] private float focused = 1f;
    [SerializeField, Min(0f)] private float disabled = 1f;

    private RectTransform capturedTarget;
    private Vector3 baseScale;

    protected override bool Capture()
    {
        capturedTarget = target;
        if (capturedTarget == null) return false;
        baseScale = capturedTarget.localScale;
        return true;
    }

    protected override void ApplyState(UIFeedbackContext context, bool immediate)
    {
        if (capturedTarget == null) return;
        float factor = context.State switch
        {
            E_UIFeedbackState.Hovered => hovered,
            E_UIFeedbackState.Pressed => pressed,
            E_UIFeedbackState.Focused => focused,
            E_UIFeedbackState.Disabled => disabled,
            _ => normal
        };
        if (context.State == E_UIFeedbackState.Disabled && hoverWhenDisabled && context.IsPointerHovered)
            factor = hovered;
        var value = baseScale * Mathf.Max(0f, factor);
        if (immediate) capturedTarget.localScale = value;
        else Play(capturedTarget.DOScale(value, Duration).SetEase(TransitionEase), context);
    }

    protected override void RestoreValues()
    {
        if (capturedTarget != null) capturedTarget.localScale = baseScale;
    }

    public override void CollectBindings(List<UIFeedbackBinding> bindings)
    {
        if (target != null) bindings.Add(new UIFeedbackBinding(target, "m_LocalScale"));
    }
}
