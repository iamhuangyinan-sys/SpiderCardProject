using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 效果只实现捕获原值、应用状态和还原，不接收输入。所有效果挂在 Controller 同节点。
/// 每个组件只管理自己创建的 Tween，不调用目标的 DOKill，避免取消其它属性的动画。
/// </summary>
[RequireComponent(typeof(UIFeedbackController))]
public abstract class UIFeedbackEffect : MonoBehaviour
{
    [Header("过渡")]
    [SerializeField, Min(0f)] private float duration = 0.12f;
    [SerializeField] private Ease ease = Ease.OutQuad;

    private Tween tween;
    private bool captured;

    protected float Duration => Mathf.Max(0f, duration);
    protected Ease TransitionEase => ease;

    protected virtual void OnEnable()
    {
        if (Application.isPlaying)
            GetComponent<UIFeedbackController>().RefreshEffects(true);
    }

    protected virtual void OnDisable() => Restore();
    protected virtual void OnDestroy() => Restore();

    public void Apply(UIFeedbackContext context, bool immediate)
    {
        if (!isActiveAndEnabled) return;
        if (!captured) captured = Capture();
        if (!captured) return;

        KillTween();
        ApplyState(context, immediate || !Application.isPlaying || Duration <= 0f);
    }

    public void Restore()
    {
        KillTween();
        if (!captured) return;
        RestoreValues();
        captured = false;
    }

    protected void Play(Tween animation, UIFeedbackContext context)
    {
        tween = animation.SetRecyclable(false).SetUpdate(context.UseUnscaledTime);
    }

    private void KillTween()
    {
        if (tween != null && tween.IsActive()) tween.Kill();
        tween = null;
    }

    /// <summary>点击成功的一次性反馈扩展点；不会阻塞 Button.onClick。</summary>
    public virtual void PlayClick(UIFeedbackContext context) { }

    public abstract void CollectBindings(List<UIFeedbackBinding> bindings);
    protected abstract bool Capture();
    protected abstract void ApplyState(UIFeedbackContext context, bool immediate);
    protected abstract void RestoreValues();
}
