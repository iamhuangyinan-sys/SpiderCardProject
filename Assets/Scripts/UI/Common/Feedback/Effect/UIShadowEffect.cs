using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct UIShadowStyle
{
    public Color color;
    [Tooltip("有参考对象时相对参考对象偏移；否则是阴影节点的 anchoredPosition")]
    public Vector2 offset;
    [Tooltip("相对阴影初始 sizeDelta 的增量，Canvas 单位")]
    public Vector2 extraSize;

    public UIShadowStyle(float alpha, Vector2 offset, Vector2 extraSize)
    {
        color = new Color(0f, 0f, 0f, alpha);
        this.offset = offset;
        this.extraSize = extraSize;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Shadow Image")]
public sealed class UIShadowEffect : UIFeedbackEffect
{
    [SerializeField, Tooltip("独立的阴影 Image，不使用原生 Shadow；Raycast Target 应关闭")]
    private Image target = null;
    [SerializeField, Tooltip("可选：阴影对应的物体。指定后跟随其位置和尺寸，再叠加各状态的 Offset / Extra Size；留空沿用原有方式")]
    private RectTransform referenceTarget = null;
    [SerializeField, Tooltip("不可交互时也允许悬停显示阴影")]
    private bool hoverWhenDisabled = false;
    [SerializeField] private UIShadowStyle normal = new UIShadowStyle(0.35f, new Vector2(4f, -4f), Vector2.zero);
    [SerializeField] private UIShadowStyle hovered = new UIShadowStyle(0.4f, new Vector2(5f, -7f), new Vector2(2f, 2f));
    [SerializeField] private UIShadowStyle pressed = new UIShadowStyle(0.2f, new Vector2(1f, -1f), Vector2.zero);
    [SerializeField] private UIShadowStyle focused = new UIShadowStyle(0.35f, new Vector2(4f, -4f), Vector2.zero);
    [SerializeField] private UIShadowStyle disabled = new UIShadowStyle(0.12f, new Vector2(2f, -2f), Vector2.zero);

    private Image capturedTarget;
    private Color baseColor;
    private Vector2 basePosition;
    private Vector2 baseSize;
    private bool followingReference;
    private Vector2 currentOffset;
    private Vector2 currentExtraSize;
    private readonly Vector3[] referenceCorners = new Vector3[4];

    protected override bool Capture()
    {
        capturedTarget = target;
        if (capturedTarget == null) return false;
        baseColor = capturedTarget.color;
        basePosition = capturedTarget.rectTransform.anchoredPosition;
        baseSize = capturedTarget.rectTransform.sizeDelta;
        currentOffset = normal.offset;
        currentExtraSize = normal.extraSize;
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
        if (context.State == E_UIFeedbackState.Disabled && hoverWhenDisabled && context.IsPointerHovered)
            style = hovered;
        var color = capturedTarget.sprite != null ? style.color : Color.clear;
        var rect = capturedTarget.rectTransform;
        followingReference = referenceTarget != null && referenceTarget != rect &&
            !referenceTarget.IsChildOf(rect) && rect.parent is RectTransform;
        if (followingReference)
        {
            if (immediate)
            {
                capturedTarget.color = color;
                currentOffset = style.offset;
                currentExtraSize = style.extraSize;
            }
            else
            {
                var followSequence = DOTween.Sequence();
                followSequence.Join(capturedTarget.DOColor(color, Duration).SetEase(TransitionEase));
                followSequence.Join(DOTween.To(() => currentOffset, v => currentOffset = v,
                    style.offset, Duration).SetEase(TransitionEase));
                followSequence.Join(DOTween.To(() => currentExtraSize, v => currentExtraSize = v,
                    style.extraSize, Duration).SetEase(TransitionEase));
                Play(followSequence, context);
            }
            UpdateReferenceLayout();
            return;
        }
        if (immediate)
        {
            capturedTarget.color = color;
            rect.anchoredPosition = style.offset;
            rect.sizeDelta = baseSize + style.extraSize;
            return;
        }
        var sequence = DOTween.Sequence();
        sequence.Join(capturedTarget.DOColor(color, Duration).SetEase(TransitionEase));
        sequence.Join(rect.DOAnchorPos(style.offset, Duration).SetEase(TransitionEase));
        sequence.Join(rect.DOSizeDelta(baseSize + style.extraSize, Duration).SetEase(TransitionEase));
        Play(sequence, context);
    }

    protected override void RestoreValues()
    {
        followingReference = false;
        if (capturedTarget == null) return;
        capturedTarget.color = baseColor;
        capturedTarget.rectTransform.anchoredPosition = basePosition;
        capturedTarget.rectTransform.sizeDelta = baseSize;
    }

    private void LateUpdate()
    {
        if (followingReference) UpdateReferenceLayout();
    }

    private void UpdateReferenceLayout()
    {
        if (capturedTarget == null || referenceTarget == null) return;
        var rect = capturedTarget.rectTransform;
        if (!(rect.parent is RectTransform parent)) return;
        // 在阴影父节点坐标系内计算，排除共同祖先缩放，避免悬停缩放被重复计算。
        referenceTarget.GetWorldCorners(referenceCorners);
        Vector2 min = parent.InverseTransformPoint(referenceCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 point = parent.InverseTransformPoint(referenceCorners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        Vector2 anchor = rect.anchorMin + Vector2.Scale(rect.anchorMax - rect.anchorMin, rect.pivot);
        Vector2 anchorPosition = parent.rect.min + Vector2.Scale(parent.rect.size, anchor);
        rect.anchoredPosition = min + Vector2.Scale(max - min + currentExtraSize, rect.pivot)
            - currentExtraSize * 0.5f - anchorPosition + currentOffset;
        rect.sizeDelta = max - min - Vector2.Scale(parent.rect.size, rect.anchorMax - rect.anchorMin)
            + currentExtraSize;
    }

    public override void CollectBindings(List<UIFeedbackBinding> bindings)
    {
        if (target == null) return;
        bindings.Add(new UIFeedbackBinding(target, "m_Color"));
        bindings.Add(new UIFeedbackBinding(target.rectTransform, "m_AnchoredPosition"));
        bindings.Add(new UIFeedbackBinding(target.rectTransform, "m_SizeDelta"));
    }
}
