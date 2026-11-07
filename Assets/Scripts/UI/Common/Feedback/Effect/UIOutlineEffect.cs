using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct UIOutlineStyle
{
    public Color color;
    public Vector2 distance;

    public UIOutlineStyle(Color color, Vector2 distance)
    {
        this.color = color;
        this.distance = distance;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Outline")]
public sealed class UIOutlineEffect : UIFeedbackEffect
{
    [SerializeField, Tooltip("背景 Graphic 上的原生 Outline；保持启用，通过颜色 Alpha 控制显隐")]
    private Outline target = null;
    [SerializeField] private UIOutlineStyle normal = new UIOutlineStyle(Color.clear, new Vector2(2f, 2f));
    [SerializeField] private UIOutlineStyle hovered = new UIOutlineStyle(new Color(1f, 0.84f, 0.3f), new Vector2(2f, 2f));
    [SerializeField] private UIOutlineStyle pressed = new UIOutlineStyle(new Color(1f, 0.84f, 0.3f, 0.5f), Vector2.one);
    [SerializeField] private UIOutlineStyle focused = new UIOutlineStyle(new Color(1f, 0.84f, 0.3f), new Vector2(2f, 2f));
    [SerializeField] private UIOutlineStyle disabled = new UIOutlineStyle(Color.clear, Vector2.one);

    private Outline capturedTarget;
    private Color baseColor;
    private Vector2 baseDistance;

    protected override bool Capture()
    {
        capturedTarget = target;
        if (capturedTarget == null) return false;
        baseColor = capturedTarget.effectColor;
        baseDistance = capturedTarget.effectDistance;
        return true;
    }

    protected override void ApplyState(UIFeedbackContext context, bool immediate)
    {
        if (capturedTarget == null) return;
        var style = context.State switch
        {
            E_UIFeedbackState.Hovered => hovered,
            E_UIFeedbackState.Pressed => pressed,
            E_UIFeedbackState.Focused => focused,
            E_UIFeedbackState.Disabled => disabled,
            _ => normal
        };
        if (immediate)
        {
            capturedTarget.effectColor = style.color;
            capturedTarget.effectDistance = style.distance;
            return;
        }
        var sequence = DOTween.Sequence();
        sequence.Join(DOTween.To(() => capturedTarget.effectColor, v => capturedTarget.effectColor = v, style.color, Duration).SetEase(TransitionEase));
        sequence.Join(DOTween.To(() => capturedTarget.effectDistance, v => capturedTarget.effectDistance = v, style.distance, Duration).SetEase(TransitionEase));
        Play(sequence, context);
    }

    protected override void RestoreValues()
    {
        if (capturedTarget == null) return;
        capturedTarget.effectColor = baseColor;
        capturedTarget.effectDistance = baseDistance;
    }

    public override void CollectBindings(List<UIFeedbackBinding> bindings)
    {
        if (target == null) return;
        bindings.Add(new UIFeedbackBinding(target, "m_EffectColor"));
        bindings.Add(new UIFeedbackBinding(target, "m_EffectDistance"));
    }
}
