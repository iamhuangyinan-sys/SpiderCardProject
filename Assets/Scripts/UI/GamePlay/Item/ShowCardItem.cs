using Framework.UI;

/// <summary>
/// 展示用的单张牌 —— 只显示牌图（沉底牌显示黄色）；默认不可点击，鼠标悬停放大
/// 选牌模式（ShopCardsPanel.PickCard）下会被挂上点击回调
/// </summary>
public partial class ShowCardItem : BaseListItem
{
    /// <summary>悬停缩放（挂在根节点上，缩放目标是内部的 mod_Card；没有就退化成不缩放）</summary>
    private HoverScaleBehaviour _hoverScale;

    public override void OnActivate()
    {
        if (_hoverScale == null) _hoverScale = GetComponent<HoverScaleBehaviour>();

        comps.modCard.OnHoverChanged = OnCardHover;
    }

    public override void OnRecycle()
    {
        comps.modCard.OnHoverChanged = null;
        _hoverScale?.SetHover(false, true);   // 回收：立即复位（组件 OnDisable 也会兜底）
        comps.modCard.Clear();
    }

    /// <summary>牌面悬停变化 → 整个格子缩放</summary>
    private void OnCardHover(bool hovering) => _hoverScale?.SetHover(hovering);

    /// <summary>按牌 id 显示牌图（isSunk = 沉底牌，染黄）</summary>
    public void Bind(string cardId, bool isSunk = false)
    {
        comps.modCard.Bind(cardId, isSunk);
    }

    /// <summary>设置点击回调；传 null 表示不可点击（删牌选牌等场合用）</summary>
    public void SetClickable(System.Action onClick)
    {
        comps.modCard.SetClickable(onClick);
    }
}
