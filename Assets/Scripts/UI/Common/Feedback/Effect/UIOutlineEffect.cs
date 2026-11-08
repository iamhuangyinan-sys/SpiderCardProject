using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[Serializable]
public struct UIOutlineStyle
{
    public Color color;
    [FormerlySerializedAs("distance"), Tooltip("每侧向外扩展的 Canvas 单位")]
    public Vector2 padding;
    public Vector2 offset;

    public UIOutlineStyle(Color color, Vector2 distance)
    {
        this.color = color;
        padding = distance;
        offset = Vector2.zero;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Outline")]
public sealed class UIOutlineEffect : UIFeedbackEffect
{
    [SerializeField, Tooltip("独立轮廓 Image；请设置透明背景的轮廓 Sprite，留空时不显示")]
    private Image target = null;
    [SerializeField] private UIOutlineStyle normal = new UIOutlineStyle(Color.clear, new Vector2(2f, 2f));
    [SerializeField] private UIOutlineStyle hovered = new UIOutlineStyle(new Color(1f, 0.84f, 0.3f), new Vector2(2f, 2f));
    [SerializeField] private UIOutlineStyle pressed = new UIOutlineStyle(new Color(1f, 0.84f, 0.3f, 0.5f), Vector2.one);
    [SerializeField] private UIOutlineStyle focused = new UIOutlineStyle(new Color(1f, 0.84f, 0.3f), new Vector2(2f, 2f));
    [SerializeField] private UIOutlineStyle disabled = new UIOutlineStyle(Color.clear, Vector2.one);

    private Image capturedTarget;
    private Color baseColor;
    private Vector2 baseSize;
    private Vector2 basePosition;

    protected override bool Capture()
    {
        capturedTarget = target;
        if (capturedTarget == null) return false;
        baseColor = capturedTarget.color;
        baseSize = capturedTarget.rectTransform.sizeDelta;
        basePosition = capturedTarget.rectTransform.anchoredPosition;
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
        // 没有轮廓图时保持透明，避免 Image 默认绘制白色矩形。
        var color = capturedTarget.sprite != null ? style.color : Color.clear;
        var rect = capturedTarget.rectTransform;
        var size = baseSize + style.padding * 2f;
        var position = basePosition + style.offset;
        if (immediate)
        {
            capturedTarget.color = color;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return;
        }
        var sequence = DOTween.Sequence();
        sequence.Join(capturedTarget.DOColor(color, Duration).SetEase(TransitionEase));
        sequence.Join(rect.DOSizeDelta(size, Duration).SetEase(TransitionEase));
        sequence.Join(rect.DOAnchorPos(position, Duration).SetEase(TransitionEase));
        Play(sequence, context);
    }

    protected override void RestoreValues()
    {
        if (capturedTarget == null) return;
        capturedTarget.color = baseColor;
        capturedTarget.rectTransform.sizeDelta = baseSize;
        capturedTarget.rectTransform.anchoredPosition = basePosition;
    }

    public override void CollectBindings(List<UIFeedbackBinding> bindings)
    {
        if (target == null) return;
        bindings.Add(new UIFeedbackBinding(target, "m_Color"));
        bindings.Add(new UIFeedbackBinding(target.rectTransform, "m_SizeDelta"));
        bindings.Add(new UIFeedbackBinding(target.rectTransform, "m_AnchoredPosition"));
    }
}
