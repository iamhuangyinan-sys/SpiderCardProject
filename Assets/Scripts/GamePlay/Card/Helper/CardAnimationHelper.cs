using DG.Tweening;
using UnityEngine;

/// <summary>
/// 卡牌动画工具 —— 封装卡牌业务动画（发牌飞行等）。
/// 纯静态、无状态：from / to 等参数全部由调用方（CardViewManager）传入，
/// 不反向依赖布局，编排方式（Insert 重叠 / Append 串行）由调用方决定。
/// </summary>
public static class CardAnimationHelper
{
    /// <summary>卡牌飞行时的渲染排序偏移（临时抬高，避免被场上牌盖住）</summary>
    public const int FlySortingOffset = 10000;

    /// <summary>卡牌飞行时长（秒）</summary>
    public const float FlyDuration = 0.25f;

    /// <summary>逐张飞行间隔（秒，发牌/回收逐张的时间差）</summary>
    public const float FlyInterval = 0.1f;

    /// <summary>翻牌单段旋转时长（秒）</summary>
    public const float FlipDuration = 0.1f;

    /// <summary>翻牌时的渲染排序偏移（临时抬高，避免侧立时被相邻牌盖住）</summary>
    public const int FlipSortingOffset = 10000;

    /// <summary>
    /// 一张牌从 from 瞬移到 to：飞行期间临时抬高排序（不负责恢复，由调用方在 OnComplete 处理）。
    /// 返回飞行动画。
    /// </summary>
    public static Tween FlyTo(CardView view, Vector3 from, Vector3 to)
    {
        view.SetSortingOrder(view.CurrentSortingOrder + FlySortingOffset);
        return AnimationHelper.FlyTo(view.transform, from, to, FlyDuration);
    }

    /// <summary>
    /// 翻牌动画：Y 轴 0°→90°（中途切图）→0°，期间临时抬高排序，翻完恢复 finalOrder。
    /// 调用前提：该牌 View 当前显示背面（数据层已置 isFaceUp=true，View 尚未刷新）。
    /// </summary>
    public static void FlipUp(CardView view, int finalOrder)
    {
        if (view == null) return;

        view.SetSortingOrder(finalOrder + FlipSortingOffset);

        var seq = DOTween.Sequence();
        seq.Append(view.transform.DORotate(new Vector3(0f, 90f, 0f), FlipDuration).SetEase(Ease.Linear)
            .OnComplete(() => view.Refresh()));
        seq.Append(view.transform.DORotate(Vector3.zero, FlipDuration).SetEase(Ease.Linear));
        seq.OnComplete(() => view.SetSortingOrder(finalOrder));
    }
}
