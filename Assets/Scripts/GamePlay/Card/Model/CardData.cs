using System.Collections.Generic;

/// <summary>
/// 单张卡牌的数据（纯数据，不引用任何 UnityEngine 对象）
/// </summary>
public class CardData
{
    /// <summary>牌 id（配表主键）</summary>
    public string id;

    /// <summary>
    /// 花色列表（规则用，配表 Suit）：
    ///   空      = 没有花色 → 跟谁都不是同花（只能单张拖起）
    ///   0-3     = 红心 / 梅花 / 黑桃 / 方块，可多个
    ///   [0,1,2,3] = 万能花色（跟任意花色都有交集）
    /// </summary>
    public List<int> suits = new();

    /// <summary>
    /// 点数列表（规则用，配表 Rank）：
    ///   空    = 没有点数 → 无法连接、也接不住任何牌
    ///   1-13  = 普通点数，可多个
    ///   -1    = 万能放下：可移动到任意牌上（不看点数也不看花色）；连接时无视点数（仍要看花色）
    ///   -2    = 无视点数：连接 / 放下都不比点数，但要看花色
    /// </summary>
    public List<int> ranks = new();

    /// <summary>是否正面朝上（false = 牌背朝上）</summary>
    public bool isFaceUp;

    /// <summary>落牌技能：放下之后触发（配表 PlaceSkill 只决定默认值，复制到别人的技能也走这里）</summary>
    public E_PlaceSkillEnum placeSkill;

    /// <summary>
    /// 沉底标记：牌被收入弃牌堆时按概率打上，发出时以牌背插到该列堆底（而不是正面发到堆顶），
    /// 防止玩家反复用同一批牌刷接龙次数。标记只服务于「发到堆底」这一次，发出后即清除。
    /// </summary>
    public bool isSunk;

    /// <summary>
    /// 整体复制另一张牌的属性（id / 花色 / 点数 / 落牌技能）。
    /// 可套娃：复制到复制牌，自己也同样获得复制技能。
    /// 注意：isFaceUp / isSunk 是牌在牌桌上的状态，不属于牌的属性，不复制
    /// 注意：花色 / 点数必须深拷贝 —— 否则复制牌和源牌共享同一个 List，改一个另一个跟着变
    /// </summary>
    public void CopyFrom(CardData other)
    {
        if (other == null) return;

        id = other.id;
        suits = new List<int>(other.suits);
        ranks = new List<int>(other.ranks);
        placeSkill = other.placeSkill;
    }
}
