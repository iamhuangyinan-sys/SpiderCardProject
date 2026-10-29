using System.Collections.Generic;
using Framework.Event;
using Framework.UI;

/// <summary>
/// 商店面板 —— 展示随机刷新的商品，购买后点继续进入下一层
/// </summary>
public partial class ShopPanel : NormalPanel
{
    /// <summary>列表里各格对应 ShopStore.goods 的下标（已售出的不进列表）</summary>
    private readonly List<int> _visibleGoods = new();

    /// <summary>打开飞入 / 关闭飞出</summary>
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    protected override void OnOpen()
    {
        comps.btnContinue.onClick.AddListener(OnClickContinue);
        comps.btnDeleteCard.onClick.AddListener(OnClickDeleteCard);

        // 列表不挂 OnShopChanged：买牌时格子要自己飞走，重建列表会把它回收掉
        // （商品只会在进商店时变，重新打开面板时 OnOpen / OnShow 会重排）
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

        EventManager.Instance.RemoveListener(E_EventEnum.OnShopChanged, RefreshDelete);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCoinChanged, RefreshDelete);
    }

    /// <summary>刷新商品列表（已售出的商品不再占格子）</summary>
    private void RefreshList()
    {
        _visibleGoods.Clear();

        var goods = ShopStore.Instance.goods;
        for (int i = 0; i < goods.Count; i++)
        {
            if (!goods[i].sold) _visibleGoods.Add(i);
        }

        comps.listShopCard.OnItemRender = (index, item) =>
        {
            var cell = item as ShopCardItem;
            if (cell == null || index < 0 || index >= _visibleGoods.Count) return;

            int goodsIndex = _visibleGoods[index];
            cell.Bind(goodsIndex, ShopStore.Instance.Get(goodsIndex));
        };

        comps.listShopCard.ItemCount = _visibleGoods.Count;
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

        ShowCardsPanel.PickCard(RunManager.Instance.Deck, def => shop.TryDeleteCard(def));
    }
}
