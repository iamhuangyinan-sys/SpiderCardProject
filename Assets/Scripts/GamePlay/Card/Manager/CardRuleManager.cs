using System.Collections.Generic;
using Framework.Mgr;

/// <summary>
/// 卡牌规则 —— 判断能否拖起、能否拖到目标列、能否发牌（蜘蛛纸牌规则）
///
/// 花色 / 点数都是「列表」（支持多重花色、多重点数）：
///   花色：两牌花色列表有交集才算同花；空列表 = 没有花色，跟谁都不同花
///   点数：1-13 普通；-1 万能放下；-2 无视点数（但要同花）；空列表 = 没有点数
///
/// 一条主线：列的排列是「从上往下点数逐张减 1」（9-8-7…），
///   所以「上方牌、下方牌」的点数关系恒为：下.ranks 含 上.ranks - 1
///   能连接(上, 下) = 花色有交集 且 点数能相连
/// </summary>
public class CardRuleManager : ManagerBase<CardRuleManager>
{
    /// <summary>判断某张牌能否作为锚点被拖起（必须正面朝上）</summary>
    public bool CanDrag(CardData anchor)
    {
        return anchor != null && anchor.isFaceUp;
    }

    // ==================== 花色 ====================

    /// <summary>两张牌的花色列表是否有交集（有交集 = 同花；空列表跟谁都没交集）</summary>
    public static bool HasCommonSuit(CardData a, CardData b)
    {
        if (a == null || b == null) return false;

        foreach (int s in a.suits)
        {
            if (b.suits.Contains(s)) return true;
        }
        return false;
    }

    // ==================== 点数 ====================

    /// <summary>点数列表里有没有这个值（判断 -1 / -2 用）</summary>
    public static bool HasRankValue(CardData card, int rank)
    {
        return card != null && card.ranks.Contains(rank);
    }

    /// <summary>
    /// 列里「上方牌压着下方牌」时，点数能否相连。
    /// 判定：下.ranks 里存在 上.ranks - 1（从上往下点数逐张减 1，如 9-8-7…）
    /// 任一侧含 -1（万能）或 -2（无视点数）→ 点数不设限；任一侧没有点数 → 连不上
    /// </summary>
    public static bool RankLinks(CardData upper, CardData lower)
    {
        if (upper == null || lower == null) return false;

        if (HasRankValue(upper, CardRankConst.Wild) || HasRankValue(lower, CardRankConst.Wild)) return true;
        if (HasRankValue(upper, CardRankConst.Ignore) || HasRankValue(lower, CardRankConst.Ignore)) return true;

        if (upper.ranks.Count == 0 || lower.ranks.Count == 0) return false;   // 没有点数，接不上

        foreach (int u in upper.ranks)
        {
            foreach (int l in lower.ranks)
            {
                if (l == u - 1) return true;
            }
        }
        return false;
    }

    /// <summary>两张相邻牌能否连接（凑成可整体拖动的同花顺）：花色有交集 + 点数能相连</summary>
    public static bool CanConnect(CardData upper, CardData lower)
    {
        return HasCommonSuit(upper, lower) && RankLinks(upper, lower);
    }

    /// <summary>
    /// 获取能被整体拖动的牌串：
    /// 锚点牌及其同列下方所有牌，必须整串「翻开、同花、逐张减 1」才可拖，
    /// 任何一张不满足则整体不可拖（返回空），并通过 blockedCard 返回导致不可拖的那张牌。
    ///
    /// 没有花色 / 没有点数的牌与下方连不上 → 自然就只能单独拖（旧 SingleGrab 字段已删）
    /// </summary>
    public List<CardData> GetDraggableCards(CardData anchor, out CardData blockedCard)
    {
        blockedCard = null;
        var result = new List<CardData>();
        if (!CanDrag(anchor)) return result;

        var columns = CardsStore.Instance.columns;

        foreach (var column in columns)
        {
            int idx = column.IndexOf(anchor);
            if (idx < 0) continue;

            // 收集 anchor 及其下方所有牌
            for (int i = idx; i < column.Count; i++)
            {
                result.Add(column[i]);
            }

            // 整串必须「翻开、能连接（同花 + 点数相连）」，任何一张不满足则整体不可拖
            // （没有花色 / 没有点数的牌连不上下一张 → 自然就只能单独拖起）
            for (int i = 1; i < result.Count; i++)
            {
                var prev = result[i - 1];
                var cur = result[i];
                if (!(cur.isFaceUp && CanConnect(prev, cur)))
                {
                    blockedCard = cur;   // 阻塞点：这张牌导致整串不可拖
                    result.Clear();
                    return result;
                }
            }
            break;
        }

        return result;
    }

