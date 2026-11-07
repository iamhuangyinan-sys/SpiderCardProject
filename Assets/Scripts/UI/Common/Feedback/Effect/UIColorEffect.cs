using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Feedback/Effects/Color")]
public sealed class UIColorEffect : UIFeedbackEffect
{
    [SerializeField] private Graphic target = null;
    [Header("与 Graphic 初始颜色相乘（白色表示保留原色）")]
    [SerializeField] private Color normal = Color.white;
    [SerializeField] private Color hovered = Color.white;
    [SerializeField] private Color pressed = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color focused = Color.white;
    [SerializeField] private Color disabled = new Color(0.65f, 0.65f, 0.65f, 0.65f);

    private Graphic capturedTarget;
    private Color baseColor;

    protected override bool Capture()
    {
        capturedTarget = target;
        if (capturedTarget == null) return false;
        baseColor = capturedTarget.color;
        return true;
    }

    protected override void ApplyState(UIFeedbackContext context, bool immediate)
    {
        if (capturedTarget == null) return;
        var tint = context.State switch
        {
            E_UIFeedbackState.Hovered => hovered,
            E_UIFeedbackState.Pressed => pressed,
            E_UIFeedbackState.Focused => focused,
            E_UIFeedbackState.Disabled => disabled,
            _ => normal
        };
        var value = baseColor * tint;
        if (immediate) capturedTarget.color = value;
        else Play(capturedTarget.DOColor(value, Duration).SetEase(TransitionEase), context);
    }

    protected override void RestoreValues()
    {
        if (capturedTarget != null) capturedTarget.color = baseColor;
    }

    public override void CollectBindings(List<UIFeedbackBinding> bindings)
    {
        if (target != null) bindings.Add(new UIFeedbackBinding(target, "m_Color"));
    }
}
