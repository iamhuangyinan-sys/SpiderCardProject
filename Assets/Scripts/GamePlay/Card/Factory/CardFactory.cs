using System.Collections.Generic;

/// <summary>
/// 建牌工厂 —— 按牌定义（CardDef）构造场上的 CardData
///
/// 不再区分普通牌 / 特殊牌：带落牌技能（placeSkill != None）的牌就是以前说的「特殊牌」，
/// 它的整图由 PlaceSkillConfig.Image 提供；不带技能就按主花色 + 主点数推导图案
/// </summary>
public static class CardFactory
{
    /// <summary>按牌定义建牌；定义无效返回 null</summary>
    public static CardData Create(CardDef def)
    {
        if (!def.IsValid) return null;

        var card = new CardData
        {
            // 深拷贝：牌组里那一份列表不能被场上牌改到
            def = def.Clone(),
            isFaceUp = false,
        };

        // 牌自带属性 → 场上属性（以后配「牌天生带毒」就走这里）
        var baseAttributes = card.def.attributes;
        for (int i = 0; i < baseAttributes.Count; i++)
        {
            card.SetAttribute(baseAttributes[i], true);
        }

        return card;
    }

    /// <summary>按花色 / 点数列表建牌（会做规范化；非法返回 null）</summary>
    public static CardData Create(IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var def = CardDef.Create(suits, ranks);
        return def.IsValid ? Create(def) : null;
    }

    /// <summary>按单个花色 + 单个点数建牌</summary>
    public static CardData Create(int suit, int rank)
    {
        return Create(CardDef.Create(suit, rank));
    }
}