    /// <summary>
    /// 判断能否把一串牌移动到目标列：
    /// - 空列：可直接移动
    /// - 万能放下（-1）：可落到任意牌上（不看点数也不看花色）
    /// - 涉及「无视点数」（-2）：只看花色（红心牌才能压到红心蜘蛛上）
    /// - 普通牌：只看点数（同花色不是落列的必要条件，同花顺才是）
    /// - 不能移回原列
    /// </summary>
    public bool CanMove(List<CardData> draggedCards, int targetColumnIndex)
    {
        if (draggedCards == null || draggedCards.Count == 0) return false;

        var columns = CardsStore.Instance.columns;
        if (targetColumnIndex < 0 || targetColumnIndex >= columns.Count) return false;

        var anchor = draggedCards[0];

        int fromColumn = FindColumnIndex(anchor);
        // 在列里且移回原列 → 拒绝；不在列里（如牌包）则跳过
        if (fromColumn >= 0 && fromColumn == targetColumnIndex) return false;

        var targetColumn = columns[targetColumnIndex];
        if (targetColumn.Count == 0) return true;   // 空列：任何牌都能放

        var top = targetColumn[targetColumn.Count - 1];

        // 万能放下：直接落，不比点数也不比花色
        if (HasRankValue(anchor, CardRankConst.Wild)) return true;

        // 只要涉及「无视点数」的牌（-2），就要求同花
        bool rankIgnored = HasRankValue(anchor, CardRankConst.Ignore) || HasRankValue(top, CardRankConst.Ignore);
        if (rankIgnored && !HasCommonSuit(anchor, top)) return false;

        // 注意方向：anchor 会落到目标堆顶的「下方」，所以 top 是上方牌
        return RankLinks(top, anchor);
    }

    /// <summary>查找某张牌所在的列索引（找不到返回 -1）</summary>
    public int FindColumnIndex(CardData card)
    {
        var columns = CardsStore.Instance.columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Contains(card)) return i;
        }
        return -1;
    }

    /// <summary>判断当前能否从发牌堆发牌（存在空列则不能发）</summary>
    public bool CanDeal()
    {
        var columns = CardsStore.Instance.columns;
        foreach (var column in columns)
        {
            if (column.Count == 0) return false;
        }
        return true;
    }

    /// <summary>
    /// 检查某列末尾 13 张是否形成 A-K 同花顺（13 张、同花、点数 K→A），
    /// 是则返回该顺子（K 到 A 共 13 张），否则返回 null。
    /// 花色：整段花色列表的交集非空（多重花色的牌能顶替它含的任一花色）
    /// 点数：必须真的是 13→1，每张牌的点数列表里要真的有这个点数（-1 / -2 都不算）
    /// </summary>
    public List<CardData> GetCompletedSequence(List<CardData> column)
    {
        if (column == null || column.Count < 13) return null;

        int start = column.Count - 13;

        if (!HasCommonSuitOfRange(column, start, 13)) return null;

        for (int i = 0; i < 13; i++)
        {
            if (!column[start + i].ranks.Contains(13 - i)) return null;
        }

        return column.GetRange(start, 13);
    }

    /// <summary>一段牌的花色列表交集是否非空（多重花色：逐张求交）</summary>
    private static bool HasCommonSuitOfRange(List<CardData> column, int start, int count)
    {
        var common = new List<int>(column[start].suits);
        if (common.Count == 0) return false;

        for (int i = 1; i < count; i++)
        {
            var suits = column[start + i].suits;

            for (int j = common.Count - 1; j >= 0; j--)
            {
                if (!suits.Contains(common[j])) common.RemoveAt(j);
            }

            if (common.Count == 0) return false;
        }

        return true;
    }

    /// <summary>判断能否把某张牌移到指定牌包（单张即可，且该牌包为空）</summary>
    public bool CanMoveToPocket(CardData card, int pocketIndex)
    {
        if (card == null) return false;
        var pockets = CardsStore.Instance.pockets;
        if (pocketIndex < 0 || pocketIndex >= pockets.Count) return false;
        return pockets[pocketIndex] == null;
    }

    /// <summary>判断能否从牌包把牌移到目标列（复用单张牌移到列的规则）</summary>
    public bool CanMoveFromPocket(int pocketIndex, int targetColumnIndex)
    {
        var store = CardsStore.Instance;
        if (pocketIndex < 0 || pocketIndex >= store.pockets.Count) return false;
        var card = store.pockets[pocketIndex];
        if (card == null) return false;
        return CanMove(new List<CardData> { card }, targetColumnIndex);
    }

    /// <summary>查找某张牌所在的牌包索引（找不到返回 -1）</summary>
    public int FindPocketIndex(CardData card)
    {
        var pockets = CardsStore.Instance.pockets;
        for (int i = 0; i < pockets.Count; i++)
        {
            if (pockets[i] == card) return i;
        }
        return -1;
    }
}
