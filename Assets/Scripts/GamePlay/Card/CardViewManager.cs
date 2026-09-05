using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;
using UnityEngine;

/// <summary>
/// 卡牌表现层管理器 —— 订阅事件，创建/摆放/回收所有 CardView
/// 数据层变化 → 派发事件 → 这里读数据并驱动表现（拉取式刷新）
/// </summary>
public class CardViewManager : ManagerBase<CardViewManager>
{
    // ============ 布局参数 ============

    /// <summary>初始生成点</summary>
    private readonly Vector3 _startPos = new Vector3(0f, 0f, 0f);

    /// <summary>列间隔（向右 +X）</summary>
    private const float ColumnSpacing = 2f;

    /// <summary>上下（行）间隔（向下 -Y）</summary>
    private const float RowSpacing = 0.3f;

    // ============ View 管理 ============

    /// <summary>CardData → CardView 反向索引</summary>
    private readonly Dictionary<CardData, CardView> _viewDict = new();

    protected override void OnInit()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnTableChanged, OnTableChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnColumnChanged, OnColumnChanged);
        EventManager.Instance.AddListener<CardData>(E_EventEnum.OnCardChanged, OnCardChanged);
        EventManager.Instance.AddListener(E_EventEnum.OnDrawPileChanged, OnDrawPileChanged);
    }

    protected override void OnDispose()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnTableChanged, OnTableChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnColumnChanged, OnColumnChanged);
        EventManager.Instance.RemoveListener<CardData>(E_EventEnum.OnCardChanged, OnCardChanged);
        EventManager.Instance.RemoveListener(E_EventEnum.OnDrawPileChanged, OnDrawPileChanged);

        ClearAllViews();
    }

    // ============ 事件回调 ============

    /// <summary>整桌刷新：回收旧 View，按当前数据重建</summary>
    private void OnTableChanged()
    {
        ClearAllViews();
        RebuildAll();
    }

    /// <summary>局部刷新某列</summary>
    private void OnColumnChanged(int columnIndex)
    {
        RebuildColumn(columnIndex);
    }

    /// <summary>单张牌变化（翻牌等）：刷新该牌表现</summary>
    private void OnCardChanged(CardData cardData)
    {
        if (_viewDict.TryGetValue(cardData, out var view))
        {
            view.Refresh();
        }
    }

    /// <summary>发牌堆变化（TODO：发牌堆渲染尚未实现）</summary>
    private void OnDrawPileChanged()
    {
        // TODO: 渲染发牌堆（牌背堆叠）
    }

    // ============ 内部 ============

    /// <summary>根据当前数据全量创建 View</summary>
    private void RebuildAll()
    {
        var store = CardsStore.Instance;

        for (int col = 0; col < store.columns.Count; col++)
        {
            var column = store.columns[col];
            for (int row = 0; row < column.Count; row++)
            {
                var cardData = column[row];
                var view = CardPoolManager.Instance.GetCard();
                if (view == null) continue;

                view.Bind(cardData);
                view.SetPosition(GetCardPosition(col, row));
                view.SetSortingOrder(GetSortingOrder(col, row));
                _viewDict[cardData] = view;
            }
        }
    }

    /// <summary>回收某列的 View 并重建该列</summary>
    private void RebuildColumn(int columnIndex)
    {
        var store = CardsStore.Instance;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) return;

        var column = store.columns[columnIndex];

        // 回收该列现有 View
        foreach (var cardData in column)
        {
            if (_viewDict.TryGetValue(cardData, out var view))
            {
                CardPoolManager.Instance.Recycle(view);
                _viewDict.Remove(cardData);
            }
        }

        // 重建该列
        for (int row = 0; row < column.Count; row++)
        {
            var cardData = column[row];
            var view = CardPoolManager.Instance.GetCard();
            if (view == null) continue;

            view.Bind(cardData);
            view.SetPosition(GetCardPosition(columnIndex, row));
            view.SetSortingOrder(GetSortingOrder(columnIndex, row));
            _viewDict[cardData] = view;
        }
    }

    /// <summary>回收所有 View 并清空索引</summary>
    private void ClearAllViews()
    {
        foreach (var view in _viewDict.Values)
        {
            CardPoolManager.Instance.Recycle(view);
        }
        _viewDict.Clear();
    }

    /// <summary>计算卡牌世界坐标：向右为列，向下为行，列内从下到上叠放</summary>
    private Vector3 GetCardPosition(int columnIndex, int rowIndex)
    {
        float x = _startPos.x + columnIndex * ColumnSpacing;
        float y = _startPos.y - rowIndex * RowSpacing;
        return new Vector3(x, y, _startPos.z);
    }

    /// <summary>计算渲染排序：同列内越靠上的牌排序值越大，渲染越靠前</summary>
    private static int GetSortingOrder(int columnIndex, int rowIndex)
    {
        return rowIndex * 100 + columnIndex;
    }
}
