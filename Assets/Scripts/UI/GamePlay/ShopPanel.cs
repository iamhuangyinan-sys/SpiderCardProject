using System.Collections.Generic;
using Framework.Event;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 商店面板 —— 展示随机刷新的商品，购买后点继续进入下一层
/// </summary>
public partial class ShopPanel : NormalPanel
{
    /// <summary>列表里各格对应 ShopStore.goods 的下标（已售出的不进列表）</summary>
    private readonly List<int> _visibleGoods = new();

    /// <summary>删牌区文字的原始颜色（预制体上的值，只记一次）</summary>
    private Color _deletePriceColor;
    private Color _deleteCountColor;

    /// <summary>提示用的颜色（不够钱 / 次数到上限）—— 和商品价格的红色保持一致</summary>
    private static readonly Color WarnColor = new Color(1f, 0.4f, 0.4f);

    /// <summary>打开飞入 / 关闭飞出</summary>
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    protected override void OnOpen()
    {
        comps.btnContinue.onClick.AddListener(OnClickContinue);
        comps.btnDeleteCard.onClick.AddListener(OnClickDeleteCard);

        // 记下预制体上的文字颜色，后面变红 / 变回都靠它
        if (comps.txtDeletePrice != null) _deletePriceColor = comps.txtDeletePrice.color;
        if (comps.txtDeleteCount != null) _deleteCountColor = comps.txtDeleteCount.color;

        // 列表不挂 OnShopChanged：买牌时格子要自己飞走，重建列表会把它回收掉
        // （商品只会在进商店时变，重新打开面板时 OnOpen / OnShow 会重排）
        EventManager.Instance.AddListener(E_EventEnum.OnShopChanged, RefreshDelete);
        EventManager.Instance.AddListener(E_EventEnum.OnCoinChanged, RefreshDelete);
        EventManager.Instance.AddListener(E_EventEnum.OnCoinChanged, RefreshGoodsPrice);

        RefreshList();
        RefreshDelete();
    }

    protected override void OnShow()
    {
        RefreshList();
        RefreshDelete();
        RefreshGoodsPrice();
    }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnContinue.onClick.RemoveListener(OnClickContinue);
        comps.btnDeleteCard.onClick.RemoveListener(OnClickDeleteCard);

        EventManager.Instance.RemoveListener(E_EventEnum.OnShopChanged, RefreshDelete);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCoinChanged, RefreshDelete);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCoinChanged, RefreshGoodsPrice);
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

    /// <summary>刷新删牌区：已用次数 / 上限 + 单次费用 + 颜色（不够钱 / 次数到上限变红）</summary>
    private void RefreshDelete()
    {
        var shop = ShopManager.Instance;

        if (comps.txtDeleteCount != null)
        {
            comps.txtDeleteCount.text = $"{shop.DeleteCount} / {shop.DeleteLimit}";
            comps.txtDeleteCount.color = shop.IsDeleteLimitReached ? WarnColor : _deleteCountColor;
        }

        if (comps.txtDeletePrice != null)
        {
            comps.txtDeletePrice.text = shop.DeletePrice.ToString();
            comps.txtDeletePrice.color = shop.IsDeleteAffordable ? _deletePriceColor : WarnColor;
        }

        // 按钮始终可点：次数用完 / 钱不够都要靠点击弹提示（静默失效体验差）
        if (comps.btnDeleteCard != null) comps.btnDeleteCard.interactable = true;
    }

    /// <summary>
    /// 刷新商品价格的颜色 / 数字（金币变了要重刷）。
    /// 不重建列表 —— 只对当前可见格子走一遍
    /// </summary>
    private void RefreshGoodsPrice()
    {
        comps.listShopCard.ForEachVisible(item => (item as ShopCardItem)?.RefreshPrice());
    }

    /// <summary>删牌：够钱且还有次数 → 开选牌面板，选中的牌立即从本局牌组里删掉</summary>
    private void OnClickDeleteCard()
    {
        var shop = ShopManager.Instance;

        if (shop.IsDeleteLimitReached)
        {
            TipHelper.Show("删除次数达到上限");
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

        ShowCardsPanel.PickCard(RunManager.Instance.Deck, "请选择一张要删除的牌", def =>
        {
            if (def.attributes != null && def.attributes.Contains(E_CardAttributeEnum.CannotDelete))
            {
                TipHelper.Show("无法删除这张牌");
                return false;
            }
            return shop.TryDeleteCard(def);
        });
    }
}
