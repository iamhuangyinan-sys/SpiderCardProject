using System.Collections.Generic;
using Framework.Store;

/// <summary>
/// 卡牌数据层 —— 保存场上各列的卡牌、发牌堆
/// </summary>
public class CardsStore : StoreBase<CardsStore>
{
    /// <summary>场上列数</summary>
    public const int ColumnCount = 10;

    /// <summary>发牌堆（栈，从栈顶取牌）</summary>
    public Stack<CardData> drawPile = new Stack<CardData>();

    /// <summary>场上各列的卡牌（索引 = 列号）</summary>
    public List<List<CardData>> columns = new List<List<CardData>>();

    protected override void OnInit()
    {
        drawPile.Clear();
        columns.Clear();
        for (int i = 0; i < ColumnCount; i++)
        {
            columns.Add(new List<CardData>());
        }
    }

    protected override void OnDispose()
    {
        drawPile.Clear();
        columns.Clear();
    }

    /// <summary>清空发牌堆与各列数据（保留列结构）</summary>
    public void Clear()
    {
        drawPile.Clear();
        foreach (var column in columns)
        {
            column.Clear();
        }
    }

    /// <summary>通知数据变更（供 View 订阅刷新）</summary>
    public void Refresh() => NotifyDataChanged();
}
