using Framework.Event;
using Framework.UI;

/// <summary>
/// 商店面板 —— 展示随机刷新的商品，购买后点继续进入下一层
/// </summary>
public partial class ShopPanel : NormalPanel
{
    /// <summary>打开飞入 / 关闭飞出</summary>
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    protected override void OnOpen()
    {
        comps.btnContinue.onClick.AddListener(OnClickContinue);
        comps.btnDeleteCard.onClick.AddListener(OnClickDeleteCard);

        EventManager.Instance.AddListener(E_EventEnum.OnShopChanged, RefreshList);
        EventManager.Instance.AddListener(E_EventEnum.OnShopChanged, RefreshDelete);
        EventManager.Instance.AddListener(E_EventEnum.OnCoinChanged, RefreshDelete);

        RefreshList();
        RefreshDelete();
    }

    protected override void OnShow()
    {
        RefreshList();
        RefreshDelete();
    }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnContinue.onClick.RemoveListener(OnClickContinue);
        comps.btnDeleteCard.onClick.RemoveListener(OnClickDeleteCard);

        EventManager.Instance.RemoveListener(E_EventEnum.OnShopChanged, RefreshList);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopChanged, RefreshDelete);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCoinChanged, RefreshDelete);
    }

    /// <summary>刷新商品列表</summary>
    private void RefreshList()
    {
        comps.listShopCard.OnItemRender = (index, item) =>
        {
            var cell = item as ShopCardItem;
            if (cell != null) cell.Bind(index, ShopStore.Instance.Get(index));
        };

        comps.listShopCard.ItemCount = ShopStore.Instance.goods.Count;
    }

    /// <summary>继续：完成商店关，回选关</summary>
    private void OnClickContinue()
    {
        ShopManager.Instance.ContinueNextLevel();
    }

    // ==================== 删牌 ====================

    /// <summary>刷新删牌区：已用次数 / 上限 + 单次费用 + 按钮可用性</summary>
    private void RefreshDelete()
    {
        var shop = ShopManager.Instance;

        if (comps.txtDeleteCount != null) comps.txtDeleteCount.text = $"{shop.DeleteCount} / {shop.DeleteLimit}";
        if (comps.txtDeletePrice != null) comps.txtDeletePrice.text = shop.DeletePrice.ToString();

        // 次数用完就不能再删了；钱不够仍可点，会弹提示（不做静默失效）
        if (comps.btnDeleteCard != null) comps.btnDeleteCard.interactable = !shop.IsDeleteLimitReached;
    }

    /// <summary>删牌：够钱且还有次数 → 开选牌面板，选中的牌立即从本局牌组里删掉</summary>
    private void OnClickDeleteCard()
    {
        var shop = ShopManager.Instance;

        if (shop.IsDeleteLimitReached)
        {
            TipHelper.Show("本次商店的删牌次数已用完");
            return;
        }

        if (!shop.IsDeleteAffordable)
        {
            TipHelper.Show("金币不足");
            return;
        }

        if (RunManager.Instance.Deck.Count == 0)
        {
            TipHelper.Show("牌组里没有牌可以删");
            return;
        }

        ShowCardsPanel.PickCard(RunManager.Instance.Deck, id => shop.TryDeleteCard(id));
    }
}
