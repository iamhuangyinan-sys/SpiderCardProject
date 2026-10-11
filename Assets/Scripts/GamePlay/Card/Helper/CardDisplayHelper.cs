using System.Collections.Generic;

/// <summary>
/// 多重花色 / 多重点数的「显示用值」解析 —— 纯计算，不碰 CardData、不碰表现层。
///
/// 列是从上往下连的（row 0 = 列顶），每张牌都跟**它上面那张牌的实际值**对齐：
///   花色：上面那张的有效花色里有一个能对上 → 把那个花色提到最前（= 主花色），其余花色位置不变
///   点数：正好是「上面那张的实际点数 − 1」→ 把那个点数提到最前（= 主点数），其余点数位置不变
///
/// 所以解析就是「从列顶往下扫一遍」：刷新到哪张牌就定哪张牌，是个自然的刷新一代价，
/// 不需要在放牌时额外触发什么；也不会因为上面叠了两张多重牌算错 ——
/// 用的是上面那张**解析之后**的实际值，而不是它的原始列表。
///
/// -1（万能放下）/ -2（无视点数）都跟"没有点数"一样：不参与对齐，也不往下传点数限制。
/// </summary>
public static class CardDisplayHelper
{
    /// <summary>
    /// 显示用花色：suits 里第一个出现在 aboveSuits 里的花色提到最前。
    /// aboveSuits = null（列顶 / 上面是牌背）/ 自己不足 2 个花色 → 原样返回（同一个引用，方便判等）
    /// </summary>
    public static IReadOnlyList<int> PromoteSuit(IReadOnlyList<int> suits, IReadOnlyList<int> aboveSuits)
    {
        if (suits == null || suits.Count <= 1 || aboveSuits == null) return suits;

        for (int i = 0; i < suits.Count; i++)
        {
            if (Contains(aboveSuits, suits[i])) return PromoteToFront(suits, i);
        }

        return suits;
    }

    /// <summary>
    /// 显示用点数：ranks 里等于「aboveRank − 1」的那个点数提到最前。
    /// aboveRank = null（列顶 / 上面是牌背 / 上面不按点数限制）/ 自己不足 2 个点数 → 原样返回
    /// </summary>
    public static IReadOnlyList<int> PromoteRank(IReadOnlyList<int> ranks, int? aboveRank)
    {
        if (ranks == null || ranks.Count <= 1 || aboveRank == null) return ranks;

        int want = aboveRank.Value - 1;

        for (int i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] == want) return PromoteToFront(ranks, i);
        }

        return ranks;
    }

    /// <summary>
    /// 这张牌「实际当成几点」—— 传给下面那张用（下面那张要正好比它小 1）。
    /// 没有点数 / 含 -1 / 含 -2 → null：规则里这几种情况都不按点数限制，下面那张不受它约束。
    /// 其余情况就是显示用的主点数（PromoteRank 已经把对齐后的那个提到最前了）
    /// </summary>
    public static int? EffectiveRank(IReadOnlyList<int> displayRanks)
    {
        if (displayRanks == null || displayRanks.Count == 0) return null;

        foreach (int rank in displayRanks)
        {
            if (rank == CardRankConst.Wild || rank == CardRankConst.Ignore) return null;
        }

        return displayRanks[0];
    }

    /// <summary>把第 index 项提到最前，其余保持原顺序（index &lt;= 0 时原样返回同一个引用）</summary>
    private static IReadOnlyList<int> PromoteToFront(IReadOnlyList<int> list, int index)
    {
        if (list == null || index <= 0) return list;

        var result = new int[list.Count];
        result[0] = list[index];

        int write = 1;
        for (int i = 0; i < list.Count; i++)
        {
            if (i != index) result[write++] = list[i];
        }

        return result;
    }

    private static bool Contains(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value) return true;
        }

        return false;
    }
}
