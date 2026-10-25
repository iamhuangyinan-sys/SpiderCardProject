/// <summary>
/// 商店商品 —— 一件挂牌出售的牌
/// </summary>
[System.Serializable]
public class ShopGoods
{
    /// <summary>商品的牌定义（花色 / 点数 / 技能 / 自带属性）</summary>
    public CardDef def;

    /// <summary>价格</summary>
    public int price;

    /// <summary>是否已售出</summary>
    public bool sold;
}
