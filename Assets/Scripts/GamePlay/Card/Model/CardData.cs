using System.Collections.Generic;

/// <summary>
/// 场上单张卡牌的数据（纯数据，不引用任何 UnityEngine 对象）
///
/// = 牌定义（CardDef：花色 / 点数 / 落牌技能 / 自带属性）
/// + 关卡内状态（朝向 / 临时属性）
///
/// 牌组、商店、存档里的「静态牌数据」用 CardDef，不要用这个类
/// </summary>
public class CardData
{
    /// <summary>牌的构成（花色 / 点数 / 落牌技能 / 自带属性），建牌时已深拷贝</summary>
    public CardDef def;

    /// <summary>是否正面朝上（false = 牌背朝上）</summary>
    public bool isFaceUp;

    /// <summary>
    /// 属性列表（枚举值就是配表 CardAttributeConfig 的主键）：
    ///   空        = 没有任何属性
    ///   可多个    = 一张牌可以同时带多个属性
    /// 属性是牌在牌桌上的状态（牌自带属性在建牌时复制进来）；沉底由弃牌堆按概率打上，发出后清除
    /// </summary>
    public List<E_CardAttributeEnum> attributes = new();

    /// <summary>花色列表（转发牌定义，顺序 = 显示顺序，[0] = 主花色）</summary>
    public List<int> suits => def.suits;

    /// <summary>点数列表（转发牌定义，顺序 = 显示顺序，[0] = 主点数）</summary>
    public List<int> ranks => def.ranks;

    /// <summary>落牌技能（转发牌定义）</summary>
    public E_PlaceSkillEnum placeSkill => def.placeSkill;

    /// <summary>
    /// 最近一次解析出来的「显示用」花色（列对齐的结果；null = 还没算过，用原始列表）。
    /// 放在数据上而不是 View 上：View 会被回收重建，但牌跑一趟（拖到空列 / 进牌包再回来）
    /// 不应该变脸 —— 列顶没有对齐对象时就用这个保持原样
    /// </summary>
    public IReadOnlyList<int> displaySuits;

    /// <summary>最近一次解析出来的「显示用」点数（同 displaySuits）</summary>
    public IReadOnlyList<int> displayRanks;

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
    /// 整体复制另一张牌（花色 / 点数 / 落牌技能 / 自带属性 / 牌桌状态属性）—— 复制技能用。
    /// 可套娃：复制到复制牌，自己也同样获得复制技能。
    /// 注意：isFaceUp 是牌在牌桌上的朝向（表现状态），不复制
    /// 注意：牌定义与属性列表都必须深拷贝 —— 否则两张牌共享同一个 List，改一个另一个跟着变
    /// </summary>
    public void CopyFrom(CardData other)
    {
        if (other == null) return;

        def = other.def.Clone();
        attributes = new List<E_CardAttributeEnum>(other.attributes);

        // 牌面换了（花色 / 点数 / 技能都变），旧的显示用值是上一张牌的，作废重算
        displaySuits = null;
        displayRanks = null;
    }
}
