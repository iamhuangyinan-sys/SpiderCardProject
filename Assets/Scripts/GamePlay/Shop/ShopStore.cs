using System.Collections.Generic;
using Framework.Store;

/// <summary>
/// 商店数据层 —— 保存当前商店的商品列表
/// </summary>
public class ShopStore : StoreBase<ShopStore>
{
    /// <summary>当前商店的商品（前 2 件特殊牌，后 4 件普通牌）</summary>
    public readonly List<ShopGoods> goods = new();

    protected override void OnInit()
    {
        Clear();
    }

    protected override void OnDispose()
    {
        Clear();
    }

    /// <summary>清空商品列表</summary>
    public void Clear()
    {
        goods.Clear();
    }

    /// <summary>取某件商品（越界返回 null）</summary>
    public ShopGoods Get(int index) =>
        index >= 0 && index < goods.Count ? goods[index] : null;

    /// <summary>通知数据变更（UI 刷新）</summary>
    public void Refresh() => NotifyDataChanged();
}
