using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 牌定义的文本编解码 —— 「一个下划线分隔」的自解释写法，只给控制台 / 调试用
/// （游戏逻辑、存档里都是结构化数据，不经过字符串）
///
/// 格式（用 _ 分隔，后面几段可省，但第一个下划线一定要写）：
///   {花色}_{点数}[_{技能}[_{属性}]]
///
/// 例：
///   0123_123                   花色 红心/方块/黑桃/梅花 + 点数 A/2/3
///   _m                         无花色 + 点数 -1（万能放下）
///   0123_s_10002_10001;10002   蜘蛛 + 复制技能 + 沉底&毒
///
/// 字符表：
///   花色：0 红心 / 1 方块 / 2 黑桃 / 3 梅花，空 = 无花色
///   点数：1-9 = A-9、0 = 10、j/q/k、s = -2、m = -1，空 = 无点数
///   技能：E_PlaceSkillEnum 的数值（10001 回收 / 10002 复制），空 = 无技能
///   属性：E_CardAttributeEnum 的数值，多个用分号隔开
/// </summary>
public static class CardDefCodec
{
    private const char Separator = '_';

    private const char AttributeSeparator = ';';

    // ==================== 编码 ====================

    /// <summary>牌定义 → 文本（空定义返回空串；能省的后面的段会省掉）</summary>
    public static string ToText(CardDef def)
    {
        if (!def.IsValid) return string.Empty;

        string head = EncodeSuits(def.suits) + "_" + EncodeRanks(def.ranks);
        string skill = def.placeSkill != E_PlaceSkillEnum.None
            ? ((int)def.placeSkill).ToString()
            : string.Empty;

        int attributeCount = def.attributes?.Count ?? 0;
        if (attributeCount == 0)
        {
            return skill.Length > 0 ? head + "_" + skill : head;
        }

        return head + "_" + skill + "_" + EncodeAttributes(def.attributes);
    }

    /// <summary>花色列表 → 字符（0-3 连写）</summary>
    private static string EncodeSuits(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0) return string.Empty;

        var sb = new StringBuilder(suits.Count);
        for (int i = 0; i < suits.Count; i++) sb.Append(suits[i]);
        return sb.ToString();
    }

    /// <summary>点数列表 → 字符</summary>
    private static string EncodeRanks(IReadOnlyList<int> ranks)
    {
        if (ranks == null || ranks.Count == 0) return string.Empty;

        var sb = new StringBuilder(ranks.Count);
        for (int i = 0; i < ranks.Count; i++)
        {
            char c = CardDef.RankToChar(ranks[i]);
            if (c != '\0') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>属性列表 → 分号隔开的数值</summary>
    private static string EncodeAttributes(IReadOnlyList<E_CardAttributeEnum> attributes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < attributes.Count; i++)
        {
            if (sb.Length > 0) sb.Append(AttributeSeparator);
            sb.Append((int)attributes[i]);
        }
        return sb.ToString();
    }

    // ==================== 解析 ====================

    /// <summary>解析文本 → 牌定义；失败时 error 说明原因（会写进控制台输出）</summary>
    public static bool TryParse(string text, out CardDef def, out string error)
    {
        def = default;
        error = null;

        if (string.IsNullOrEmpty(text))
        {
            error = "牌定义为空（至少写 花色_点数，如 _1）";
            return false;
        }

        string[] parts = text.Split(Separator);
        if (parts.Length < 2)
        {
            error = "缺少下划线（格式：花色_点数[_技能[_属性]]）";
            return false;
        }

        if (parts.Length > 4)
        {
            error = "段数太多（格式：花色_点数[_技能[_属性]]）";
            return false;
        }

        if (!TryParseSuits(parts[0], out var suits, out error)) return false;
        if (!TryParseRanks(parts[1], out var ranks, out error)) return false;

        // 第 3 段：技能（可空 = None）
        var placeSkill = E_PlaceSkillEnum.None;
        if (parts.Length > 2 && parts[2].Length > 0)
        {
            if (!int.TryParse(parts[2], out int skillValue) ||
                !Enum.IsDefined(typeof(E_PlaceSkillEnum), skillValue))
            {
                error = $"非法落牌技能「{parts[2]}」（可用 10001 回收 / 10002 复制）";
                return false;
            }

            placeSkill = (E_PlaceSkillEnum)skillValue;
        }

        // 第 4 段：属性（可空）
        List<E_CardAttributeEnum> attributes = null;
        if (parts.Length > 3 && parts[3].Length > 0)
        {
            if (!CardAttributeHelper.TryParse(parts[3], out attributes))
            {
                error = $"非法属性「{parts[3]}」（用分号隔开的枚举值，如 10001;10002）";
                return false;
            }
        }

        if (!CardDef.TryCreate(suits, ranks, placeSkill, attributes, out def, out error)) return false;

        return true;
    }

    /// <summary>花色段：0-3 连写，可空</summary>
    private static bool TryParseSuits(string text, out List<int> suits, out string error)
    {
        suits = new List<int>();
        error = null;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c < '0' || c > '3')
            {
                error = $"非法花色字符「{c}」（只能是 0-3）";
                return false;
            }

            int suit = c - '0';
            if (!suits.Contains(suit)) suits.Add(suit);
        }

        return true;
    }

    /// <summary>点数段：字符表见类注释，可空</summary>
    private static bool TryParseRanks(string text, out List<int> ranks, out string error)
    {
        ranks = new List<int>();
        error = null;

        for (int i = 0; i < text.Length; i++)
        {
            int rank = CardDef.CharToRank(text[i]);
            if (rank == int.MinValue)
            {
                error = $"非法点数字符「{text[i]}」（1-9 / 0 / j / q / k / s / m）";
                return false;
            }

            if (!ranks.Contains(rank)) ranks.Add(rank);
        }

        return true;
    }
}
