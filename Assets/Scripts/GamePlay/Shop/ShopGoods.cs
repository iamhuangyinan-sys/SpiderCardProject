/// <summary>
/// 商店商品 —— 一件挂牌出售的牌
/// </summary>
[System.Serializable]
public class ShopGoods
{
    /// <summary>牌 id（CardConfig.Id）</summary>
    public string cardId;

    /// <summary>价格</summary>
    public int price;

    /// <summary>是否已售出</summary>
    public bool sold;
}
