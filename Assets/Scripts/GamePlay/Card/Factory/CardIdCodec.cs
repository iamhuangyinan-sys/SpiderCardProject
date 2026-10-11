using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 牌 id 编解码 —— 把「花色列表 + 点数列表」编成一个自解释的字符串 id
///
/// 格式：
///   普通牌：N{花色串}_{点数串}             例 N02_0jk   = 花色[红心,黑桃] + 点数[10,J,K]
///   特殊牌：S{花色串}_{点数串}_{配表 id}     例 S_m_0200001 = 无花色 + [-1] + 配表「复制」
///
/// 字符表：
///   花色：0 红心 / 1 方块 / 2 黑桃 / 3 梅花，空 = 无花色（与 E_CardSuitEnum 一致）
///   点数：1..9 = A..9，0 = 10，j = 11，q = 12，k = 13，s = -2，m = -1，空 = 无点数
///
/// 生成时按固定字符序排序 + 去重，保证同一个花色点数组合只有一种 id 写法
/// </summary>
public static class CardIdCodec
{
    private const string NormalTag = "N";
    private const string SpecialTag = "S";

    /// <summary>点数的固定字符序（生成排序用，也是比较用的唯一顺序）</summary>
    private const string RankOrder = "1234567890jqksm";

    /// <summary>花色的固定字符序（= 花色值本身，见 E_CardSuitEnum）</summary>
    private const string SuitOrder = "0123";

    /// <summary>非法点数字符的返回值</summary>
    private const int InvalidRank = int.MinValue;

    // ==================== 编码 ====================

    /// <summary>花色列表 + 点数列表 → 普通牌编码 id（点数超过上限时警告并返回 null）</summary>
    public static string Encode(IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        if (!CheckRankCount(null, ranks)) return null;

        return NormalTag + EncodeSuits(suits) + "_" + EncodeRanks(ranks);
    }

    /// <summary>单个花色 + 单个点数 → 普通牌编码 id</summary>
    public static string Encode(int suit, int rank)
    {
        return Encode(new[] { suit }, new[] { rank });
    }

    /// <summary>特殊牌编码 id（花色点数段 + 配表 id；点数超过上限时警告并返回 null）</summary>
    public static string EncodeSpecial(IReadOnlyList<int> suits, IReadOnlyList<int> ranks, string configId)
    {
        if (!CheckRankCount(null, ranks)) return null;

        return SpecialTag + EncodeSuits(suits) + "_" + EncodeRanks(ranks) + "_" + configId;
    }

    // ==================== 解析 ====================

    /// <summary>是否是特殊牌（S 开头）</summary>
    public static bool IsSpecial(string id)
    {
        return !string.IsNullOrEmpty(id) && id[0] == SpecialTag[0];
    }

    /// <summary>
    /// 解析编码 id：拆出花色列表与点数列表。
    /// 格式不对（前缀 / 段数 / 非法字符）→ 返回 false 并打日志
    /// </summary>
    public static bool TryParse(string id, out List<int> suits, out List<int> ranks)
    {
        suits = new List<int>();
        ranks = new List<int>();

        if (string.IsNullOrEmpty(id))
        {
            LogError(id, "为空");
            return false;
        }

        // 保留空段：S__0300001 → ["S", "", "0300001"]
        string[] parts = id.Split('_');
        if (parts.Length < 2 || parts.Length > 3)
        {
            LogError(id, "段数不对（应为 N花色_点数 或 S花色_点数_配表id）");
            return false;
        }

        bool isSpecial = id[0] == SpecialTag[0];
        if (!isSpecial && id[0] != NormalTag[0])
        {
            LogError(id, "前缀必须是 N（普通牌）或 S（特殊牌）");
            return false;
        }

        if (isSpecial && parts.Length != 3)
        {
            LogError(id, "特殊牌要带配表 id（S花色_点数_配表id）");
            return false;
        }

        if (!isSpecial && parts.Length != 2)
        {
            LogError(id, "普通牌只能是 N花色_点数 两段");
            return false;
        }

        if (isSpecial && parts[2].Length == 0)
        {
            LogError(id, "特殊牌缺少配表 id");
            return false;
        }

        // 第 1 段：前缀 + 花色串
        string tag = parts[0];
        for (int i = 1; i < tag.Length; i++)
        {
            int suit = SuitOrder.IndexOf(tag[i]);
            if (suit < 0)
            {
                LogError(id, $"非法花色字符 '{tag[i]}'（只能是 0-3）");
                return false;
            }
            suits.Add(suit);
        }

        // 第 2 段：点数串
        foreach (char c in parts[1])
        {
            int rank = RankCharToValue(c);
            if (rank == InvalidRank)
            {
                LogError(id, $"非法点数字符 '{c}'（只能是 1-9 0 j q k s m）");
                return false;
            }
            ranks.Add(rank);
        }

        // 逻辑规定：点数最多 CardRankConst.MaxCount 个（含主点数），普通牌 / 特殊牌都一样
        if (!CheckRankCount(id, ranks)) return false;

        suits.Sort();
        ranks.Sort();
        return true;
    }

