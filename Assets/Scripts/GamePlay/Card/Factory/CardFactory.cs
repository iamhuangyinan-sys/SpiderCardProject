using System.Collections.Generic;

/// <summary>
/// 建牌工厂 —— 按编码 id 或花色点数构造 CardData
///
/// 普通牌（N 开头）：不查配表，花色点数直接从 id 解析，图案由表现层按花色点数自动推导
/// 特殊牌（S 开头）：查配表拿落牌技能 / 图案 / 文案（配表 Id 就是整串编码 id）
/// </summary>
public static class CardFactory
{
    /// <summary>按编码 id 建牌；id 非法返回 null</summary>
    public static CardData Create(string encodedId)
    {
        if (!CardIdCodec.TryParse(encodedId, out var suits, out var ranks)) return null;

        var card = new CardData
        {
            id = encodedId,
            suits = suits,
            ranks = ranks,
            isFaceUp = false,
        };

        // 特殊牌才有配表：补落牌技能（图案由表现层按 id 查表拿，这里不存）
        if (CardIdCodec.IsSpecial(encodedId))
        {
            var cfg = GetConfig(encodedId);
            card.placeSkill = cfg != null ? (E_PlaceSkillEnum)cfg.PlaceSkill : E_PlaceSkillEnum.None;
        }

        return card;
    }

    /// <summary>按花色点数建普通牌（点数超过上限时 Encode 会警告并返回 null，这里直接返回 null）</summary>
    public static CardData Create(IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        string id = CardIdCodec.Encode(suits, ranks);
        return string.IsNullOrEmpty(id) ? null : Create(id);
    }

    /// <summary>按单个花色 + 单个点数建普通牌</summary>
    public static CardData Create(int suit, int rank)
    {
        return Create(CardIdCodec.Encode(suit, rank));
    }

    /// <summary>按编码 id 查配表（普通牌查不到，返回 null）</summary>
    public static CardConfig GetConfig(string encodedId)
    {
        if (string.IsNullOrEmpty(encodedId)) return null;

        return ConfigHelper.Get<CardConfig>(encodedId);
    }
}
