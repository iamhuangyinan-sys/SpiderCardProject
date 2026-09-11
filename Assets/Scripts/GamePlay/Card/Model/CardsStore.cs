using System.Collections.Generic;
using Framework.Store;

/// <summary>
/// 卡牌数据层 —— 保存场上各列的卡牌、发牌堆
/// </summary>
public class CardsStore : StoreBase<CardsStore>
{
    /// <summary>场上列数（开始新游戏时设置）</summary>
    public int columnCount = 10;

    /// <summary>发牌堆（栈，从栈顶取牌）</summary>
    public Stack<CardData> drawPile = new Stack<CardData>();

    /// <summary>场上各列的卡牌（索引 = 列号）</summary>
    public List<List<CardData>> columns = new List<List<CardData>>();

    /// <summary>弃牌堆（已完成收走的 A-K 同花顺）</summary>
    public List<CardData> discardPile = new List<CardData>();

    /// <summary>牌包数量（开始新游戏时设置）</summary>
    public int pocketCount = 2;

    /// <summary>牌包（暂存区），每个位置存一张牌或 null</summary>
    public List<CardData> pockets = new List<CardData>();

    /// <summary>当前接龙次数（本局 A-K 顺子收集次数）</summary>
    public int straightCount;

    /// <summary>通关所需接龙次数（0 表示无通关要求）</summary>
    public int needStraightNum;

    protected override void OnInit()
    {
        drawPile.Clear();
        discardPile.Clear();
        ResetLayout();
    }

    /// <summary>按当前列数/牌包数重建列与牌包结构</summary>
    private void ResetLayout()
    {
        columns.Clear();
        for (int i = 0; i < columnCount; i++)
        {
            columns.Add(new List<CardData>());
        }

        pockets.Clear();
        for (int i = 0; i < pocketCount; i++)
        {
            pockets.Add(null);
        }
    }

    protected override void OnDispose()
    {
        drawPile.Clear();
        discardPile.Clear();
        pockets.Clear();
        columns.Clear();
    }

    /// <summary>清空发牌堆与各列数据，并按当前列数/牌包数重建结构</summary>
    public void Clear()
    {
        drawPile.Clear();
        discardPile.Clear();
        ResetLayout();
    }

    /// <summary>通知数据变更（供 View 订阅刷新）</summary>
    public void Refresh() => NotifyDataChanged();
}
