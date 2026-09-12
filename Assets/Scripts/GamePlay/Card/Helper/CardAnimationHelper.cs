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

    /// <summary>发光目标透明度</summary>
    public const float GlowAlpha = 0.9f;

    /// <summary>投影目标透明度</summary>
    public const float ShadowAlpha = 0.8f;

    /// <summary>抓起时牌串整体的左上偏移</summary>
    public static readonly Vector3 LiftOffset = new Vector3(-0.1f, 0.125f, 0f);

    /// <summary>抓起时影子的右下偏移（本地，相对牌）</summary>
    public static readonly Vector3 ShadowLiftOffset = new Vector3(0.2f, -0.25f, 0f);

    /// <summary>
    /// 一张牌从 from 瞬移到 to：飞行期间临时抬高排序（不负责恢复，由调用方在 OnComplete 处理）。
    /// 返回飞行动画。
    /// </summary>
    public static Tween FlyTo(CardView view, Vector3 from, Vector3 to)
    {
        CardSoundHelper.PlayPlace();
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

        CardSoundHelper.PlayFlip();
        view.SetSortingOrder(finalOrder + FlipSortingOffset);

        var seq = DOTween.Sequence();
        seq.Append(view.transform.DORotate(new Vector3(0f, 90f, 0f), FlipDuration).SetEase(Ease.Linear)
            .OnComplete(() => view.Refresh()));
        seq.Append(view.transform.DORotate(Vector3.zero, FlipDuration).SetEase(Ease.Linear));
        seq.OnComplete(() => view.SetSortingOrder(finalOrder));
    }

    /// <summary>显示发光（淡入）</summary>
    public static void ShowGlow(CardView view)
    {
        if (view.GlowRenderer == null) return;
        view.GlowGo.SetActive(true);
        view.GlowRenderer.DOFade(GlowAlpha, 0.15f);
    }

    /// <summary>显示投影（淡入）</summary>
    public static void ShowShadow(CardView view)
    {
        if (view.ShadowRenderer == null) return;
        view.ShadowGo.SetActive(true);
        view.ShadowRenderer.DOFade(ShadowAlpha, 0.15f);
    }

    /// <summary>显示发光与投影（淡入），投影按牌串实际高度拉伸</summary>
    public static void ShowGlowShadow(CardView view, int cardCount)
    {
        ShowGlow(view);
        ShowShadow(view);

        float cardHeight = view.RootRenderer != null ? view.RootRenderer.bounds.size.y : 1f;
        float stretch = 1f + (cardCount - 1) * CardViewManager.FaceUpSpacing / cardHeight;
        if (view.ShadowGo != null)
        {
            view.ShadowGo.transform.localScale = new Vector3(1f, stretch, 1f);
        }
    }

    /// <summary>隐藏发光与投影（淡出），淡出完成后恢复投影缩放</summary>
    public static void HideGlowShadow(CardView view)
    {
        if (view.GlowRenderer != null)
        {
            view.GlowRenderer.DOFade(0f, 0.1f).OnComplete(() => view.GlowGo.SetActive(false));
        }
        if (view.ShadowRenderer != null)
        {
            view.ShadowRenderer.DOFade(0f, 0.1f).OnComplete(() =>
            {
                view.ShadowGo.SetActive(false);
                view.ShadowGo.transform.localScale = Vector3.one;
            });
        }
    }

    /// <summary>设置影子本地偏移（在初始位置基础上，动画过渡）</summary>
    public static void SetShadowLocalOffset(CardView view, Vector3 offset, float duration = 0.15f)
    {
        if (view.ShadowGo != null)
        {
            view.ShadowGo.transform.DOLocalMove(view.ShadowInitLocalPos + offset, duration);
        }
    }

    /// <summary>不可拖提示：短暂变灰后恢复</summary>
    public static void FlashBlocked(CardView view)
    {
        if (view.RootRenderer == null) return;
        view.RootRenderer.DOKill();
        view.RootRenderer.DOColor(Color.gray, 0.1f).OnComplete(() =>
        {
            view.RootRenderer.DOColor(Color.white, 0.25f);
        });
    }

    /// <summary>复位：清理残留动画 + 恢复发光投影状态（Bind 时调用）</summary>
    public static void Reset(CardView view)
    {
        if (view.GlowRenderer != null) view.GlowRenderer.DOKill();
        if (view.ShadowRenderer != null) view.ShadowRenderer.DOKill();
        if (view.RootRenderer != null) view.RootRenderer.DOKill();
        if (view.GlowGo != null) view.GlowGo.SetActive(false);
        if (view.ShadowGo != null)
        {
            view.ShadowGo.transform.localScale = Vector3.one;
            view.ShadowGo.transform.localPosition = view.ShadowInitLocalPos;
            view.ShadowGo.SetActive(false);
        }
    }

    /// <summary>抓起锚点牌：发光+投影（按张数拉伸）+ 影子右下偏移动画</summary>
    public static void LiftAnchor(CardView view, int cardCount)
    {
        ShowGlowShadow(view, cardCount);
        SetShadowLocalOffset(view, ShadowLiftOffset);
    }

    /// <summary>抓起普通牌：只显示发光</summary>
    public static void LiftCard(CardView view)
    {
        ShowGlow(view);
    }

    /// <summary>放下：隐藏发光投影 + 影子恢复动画</summary>
    public static void DropCard(CardView view)
    {
        HideGlowShadow(view);
        SetShadowLocalOffset(view, Vector3.zero);
    }
}
