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

    /// <summary>
    /// 获取能被整体拖动的牌串：
    /// 锚点牌及其同列下方所有牌，必须整串「翻开、同花色、逐张减 1」才可拖，
    /// 任何一张不满足则整体不可拖（返回空）
    /// </summary>
    public List<CardData> GetDraggableCards(CardData anchor)
    {
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

            // 整串必须「翻开、同花色、逐张减 1」，任何一张不满足则整体不可拖
            for (int i = 1; i < result.Count; i++)
            {
                var prev = result[i - 1];
                var cur = result[i];
                if (!(cur.isFaceUp && cur.suit == prev.suit && cur.rank == prev.rank - 1))
                {
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
    /// - 非空：锚点牌（拖串最上面那张）点数比目标堆顶小 1
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

        var top = targetColumn[targetColumn.Count - 1];
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
    /// 检查某列从最下方往上是否形成 A-K 同花顺（13 张、同花色、点数 K→A），
    /// 是则返回该顺子（K 到 A 共 13 张），否则返回 null
    /// </summary>
    public List<CardData> GetCompletedSequence(List<CardData> column)
    {
        if (column == null || column.Count < 13) return null;

        int start = column.Count - 13;
        var suit = column[start].suit;
        for (int i = 0; i < 13; i++)
        {
            var card = column[start + i];
            if (card.suit != suit || card.rank != 13 - i) return null;
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
