using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 牌的「定义」—— 一张牌由「花色列表 + 点数列表 + 落牌技能 + 自带属性」构成，没有 id。
///
/// 用途（全部是静态牌数据）：
///   本局牌组（RunStore.deck）、商店商品、关卡事件牌、存档
/// 场上牌用 CardData（= 一个 CardDef + 关卡内状态），两者别混用。
///
/// 规范化：构造一律走 <see cref="Create"/> —— 花色按 0-3、点数按 1234567890jqksm 排序去重，
/// 点数上限 CardRankConst.MaxCount。好处：
///   1. 「第一个花色 / 第一个点数 = 主花色 / 主点数」的既有规则天然成立
///   2. 同一个组合只有一种表示 → 可以按值比较 / 从牌组里删
/// </summary>
[Serializable]
public struct CardDef : IEquatable<CardDef>
{
    /// <summary>点数排序用的固定字符序（也是唯一合法点数字符表）</summary>
    private const string RankOrder = "1234567890jqksm";

    /// <summary>花色列表（已排序去重；空 = 没有花色）</summary>
    public List<int> suits;

    /// <summary>点数列表（已排序去重；空 = 没有点数）</summary>
    public List<int> ranks;

    /// <summary>落牌技能（None = 普通牌；不是 None 的牌就是以前的「特殊牌」）</summary>
    public E_PlaceSkillEnum placeSkill;

    /// <summary>
    /// 牌自带属性（预留：以后「毒」这类固有属性配在这里）。
    /// 建场上的牌时会复制进 CardData.attributes，和关卡内临时属性共存
    /// </summary>
    public List<E_CardAttributeEnum> attributes;

    /// <summary>是否是一个构造成功的定义（default(CardDef) 的列表是 null，算无效）</summary>
    public bool IsValid => suits != null && ranks != null;

    /// <summary>是否带落牌技能（= 以前的「特殊牌」）</summary>
    public bool HasSkill => placeSkill != E_PlaceSkillEnum.None;

    // ==================== 构造 ====================

    /// <summary>
    /// 构造并规范化（花色 / 点数排序去重、校验点数上限）。
    /// 非法时打警告并返回无效定义（IsValid = false），调用方要判。
    /// </summary>
    public static CardDef Create(IReadOnlyList<int> suits, IReadOnlyList<int> ranks,
        E_PlaceSkillEnum placeSkill = E_PlaceSkillEnum.None,
        IReadOnlyList<E_CardAttributeEnum> attributes = null)
    {
        return TryCreate(suits, ranks, placeSkill, attributes, out var def) ? def : default;
    }

    /// <summary>同 Create，但用返回值表示成功；失败原因写进 error</summary>
    public static bool TryCreate(IReadOnlyList<int> suits, IReadOnlyList<int> ranks,
        E_PlaceSkillEnum placeSkill, IReadOnlyList<E_CardAttributeEnum> attributes,
        out CardDef def, out string error)
    {
        def = default;

        if (!NormalizeSuits(suits, out var suitList, out error)) return false;
        if (!NormalizeRanks(ranks, out var rankList, out error)) return false;

        def = new CardDef
        {
            suits = suitList,
            ranks = rankList,
            placeSkill = placeSkill,
            attributes = attributes != null
                ? new List<E_CardAttributeEnum>(attributes)
                : new List<E_CardAttributeEnum>(),
        };

        return true;
    }

    /// <summary>同 TryCreate，但不关心失败原因</summary>
    public static bool TryCreate(IReadOnlyList<int> suits, IReadOnlyList<int> ranks,
        E_PlaceSkillEnum placeSkill, IReadOnlyList<E_CardAttributeEnum> attributes, out CardDef def)
    {
        return TryCreate(suits, ranks, placeSkill, attributes, out def, out _);
    }

    /// <summary>单个花色 + 单个点数的快捷构造</summary>
    public static CardDef Create(int suit, int rank)
    {
        return Create(new[] { suit }, new[] { rank });
    }

    // ==================== 复制 / 比较 ====================

    /// <summary>深拷贝（列表是引用，传给场上牌之前必须拷一份）</summary>
    public CardDef Clone()
    {
        var copy = this;
        copy.suits = suits != null ? new List<int>(suits) : new List<int>();
        copy.ranks = ranks != null ? new List<int>(ranks) : new List<int>();
        copy.attributes = attributes != null
            ? new List<E_CardAttributeEnum>(attributes)
            : new List<E_CardAttributeEnum>();
        return copy;
    }

