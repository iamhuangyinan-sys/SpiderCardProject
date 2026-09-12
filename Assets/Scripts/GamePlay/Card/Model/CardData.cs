/// <summary>
/// 单张卡牌的数据（纯数据，不引用任何 UnityEngine 对象）
/// </summary>
public class CardData
{
    /// <summary>牌 id（配表主键）</summary>
    public string id;

    /// <summary>花色（规则用）；E_CardSuitEnum.Wild（配表 -1）= 万能花色，可当作任意花色</summary>
    public E_CardSuitEnum suit;

    /// <summary>点数 1-13，对应 A-K（规则用）；-1 = 万能点数，-2 = 黑色牌（不参与连接）</summary>
    public int rank;

    /// <summary>是否正面朝上（false = 牌背朝上）</summary>
    public bool isFaceUp;

    /// <summary>单抓：只能单独拖起单张（不能被其他牌压住）</summary>
    public bool isSingleGrab;

    /// <summary>
    /// 任意落点：拖起后可以叠到任意牌上，无视点数、花色、黑色牌限制；
    /// 只是「放得下去」，能不能拖起来仍看单抓 / 同花连通那套规则。
    /// 配表 AnyTarget 只决定默认是否带此属性（与 SingleGrab 同理），普通牌后续也能被赋予
    /// </summary>
    public bool isAnyTarget;

    /// <summary>落牌技能：放下之后触发（配表 PlaceSkill 只决定默认值，复制到别人的技能也走这里）</summary>
    public E_PlaceSkillEnum placeSkill;

    /// <summary>
    /// 沉底标记：牌被收入弃牌堆时按概率打上，发出时以牌背插到该列堆底（而不是正面发到堆顶），
    /// 防止玩家反复用同一批牌刷接龙次数。标记只服务于「发到堆底」这一次，发出后即清除。
    /// </summary>
    public bool isSunk;

    /// <summary>
    /// 整体复制另一张牌的属性（id / 花色 / 点数 / 单抓 / 任意落点 / 落牌技能）。
    /// 可套娃：复制到复制牌，自己也同样获得复制技能。
    /// 注意：isFaceUp / isSunk 是牌在牌桌上的状态，不属于牌的属性，不复制
    /// </summary>
    public void CopyFrom(CardData other)
    {
        if (other == null) return;

        id = other.id;
        suit = other.suit;
        rank = other.rank;
        isSingleGrab = other.isSingleGrab;
        isAnyTarget = other.isAnyTarget;
        placeSkill = other.placeSkill;
    }
}
