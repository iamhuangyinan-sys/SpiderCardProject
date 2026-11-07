using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Offset")]
public sealed class UIOffsetEffect : UIFeedbackEffect
{
    [SerializeField] private RectTransform target = null;
    [Header("相对初始 anchoredPosition 的偏移")]
    [SerializeField] private Vector2 normal = Vector2.zero;
    [SerializeField] private Vector2 hovered = new Vector2(0f, 2f);
    [SerializeField] private Vector2 pressed = new Vector2(0f, -3f);
    [SerializeField] private Vector2 focused = Vector2.zero;
    [SerializeField] private Vector2 disabled = Vector2.zero;

    private RectTransform capturedTarget;
    private Vector2 basePosition;

    protected override bool Capture()
    {
        capturedTarget = target;
        if (capturedTarget == null) return false;
        basePosition = capturedTarget.anchoredPosition;
        return true;
    }

    protected override void ApplyState(UIFeedbackContext context, bool immediate)
    {
        if (capturedTarget == null) return;
        var offset = context.State switch
        {
            E_UIFeedbackState.Hovered => hovered,
            E_UIFeedbackState.Pressed => pressed,
            E_UIFeedbackState.Focused => focused,
            E_UIFeedbackState.Disabled => disabled,
            _ => normal
        };
        var value = basePosition + offset;
        if (immediate) capturedTarget.anchoredPosition = value;
        else Play(capturedTarget.DOAnchorPos(value, Duration).SetEase(TransitionEase), context);
    }

    protected override void RestoreValues()
    {
        if (capturedTarget != null) capturedTarget.anchoredPosition = basePosition;
    }

    public override void CollectBindings(List<UIFeedbackBinding> bindings)
    {
        if (target != null) bindings.Add(new UIFeedbackBinding(target, "m_AnchoredPosition"));
    }
}
