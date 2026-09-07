using DG.Tweening;
using UnityEngine;

/// <summary>
/// 动画工具 —— 对 DOTween 的通用语义化封装（纯静态，不服务具体业务）
/// 各业务模块调用本类编排动画，不直接依赖 DOTween API。
/// </summary>
public static class AnimationHelper
{
    /// <summary>从 from 瞬移，再飞到 to（发牌、移动等用）</summary>
    public static Tween FlyTo(Transform trans, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutQuad)
    {
        trans.position = from;
        return trans.DOMove(to, duration).SetEase(ease);
    }

    /// <summary>直接移动到 to</summary>
    public static Tween MoveTo(Transform trans, Vector3 to, float duration, Ease ease = Ease.OutQuad)
    {
        return trans.DOMove(to, duration).SetEase(ease);
    }

    /// <summary>缩放到目标值（入场 / 翻牌等用）</summary>
    public static Tween ScaleTo(Transform trans, Vector3 target, float duration, Ease ease = Ease.OutBack)
    {
        return trans.DOScale(target, duration).SetEase(ease);
    }

    /// <summary>取消该物体上的所有动画（回收 / 打断时调用，避免动画残留）</summary>
    public static void Kill(Transform trans)
    {
        trans.DOKill();
    }
}
