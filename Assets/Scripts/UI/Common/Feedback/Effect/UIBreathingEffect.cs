using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Breathing")]
public sealed class UIBreathingEffect : UIFeedbackEffect
{
    [SerializeField, Tooltip("独立的 BreatheRoot，不要指向 UIScaleEffect 的目标")]
    private RectTransform target = null;
    [SerializeField, Min(0.01f)] private float minScale = 0.98f;
    [SerializeField, Min(0.01f)] private float maxScale = 1.02f;
    [SerializeField, Min(0.01f), Tooltip("从谷值到峰值的时间；完整周期为两倍")]
    private float halfCycle = 1f;

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
        if (!context.CanBreathe)
        {
            if (immediate) capturedTarget.localScale = baseScale;
            else Play(capturedTarget.DOScale(baseScale, Duration).SetEase(TransitionEase), context);
            return;
        }

        var peak = baseScale * Mathf.Max(minScale, maxScale);
        var trough = baseScale * Mathf.Min(minScale, maxScale);
        if (!Application.isPlaying)
        {
            capturedTarget.localScale = peak; // 编辑器只预览峰值，不启动无限动画。
            return;
        }

        float seconds = Mathf.Max(0.01f, halfCycle);
        // 先平滑到谷值，再播放独立的无限 Yoyo。避免 Sequence 重启时跳回首次起点。
        Play(capturedTarget.DOScale(trough, Mathf.Max(0.01f, Duration))
            .SetEase(TransitionEase).OnComplete(() =>
            {
                if (capturedTarget == null) return;
                Play(capturedTarget.DOScale(peak, seconds)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo), context);
            }), context);
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
