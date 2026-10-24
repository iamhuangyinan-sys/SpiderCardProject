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
    ///   0-3     = 红心 / 方块 / 黑桃 / 梅花，可多个
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
    /// 属性列表（枚举值就是配表 CardAttributeConfig 的主键）：
    ///   空        = 没有任何属性
    ///   可多个    = 一张牌可以同时带多个属性
    /// 属性是牌在牌桌上的状态，不进 id 编码；沉底由弃牌堆按概率打上，发出后清除
    /// </summary>
    public List<E_CardAttributeEnum> attributes = new();

    /// <summary>有没有某个属性</summary>
    public bool HasAttribute(E_CardAttributeEnum attribute)
    {
        return attributes.Contains(attribute);
    }

    /// <summary>加 / 去一个属性（幂等：已经有了就不再添加）</summary>
    public void SetAttribute(E_CardAttributeEnum attribute, bool on)
    {
        bool has = attributes.Contains(attribute);

        if (on)
        {
            if (!has) attributes.Add(attribute);
        }
        else if (has)
        {
            attributes.Remove(attribute);
        }
    }

    /// <summary>
    /// 整体复制另一张牌的属性（id / 花色 / 点数 / 落牌技能 / 牌桌状态属性）。
    /// 可套娃：复制到复制牌，自己也同样获得复制技能。
    /// 注意：isFaceUp 是牌在牌桌上的朝向（表现状态），不复制；attributes 是牌的属性，要跟着源牌一起复制
    /// 注意：花色 / 点数 / 属性必须深拷贝 —— 否则复制牌和源牌共享同一个 List，改一个另一个跟着变
    /// </summary>
    public void CopyFrom(CardData other)
    {
        if (other == null) return;

        id = other.id;
        suits = new List<int>(other.suits);
        ranks = new List<int>(other.ranks);
        placeSkill = other.placeSkill;
        attributes = new List<E_CardAttributeEnum>(other.attributes);
    }
}
