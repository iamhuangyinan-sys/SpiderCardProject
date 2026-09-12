using Framework.UI;

/// <summary>
/// 商店商品格 —— 显示牌图与价格，点击购买
/// </summary>
public partial class ShopCardItem : BaseListItem
{
    private int _index = -1;

    public override void OnActivate() { }

    public override void OnRecycle()
    {
        comps.btnCard.onClick.RemoveListener(OnClickCard);
        _index = -1;
    }

    /// <summary>绑定商品并刷新显示</summary>
    public void Bind(int index, ShopGoods goods)
    {
        _index = index;

        comps.btnCard.onClick.RemoveListener(OnClickCard);
        comps.btnCard.onClick.AddListener(OnClickCard);

        if (goods == null) return;

        // 牌图（与场上卡牌同一套图，路径 Resources/GamePlay/Card/）
        var cfg = ConfigHelper.Get<CardConfig>(goods.cardId);
        if (cfg != null && comps.btnCard.image != null)
        {
            comps.btnCard.image.sprite = CardResManager.Instance.GetCardImage(cfg.Image);
        }

        // 价格 / 已售出
        comps.txtCoin.text = goods.sold ? "已售出" : goods.price.ToString();
        comps.btnCard.interactable = !goods.sold;
    }

    private void OnClickCard()
    {
        ShopManager.Instance.TryBuy(_index);
    }
}
