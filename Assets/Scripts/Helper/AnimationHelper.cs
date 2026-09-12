using DG.Tweening;
using UnityEngine;

/// <summary>
/// 动画工具 —— 对 DOTween 的通用语义化封装（纯静态，不服务具体业务）
/// 各业务模块调用本类编排动画，不直接依赖 DOTween API。
/// </summary>
public static class AnimationHelper
{
    /// <summary>UI 面板入场默认时长</summary>
    public const float PanelFlyInDuration = 0.3f;

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

    /// <summary>
    /// UI 面板从下方飞入：先瞬移到目标位置下方 offsetY，再向上滑回原位 + 淡入。
    /// </summary>
    /// <param name="rect">面板 RectTransform（原位 = 调用时的 anchoredPosition）</param>
    /// <param name="canvasGroup">淡入用（可为 null，则只做位移）</param>
    /// <param name="offsetY">起始下移距离（一般传面板高度，保证从屏幕外飞入）</param>
    public static Tween FlyInFromBottom(RectTransform rect, CanvasGroup canvasGroup, float offsetY,
        float duration = PanelFlyInDuration, Ease ease = Ease.OutCubic)
    {
        if (rect == null) return null;

        // 清掉残留动画，避免重复打开时位置错乱
        rect.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();

        Vector2 target = rect.anchoredPosition;
        rect.anchoredPosition = target - new Vector2(0f, offsetY);

        var seq = DOTween.Sequence();
        seq.Join(rect.DOAnchorPos(target, duration).SetEase(ease));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            seq.Join(canvasGroup.DOFade(1f, duration));
        }

        return seq;
    }

    /// <summary>取消该物体上的所有动画（回收 / 打断时调用，避免动画残留）</summary>
    public static void Kill(Transform trans)
    {
        trans.DOKill();
    }
}
