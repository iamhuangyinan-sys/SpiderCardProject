using Framework.UI;

/// <summary>
/// 商店商品格 —— 显示牌图与价格，点击购买；鼠标悬停整个格子放大
/// </summary>
public partial class ShopCardItem : BaseListItem
{
    private int _index = -1;

    /// <summary>悬停缩放（挂在根节点上，缩放目标是内部那个轴心居中的 layout；没有就退化成不缩放）</summary>
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
        _index = -1;
    }

    /// <summary>牌面悬停变化 → 整个格子缩放（连价格一起）</summary>
    private void OnCardHover(bool hovering) => _hoverScale?.SetHover(hovering);

    /// <summary>绑定商品并刷新显示</summary>
    public void Bind(int index, ShopGoods goods)
    {
        _index = index;

        if (goods == null)
        {
            comps.modCard.Clear();
            comps.txtCoin.text = string.Empty;
            return;
        }

        comps.modCard.Bind(goods.cardId);

        // 已售出 → 不可点击（模组会把按钮组件关掉）
        if (goods.sold)
        {
            comps.modCard.SetClickable(null);
            comps.txtCoin.text = "已售出";
        }
        else
        {
            comps.modCard.SetClickable(OnClickCard);
            comps.txtCoin.text = goods.price.ToString();
        }
    }

    private void OnClickCard()
    {
        ShopManager.Instance.TryBuy(_index);
    }
}
