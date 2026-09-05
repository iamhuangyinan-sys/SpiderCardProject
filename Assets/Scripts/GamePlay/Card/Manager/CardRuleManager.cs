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
    /// - 非空：锚点牌（拖串最上面那张）与目标堆顶同花色、点数小 1
    /// - 不能移回原列
    /// </summary>
    public bool CanMove(List<CardData> draggedCards, int targetColumnIndex)
    {
        if (draggedCards == null || draggedCards.Count == 0) return false;

        var columns = CardsStore.Instance.columns;
        if (targetColumnIndex < 0 || targetColumnIndex >= columns.Count) return false;

        var anchor = draggedCards[0];

        int fromColumn = FindColumnIndex(anchor);
        if (fromColumn < 0 || fromColumn == targetColumnIndex) return false;

        var targetColumn = columns[targetColumnIndex];
        if (targetColumn.Count == 0) return true;

        var top = targetColumn[targetColumn.Count - 1];
        return anchor.suit == top.suit && anchor.rank == top.rank - 1;
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
}
