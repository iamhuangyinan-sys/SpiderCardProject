/// <summary>
/// 商店商品 —— 一件挂牌出售的牌
/// </summary>
[System.Serializable]
public class ShopGoods
{
    /// <summary>打折时的折扣率（半价）</summary>
    public const float DiscountRate = 0.5f;
    private const double PoisonChance = 0.2;
    private const decimal PoisonPriceRate = 0.9m;

    /// <summary>商品的牌定义（花色 / 点数 / 技能 / 自带属性）</summary>
    public CardDef def;

    /// <summary>波动后的原价（生成时算好并落盘，所以退出重进价格不变）</summary>
    public int price;

    /// <summary>本件是否打折（同样落盘，重进商店不会重抽）</summary>
    public bool discount;

    /// <summary>生成时确定的毒牌折价标记，随商品存档</summary>
    public bool poisonDiscount;

    /// <summary>是否已售出</summary>
    public bool sold;

    public int FinalPrice
    {
        get
        {
            int originalPrice = discount ? UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(price * DiscountRate)) : price;
            return poisonDiscount
                ? (int)System.Math.Round(originalPrice * PoisonPriceRate, System.MidpointRounding.AwayFromZero)
                : originalPrice;
        }
    }

    public void RollPoison(System.Random random)
    {
        if (def.attributes.Contains(E_CardAttributeEnum.Poison))
        {
            poisonDiscount = true;
            return;
        }

        if (random.NextDouble() >= PoisonChance) return;

        def.attributes.Add(E_CardAttributeEnum.Poison);
        poisonDiscount = true;
    }
}