    /// <summary>按内容比较（花色 / 点数已规范化，顺序是确定的；属性不看顺序）</summary>
    public bool Equals(CardDef other)
    {
        if (placeSkill != other.placeSkill) return false;
        if (!SameSequence(suits, other.suits)) return false;
        if (!SameSequence(ranks, other.ranks)) return false;

        int myCount = attributes?.Count ?? 0;
        int otherCount = other.attributes?.Count ?? 0;
        if (myCount != otherCount) return false;

        for (int i = 0; i < myCount; i++)
        {
            if (!other.attributes.Contains(attributes[i])) return false;
        }

        return true;
    }

    public override bool Equals(object obj) => obj is CardDef other && Equals(other);

    public override int GetHashCode()
    {
        int hash = (int)placeSkill;

        if (suits != null)
        {
            for (int i = 0; i < suits.Count; i++) hash = hash * 31 + suits[i];
        }

        if (ranks != null)
        {
            for (int i = 0; i < ranks.Count; i++) hash = hash * 31 + ranks[i];
        }

        if (attributes != null)
        {
            for (int i = 0; i < attributes.Count; i++) hash = hash * 31 + (int)attributes[i];
        }

        return hash;
    }

    private static bool SameSequence(List<int> a, List<int> b)
    {
        int countA = a?.Count ?? 0;
        int countB = b?.Count ?? 0;
        if (countA != countB) return false;

        for (int i = 0; i < countA; i++)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }

    // ==================== 字符表（编码 / 解析共用） ====================

    /// <summary>点数 → 字符：1-9 = A-9、0 = 10、j/q/k、s = -2、m = -1；非法返回 '\0'</summary>
    public static char RankToChar(int rank)
    {
        if (rank >= 1 && rank <= 9) return (char)('0' + rank);

        switch (rank)
        {
            case 10: return '0';
            case 11: return 'j';
            case 12: return 'q';
            case 13: return 'k';
            case CardRankConst.Ignore: return 's';
            case CardRankConst.Wild: return 'm';
            default: return '\0';
        }
    }

    /// <summary>字符 → 点数；非法返回 int.MinValue</summary>
    public static int CharToRank(char c)
    {
        if (c >= '1' && c <= '9') return c - '0';

        switch (char.ToLowerInvariant(c))
        {
            case '0': return 10;
            case 'j': return 11;
            case 'q': return 12;
            case 'k': return 13;
            case 's': return CardRankConst.Ignore;
            case 'm': return CardRankConst.Wild;
            default: return int.MinValue;
        }
    }

    // ==================== 规范化 ====================

    /// <summary>花色：0-3 合法，排序升序 + 去重（空 = 没有花色）</summary>
    private static bool NormalizeSuits(IReadOnlyList<int> input, out List<int> result, out string error)
    {
        result = new List<int>();
        error = null;

        if (input == null) return true;

        for (int i = 0; i < input.Count; i++)
        {
            int suit = input[i];

            if (suit < (int)E_CardSuitEnum.Hearts || suit > (int)E_CardSuitEnum.Clubs)
            {
                error = $"非法花色 {suit}（只能是 0-3）";
                return false;
            }

            if (!result.Contains(suit)) result.Add(suit);
        }

        result.Sort();
        return true;
    }

    /// <summary>点数：按 RankOrder 排序 + 去重，数量上限 CardRankConst.MaxCount</summary>
    private static bool NormalizeRanks(IReadOnlyList<int> input, out List<int> result, out string error)
    {
        result = new List<int>();
        error = null;

        if (input == null) return true;

        for (int i = 0; i < input.Count; i++)
        {
            int rank = input[i];

            if (RankToChar(rank) == '\0')
            {
                error = $"非法点数 {rank}";
                return false;
            }

            if (!result.Contains(rank)) result.Add(rank);
        }

        if (result.Count > CardRankConst.MaxCount)
        {
            error = $"点数最多 {CardRankConst.MaxCount} 个（含主点数），当前 {result.Count} 个";
            return false;
        }

        result.Sort(CompareRank);
        return true;
    }

    /// <summary>按固定字符序比较点数（= 主点数的优先顺序）</summary>
    private static int CompareRank(int a, int b)
    {
        return RankOrder.IndexOf(RankToChar(a)).CompareTo(RankOrder.IndexOf(RankToChar(b)));
    }
}
