/// <summary>
/// 商店商品 —— 一件挂牌出售的牌
/// </summary>
[System.Serializable]
public class ShopGoods
{
    /// <summary>打折时的折扣率（半价）</summary>
    public const float DiscountRate = 0.5f;

    /// <summary>商品的牌定义（花色 / 点数 / 技能 / 自带属性）</summary>
    public CardDef def;

    /// <summary>波动后的原价（生成时算好并落盘，所以退出重进价格不变）</summary>
    public int price;

    /// <summary>本件是否打折（同样落盘，重进商店不会重抽）</summary>
    public bool discount;

    /// <summary>是否已售出</summary>
    public bool sold;

    /// <summary>
    /// 实际售价：打折 = 原价 × 50% 四舍五入（最低 1）。
    /// 只读属性 —— JsonUtility 只序列化字段，不会把算出来的值写进存档
    /// </summary>
    public int FinalPrice => discount ? UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(price * DiscountRate)) : price;
}
