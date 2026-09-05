using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;
using TMPro;
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
    private const float ColumnSpacing = 1.7f;

    /// <summary>正面牌（翻开）的向下间隔</summary>
    private const float FaceUpSpacing = 0.7f;

    /// <summary>反面牌（牌背）的向下间隔</summary>
    private const float FaceDownSpacing = 0.3f;

    // ============ View 管理 ============

    /// <summary>CardData → CardView 反向索引</summary>
    private readonly Dictionary<CardData, CardView> _viewDict = new();

    /// <summary>空列垫底图（CardEmpty 的子物体，按列顺序）</summary>
    private readonly List<GameObject> _emptySlots = new();

    /// <summary>发牌堆剩余数量文本</summary>
    private TMP_Text _drawPileCountText;

    protected override void OnInit()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnTableChanged, OnTableChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnColumnChanged, OnColumnChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnColumnAppend, OnColumnAppend);
        EventManager.Instance.AddListener<CardData>(E_EventEnum.OnCardChanged, OnCardChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnDrawPileChanged, OnDrawPileChanged);

        FindEmptySlots();
        FindDrawPileCountText();
    }

    protected override void OnDispose()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnTableChanged, OnTableChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnColumnChanged, OnColumnChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnColumnAppend, OnColumnAppend);
        EventManager.Instance.RemoveListener<CardData>(E_EventEnum.OnCardChanged, OnCardChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnDrawPileChanged, OnDrawPileChanged);

        ClearAllViews();
        _emptySlots.Clear();
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

    /// <summary>某列末尾增量追加一张（发牌用，不重建整列）</summary>
    private void OnColumnAppend(int columnIndex)
    {
        AppendCardToColumn(columnIndex);
    }

    /// <summary>单张牌变化（翻牌等）：刷新该牌表现</summary>
    private void OnCardChanged(CardData cardData)
    {
        if (_viewDict.TryGetValue(cardData, out var view))
        {
            view.Refresh();
        }
    }

    /// <summary>发牌堆数量变化：更新剩余数量文本</summary>
    private void OnDrawPileChanged(int count)
    {
        if (_drawPileCountText != null)
        {
            _drawPileCountText.text = count.ToString();
        }
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
                view.SetPosition(GetCardPosition(col, column, row));
                view.SetSortingOrder(GetSortingOrder(col, row));
                _viewDict[cardData] = view;
            }
        }

        RefreshEmptySlots();
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
            view.SetPosition(GetCardPosition(columnIndex, column, row));
            view.SetSortingOrder(GetSortingOrder(columnIndex, row));
            _viewDict[cardData] = view;
        }

        RefreshEmptySlots();
    }

    /// <summary>在指定列末尾增量添加一张牌的 View（不回收整列）</summary>
    private void AppendCardToColumn(int columnIndex)
    {
        var store = CardsStore.Instance;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) return;

        var column = store.columns[columnIndex];
        if (column.Count == 0) return;

        int row = column.Count - 1;
        var cardData = column[row];
        if (_viewDict.ContainsKey(cardData)) return;

        var view = CardPoolManager.Instance.GetCard();
        if (view == null) return;

        view.Bind(cardData);
        view.SetPosition(GetCardPosition(columnIndex, column, row));
        view.SetSortingOrder(GetSortingOrder(columnIndex, row));
        _viewDict[cardData] = view;
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

    /// <summary>按数据查找对应的 View（不存在返回 null）</summary>
    public CardView GetView(CardData cardData)
    {
        return _viewDict.TryGetValue(cardData, out var view) ? view : null;
    }

    /// <summary>在场景中查找 CardEmpty 及其垫底子物体</summary>
    private void FindEmptySlots()
    {
        _emptySlots.Clear();

        var cardEmpty = GameObject.Find("CardEmpty");
        if (cardEmpty == null)
        {
            Debug.LogWarning("[CardViewManager] 场景中未找到 CardEmpty");
            return;
        }

        foreach (Transform child in cardEmpty.transform)
        {
            _emptySlots.Add(child.gameObject);
        }
    }

    /// <summary>按列数显示前 N 个垫底图（垫底图一直显示，非空列的自然被牌盖住）</summary>
    private void RefreshEmptySlots()
    {
        int columnCount = CardsStore.Instance.columns.Count;
        for (int i = 0; i < _emptySlots.Count; i++)
        {
            _emptySlots[i].SetActive(i < columnCount);
        }
    }

    /// <summary>在场景中查找发牌堆数量文本</summary>
    private void FindDrawPileCountText()
    {
        var go = GameObject.Find("DrawPileCountText");
        if (go != null)
        {
            _drawPileCountText = go.GetComponent<TMP_Text>();
        }

        if (_drawPileCountText == null)
        {
            Debug.LogWarning("[CardViewManager] 场景中未找到 DrawPileCountText");
        }
    }

    /// <summary>根据世界坐标判定落点列索引（越界返回 -1）</summary>
    public int GetColumnIndexAt(Vector3 worldPos)
    {
        int col = Mathf.RoundToInt((worldPos.x - _startPos.x) / ColumnSpacing);
        if (col < 0 || col >= CardsStore.ColumnCount) return -1;
        return col;
    }

    /// <summary>计算卡牌世界坐标：向右为列，向下为行；列内间隔取决于上面那张牌（翻开 0.7、牌背 0.3）</summary>
    private Vector3 GetCardPosition(int columnIndex, List<CardData> column, int rowIndex)
    {
        float x = _startPos.x + columnIndex * ColumnSpacing;

        // 从列顶往下累加：间隔取决于上面那张牌是翻开还是牌背
        float y = _startPos.y;
        for (int i = 1; i <= rowIndex; i++)
        {
            float spacing = column[i - 1].isFaceUp ? FaceUpSpacing : FaceDownSpacing;
            y -= spacing;
        }

        return new Vector3(x, y, _startPos.z);
    }

    /// <summary>计算渲染排序：同列内越靠上的牌排序值越大，渲染越靠前</summary>
    private static int GetSortingOrder(int columnIndex, int rowIndex)
    {
        return rowIndex * 100 + columnIndex;
    }
}