    // ==================== 内部 ====================

    private static string EncodeSuits(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0) return string.Empty;

        var chars = new List<char>();
        foreach (int suit in suits)
        {
            if (suit < 0 || suit >= SuitOrder.Length) continue;   // 非法花色直接丢

            char c = SuitOrder[suit];
            if (!chars.Contains(c)) chars.Add(c);
        }

        chars.Sort();
        return new string(chars.ToArray());
    }

    private static string EncodeRanks(IReadOnlyList<int> ranks)
    {
        if (ranks == null || ranks.Count == 0) return string.Empty;

        var chars = new List<char>();
        foreach (int rank in ranks)
        {
            char c = RankValueToChar(rank);
            if (c == '?' || chars.Contains(c)) continue;

            chars.Add(c);
        }

        // 按固定字符序排序（不是 ASCII 序），保证同一组合只有一种写法
        chars.Sort((a, b) => RankOrder.IndexOf(a).CompareTo(RankOrder.IndexOf(b)));
        return new string(chars.ToArray());
    }

    private static int RankCharToValue(char c)
    {
        switch (c)
        {
            case '0': return 10;
            case 'j': return 11;
            case 'q': return 12;
            case 'k': return 13;
            case 's': return CardRankConst.Ignore;   // -2
            case 'm': return CardRankConst.Wild;     // -1
            default: return c >= '1' && c <= '9' ? c - '0' : InvalidRank;
        }
    }

    /// <summary>点数 → 字符（非法点数返回 '?'）</summary>
    private static char RankValueToChar(int rank)
    {
        switch (rank)
        {
            case 10: return '0';
            case 11: return 'j';
            case 12: return 'q';
            case 13: return 'k';
            case CardRankConst.Ignore: return 's';
            case CardRankConst.Wild: return 'm';
            default: return rank >= 1 && rank <= 9 ? (char)('0' + rank) : '?';
        }
    }

    private static void LogError(string id, string reason)
    {
        Debug.LogError($"[CardIdCodec] 非法牌 id「{id}」：{reason}");
    }

    /// <summary>
    /// 点数数量是否合法（最多 CardRankConst.MaxCount 个，含主点数，普通牌 / 特殊牌一样）。
    /// 超了 → 警告并返回 false。id 只用于日志（编码时还没拼出 id，可传 null）
    /// </summary>
    private static bool CheckRankCount(string id, IReadOnlyList<int> ranks)
    {
        int count = ranks?.Count ?? 0;
        if (count <= CardRankConst.MaxCount) return true;

        string who = string.IsNullOrEmpty(id) ? string.Empty : $"（{id}）";
        Debug.LogWarning($"[CardIdCodec] 牌最多 {CardRankConst.MaxCount} 个点数（含主点数）" +
                         $"，当前 {count} 个{who} —— 已拒绝");
        return false;
    }
}
