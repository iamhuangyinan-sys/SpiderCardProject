/// <summary>
/// 单张卡牌的数据（纯数据，不引用任何 UnityEngine 对象）
/// </summary>
public class CardData
{
    /// <summary>牌 id（配表主键）</summary>
    public string id;

    /// <summary>花色（规则用）</summary>
    public E_CardSuitEnum suit;

    /// <summary>点数 1-13，对应 A-K（规则用）</summary>
    public int rank;

    /// <summary>是否正面朝上（false = 牌背朝上）</summary>
    public bool isFaceUp;
}
