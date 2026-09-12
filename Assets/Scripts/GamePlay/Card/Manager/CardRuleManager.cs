using System.Collections.Generic;
using Framework.Mgr;

/// <summary>
/// 卡牌规则 —— 判断能否拖起、能否拖到目标列、能否发牌（蜘蛛纸牌规则）
/// </summary>
public class CardRuleManager : ManagerBase<CardRuleManager>
{
    /// <summary>判断某张牌能否作为锚点被拖起（必须正面朝上）</summary>
    public bool CanDrag(CardData anchor)
    {
        return anchor != null && anchor.isFaceUp;
    }

    /// <summary>点数万能（配表 Rank = -1，蜘蛛牌）：可以当作任意点数</summary>
    public static bool IsRankWild(CardData card)
    {
        return card != null && card.rank == -1;
    }

    /// <summary>花色万能（配表 Suit = -1）：可以当作任意花色</summary>
    public static bool IsSuitWild(CardData card)
    {
        return card != null && card.suit == E_CardSuitEnum.Wild;
    }

    /// <summary>是否全万能（花色 + 点数都万能，配表两个都填 -1）</summary>
    public static bool IsFullWild(CardData card)
    {
        return IsRankWild(card) && IsSuitWild(card);
    }

    /// <summary>这张牌是否带「任意落点」属性：拖起后可以叠到任意牌上（配表只给默认值）</summary>
    public static bool IsAnyTarget(CardData card)
    {
        return card != null && card.isAnyTarget;
    }

    /// <summary>是否是黑色牌（无花色无点数，点数 -2，不能连接、只能单独拖、只能放空列）</summary>
    public static bool IsBlack(CardData card)
    {
        return card != null && card.rank == -2;
    }

    /// <summary>
    /// 两张相邻牌能否连接（能凑成可整体拖动的同花顺）：
    /// 万能花色不比花色，万能点数不比点数，两者都万能则与任意牌都能连；黑色牌不能连接
    /// </summary>
    private static bool CanConnect(CardData prev, CardData cur)
    {
        if (IsBlack(prev) || IsBlack(cur)) return false;

        bool suitOk = IsSuitWild(prev) || IsSuitWild(cur) || prev.suit == cur.suit;
        bool rankOk = IsRankWild(prev) || IsRankWild(cur) || cur.rank == prev.rank - 1;

        return suitOk && rankOk;
    }

    /// <summary>
    /// 获取能被整体拖动的牌串：
    /// 锚点牌及其同列下方所有牌，必须整串「翻开、同花色、逐张减 1」才可拖，
    /// 任何一张不满足则整体不可拖（返回空），并通过 blockedCard 返回导致不可拖的那张牌。
    /// </summary>
    public List<CardData> GetDraggableCards(CardData anchor, out CardData blockedCard)
    {
        blockedCard = null;
        var result = new List<CardData>();
        if (!CanDrag(anchor)) return result;

        var columns = CardsStore.Instance.columns;

        // 单抓牌：只能单独拖拽单张，且必须位于列顶（不能被其他牌压住）
        if (anchor.isSingleGrab)
        {
            foreach (var column in columns)
            {
                int idx = column.IndexOf(anchor);
                if (idx < 0) continue;
                // 只有列顶的单抓牌才可拖起，被压住则不可拖
                if (idx == column.Count - 1)
                {
                    result.Add(anchor);
                }
                else
                {
                    blockedCard = column[idx + 1];  // 压住它的那张牌
                }
                break;
            }
            return result;
        }

        foreach (var column in columns)
        {
            int idx = column.IndexOf(anchor);
            if (idx < 0) continue;

            // 收集 anchor 及其下方所有牌
            for (int i = idx; i < column.Count; i++)
            {
                result.Add(column[i]);
            }

            // 整串必须「翻开、可连接（同花色，蜘蛛牌或逐张减 1）」，任何一张不满足则整体不可拖
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
    /// - 非空：普通牌点数比目标堆顶小 1；蜘蛛牌同花色即可连接
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
        if (targetColumn.Count == 0) return true;

        // 任意落点牌：可以叠到任意牌上（含黑色牌；自身是黑色牌也照常生效）
        if (IsAnyTarget(anchor)) return true;

        // 黑色牌只能放到空列
        if (IsBlack(anchor)) return false;

        var top = targetColumn[targetColumn.Count - 1];

        // 蜘蛛牌参与（点数万能）：不比点数，只看花色；花色万能则花色也不比
        if (IsRankWild(anchor) || IsRankWild(top))
        {
            return IsSuitWild(anchor) || IsSuitWild(top) || anchor.suit == top.suit;
        }

        // 普通牌：只看点数（同花色不是落列的必要条件，同花顺才是）
        return anchor.rank == top.rank - 1;
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
    /// 检查某列末尾 13 张是否形成 A-K 同花顺（13 张、同花色、点数 K→A），
    /// 是则返回该顺子（K 到 A 共 13 张），否则返回 null。
    /// 万能牌可以顶替它所在位置的牌：万能花色顶替花色，万能点数顶替点数
    /// </summary>
    public List<CardData> GetCompletedSequence(List<CardData> column)
    {
        if (column == null || column.Count < 13) return null;

        int start = column.Count - 13;
        int end = start + 13;

        // 花色基准：取第一张非万能花色的牌（整段都是万能花色时，花色不做要求）
        E_CardSuitEnum suit = E_CardSuitEnum.Wild;
        for (int i = start; i < end; i++)
        {
            if (IsSuitWild(column[i])) continue;

            suit = column[i].suit;
            break;
        }

        for (int i = start; i < end; i++)
        {
            var card = column[i];

            // 花色：万能花色可当基准花色，其余必须与基准一致
            if (!IsSuitWild(card) && card.suit != suit) return null;

            // 点数：从 K 逐张减到 A；万能点数可当任意点数
            if (!IsRankWild(card) && card.rank != 13 - (i - start)) return null;
        }

        return column.GetRange(start, 13);
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
