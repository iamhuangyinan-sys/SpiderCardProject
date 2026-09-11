using System.Collections.Generic;
using DG.Tweening;
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

    /// <summary>正面牌（翻开）的向下间隔（投影拉伸计算用）</summary>
    public const float FaceUpSpacing = 0.65f;

    /// <summary>反面牌（牌背）的向下间隔</summary>
    private const float FaceDownSpacing = 0.3f;

    // ============ View 管理 ============

    /// <summary>CardData → CardView 反向索引</summary>
    private readonly Dictionary<CardData, CardView> _viewDict = new();

    /// <summary>空列垫底图（CardEmpty 的子物体，按列顺序）</summary>
    private readonly List<GameObject> _emptySlots = new();

    /// <summary>发牌堆剩余数量文本</summary>
    private TMP_Text _drawPileCountText;

    /// <summary>弃牌堆数量文本</summary>
    private TMP_Text _discardPileCountText;

    /// <summary>牌包命中半径</summary>
    private const float PocketHitRadius = 1f;

    /// <summary>牌包牌的渲染排序</summary>
    private const int PocketSortingOrder = 500;

    /// <summary>牌包空槽物体（PocketEmpty 的子物体）</summary>
    private readonly List<GameObject> _pocketSlots = new();

    /// <summary>牌包空槽世界坐标</summary>
    private readonly List<Vector3> _pocketPositions = new();

    /// <summary>每个牌包的牌 View（null = 空）</summary>
    private readonly List<CardView> _pocketViews = new();

    /// <summary>是否正在播放动画（动画期间锁定输入）</summary>
    public bool IsAnimating { get; private set; }

    /// <summary>发牌堆 Transform（发牌动画起点）</summary>
    private Transform _drawPileTrans;

    /// <summary>弃牌堆 Transform（回收动画终点）</summary>
    private Transform _discardPileTrans;

    /// <summary>进行中的飞行动画数量（发牌/回收，减到 0 解锁输入）</summary>
    private int _animatingCount;

    protected override void OnInit()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnTableChanged, OnTableChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnColumnChanged, OnColumnChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnColumnAppend, OnColumnAppend);
        EventManager.Instance.AddListener<CardData>(E_EventEnum.OnCardChanged, OnCardChanged);
        EventManager.Instance.AddListener<CardData>(E_EventEnum.OnCardToDiscard, OnCardToDiscard);
        EventManager.Instance.AddListener(E_EventEnum.OnShuffleBack, OnShuffleBack);
        EventManager.Instance.AddListener(E_EventEnum.OnDiscardToDrawPile, OnDiscardToDrawPile);
        EventManager.Instance.AddListener<CardData>(E_EventEnum.OnCardToDrawPile, OnCardToDrawPile);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnDrawPileChanged, OnDrawPileChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnDiscardPileChanged, OnDiscardPileChanged);
        EventManager.Instance.AddListener<int>(E_EventEnum.OnPocketChanged, OnPocketChanged);

        FindEmptySlots();
        FindDrawPileCountText();
        FindDiscardPileCountText();
        FindPocketSlots();
        FindDrawPile();
        FindDiscardPile();
    }

    protected override void OnDispose()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnTableChanged, OnTableChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnColumnChanged, OnColumnChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnColumnAppend, OnColumnAppend);
        EventManager.Instance.RemoveListener<CardData>(E_EventEnum.OnCardChanged, OnCardChanged);
        EventManager.Instance.RemoveListener<CardData>(E_EventEnum.OnCardToDiscard, OnCardToDiscard);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShuffleBack, OnShuffleBack);
        EventManager.Instance.RemoveListener(E_EventEnum.OnDiscardToDrawPile, OnDiscardToDrawPile);
        EventManager.Instance.RemoveListener<CardData>(E_EventEnum.OnCardToDrawPile, OnCardToDrawPile);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnDrawPileChanged, OnDrawPileChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnDiscardPileChanged, OnDiscardPileChanged);
        EventManager.Instance.RemoveListener<int>(E_EventEnum.OnPocketChanged, OnPocketChanged);

        ClearAllViews();
        _emptySlots.Clear();

        _animatingCount = 0;
        IsAnimating = false;
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

    /// <summary>单张牌变化（翻牌）：播放翻牌动画</summary>
    private void OnCardChanged(CardData cardData)
    {
        if (!_viewDict.TryGetValue(cardData, out var view)) return;

        // 计算该牌的正常排序（翻牌结束后应恢复的层级，避免读到飞行中的临时抬升值）
        int finalOrder = GetNormalSortingOrder(cardData, view);
        CardAnimationHelper.FlipUp(view, finalOrder);
    }

    /// <summary>计算一张牌在列中的正常渲染排序（不在列则保持当前）</summary>
    private int GetNormalSortingOrder(CardData cardData, CardView view)
    {
        if (view.columnIndex < 0) return view.CurrentSortingOrder;

        var column = CardsStore.Instance.columns[view.columnIndex];
        int row = column.IndexOf(cardData);
        return row >= 0 ? GetSortingOrder(view.columnIndex, row) : view.CurrentSortingOrder;
    }

    /// <summary>单张牌回收：从当前位置飞到弃牌堆，落地后回收 View</summary>
    private void OnCardToDiscard(CardData cardData)
    {
        if (!_viewDict.TryGetValue(cardData, out var view)) return;
        _viewDict.Remove(cardData);

        FlyViewAndRecycle(view, view.transform.position, GetDiscardPilePosition());
    }

    /// <summary>弃牌堆洗回：临时一张牌背从弃牌堆飞到发牌堆，落地回收</summary>
    private void OnShuffleBack()
    {
        PlayCardBackToDrawPile(GetDiscardPilePosition());
    }

    /// <summary>结算收牌：弃牌堆整堆收回发牌堆（用一张牌背代表，与洗回动画一致）</summary>
    private void OnDiscardToDrawPile()
    {
        PlayCardBackToDrawPile(GetDiscardPilePosition());
    }

    /// <summary>结算收牌：单张牌本体收回发牌堆（场上列 / 牌包的牌）</summary>
    private void OnCardToDrawPile(CardData cardData)
    {
        var view = TakeViewOf(cardData);
        if (view == null) return;

        FlyViewAndRecycle(view, view.transform.position, GetDrawPilePosition());
    }

    /// <summary>取出某张牌的 View 并解除持有（场上列优先，其次牌包），不存在返回 null</summary>
    private CardView TakeViewOf(CardData cardData)
    {
        if (_viewDict.TryGetValue(cardData, out var view))
        {
            _viewDict.Remove(cardData);
            return view;
        }

        for (int i = 0; i < _pocketViews.Count; i++)
        {
            var pocketView = _pocketViews[i];
            if (pocketView == null || pocketView.data != cardData) continue;

            _pocketViews[i] = null;
            return pocketView;
        }

        return null;
    }

    /// <summary>一张牌从 from 飞到 to，落地后回收 View（弃牌堆 / 发牌堆共用），飞行期间锁输入</summary>
    private void FlyViewAndRecycle(CardView view, Vector3 from, Vector3 to)
    {
        var tween = CardAnimationHelper.FlyTo(view, from, to);

        _animatingCount++;
        IsAnimating = true;
        tween.OnComplete(() =>
        {
            _animatingCount--;
            if (_animatingCount <= 0)
            {
                _animatingCount = 0;
                IsAnimating = false;
            }
            CardPoolManager.Instance.Recycle(view);
        });
    }

    /// <summary>播放「一张牌背从 fromPos 飞到发牌堆」动画（洗回 / 弃牌堆收牌共用）</summary>
    private void PlayCardBackToDrawPile(Vector3 fromPos)
    {
        var view = CardPoolManager.Instance.GetCard();
        if (view == null) return;

        view.ShowBack();
        FlyViewAndRecycle(view, fromPos, GetDrawPilePosition());
    }

    /// <summary>发牌堆数量变化：更新剩余数量文本</summary>
    private void OnDrawPileChanged(int count)
    {
        if (_drawPileCountText != null)
        {
            _drawPileCountText.text = count.ToString();
        }
    }

    /// <summary>弃牌堆数量变化：更新数量文本</summary>
    private void OnDiscardPileChanged(int count)
    {
        if (_discardPileCountText != null)
        {
            _discardPileCountText.text = count.ToString();
        }
    }

    /// <summary>牌包变化：刷新该牌包的牌显示</summary>
    private void OnPocketChanged(int pocketIndex)
    {
        RefreshPocket(pocketIndex);
    }

    // ============ 内部 ============

    /// <summary>根据当前数据全量创建 View（直接摆位，不做动画）</summary>
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
                view.columnIndex = col;
                view.SetPosition(GetCardPosition(col, column, row));
                view.SetSortingOrder(GetSortingOrder(col, row));
                _viewDict[cardData] = view;
            }
        }

        RefreshEmptySlots();
        RefreshPocketSlots();
    }

    /// <summary>在场景中查找发牌堆（挂 DrawPileController 的物体）</summary>
    private void FindDrawPile()
    {
        var controller = Object.FindAnyObjectByType<DrawPileController>();
        if (controller != null)
        {
            _drawPileTrans = controller.transform;
        }
        else
        {
            Debug.LogWarning("[CardViewManager] 场景中未找到 DrawPileController，发牌动画将使用默认起点");
        }
    }

    /// <summary>发牌堆世界坐标（未找到则用初始生成点）</summary>
    private Vector3 GetDrawPilePosition()
    {
        return _drawPileTrans != null ? _drawPileTrans.position : _startPos;
    }

    /// <summary>在场景中查找弃牌堆（回收动画终点）</summary>
    private void FindDiscardPile()
    {
        var go = GameObject.Find("DiscardPile");
        if (go != null)
        {
            _discardPileTrans = go.transform;
        }
        else
        {
            Debug.LogWarning("[CardViewManager] 场景中未找到 DiscardPile，回收动画将使用默认位置");
        }
    }

    /// <summary>弃牌堆世界坐标（未找到则用初始生成点）</summary>
    private Vector3 GetDiscardPilePosition()
    {
        return _discardPileTrans != null ? _discardPileTrans.position : _startPos;
    }

    /// <summary>回收某列的所有 View 并重建该列</summary>
    private void RebuildColumn(int columnIndex)
    {
        var store = CardsStore.Instance;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) return;

        var column = store.columns[columnIndex];

        // 回收该列所有 View（按列归属找，包括已从数据移除的牌）
        var toRemove = new List<CardData>();
        foreach (var kv in _viewDict)
        {
            if (kv.Value != null && kv.Value.columnIndex == columnIndex)
            {
                CardPoolManager.Instance.Recycle(kv.Value);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _viewDict.Remove(key);
        }

        // 重建该列
        for (int row = 0; row < column.Count; row++)
        {
            var cardData = column[row];
            var view = CardPoolManager.Instance.GetCard();
            if (view == null) continue;

            view.Bind(cardData);
            view.columnIndex = columnIndex;
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
        view.columnIndex = columnIndex;

        // 发牌动画：从发牌堆飞到该列顶部，飞行期间锁定输入，落地恢复层级并解锁
        int finalOrder = GetSortingOrder(columnIndex, row);
        Vector3 target = GetCardPosition(columnIndex, column, row);
        view.SetSortingOrder(finalOrder);
        var tween = CardAnimationHelper.FlyTo(view, GetDrawPilePosition(), target);

        _animatingCount++;
        IsAnimating = true;
        tween.OnComplete(() =>
        {
            view.SetSortingOrder(finalOrder);
            _animatingCount--;
            if (_animatingCount <= 0)
            {
                _animatingCount = 0;
                IsAnimating = false;
            }
        });

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

        // 回收牌包 View
        for (int i = 0; i < _pocketViews.Count; i++)
        {
            if (_pocketViews[i] != null)
            {
                CardPoolManager.Instance.Recycle(_pocketViews[i]);
                _pocketViews[i] = null;
            }
        }
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

    /// <summary>在场景中查找弃牌堆数量文本</summary>
    private void FindDiscardPileCountText()
    {
        var go = GameObject.Find("DiscardPileCountText");
        if (go != null)
        {
            _discardPileCountText = go.GetComponent<TMP_Text>();
        }

        if (_discardPileCountText == null)
        {
            Debug.LogWarning("[CardViewManager] 场景中未找到 DiscardPileCountText");
        }
    }

    /// <summary>在场景中查找 PocketEmpty 及其空槽子物体</summary>
    private void FindPocketSlots()
    {
        _pocketSlots.Clear();
        _pocketPositions.Clear();

        var pocketEmpty = GameObject.Find("PocketEmpty");
        if (pocketEmpty == null)
        {
            Debug.LogWarning("[CardViewManager] 场景中未找到 PocketEmpty");
            return;
        }

        foreach (Transform child in pocketEmpty.transform)
        {
            _pocketSlots.Add(child.gameObject);
            _pocketPositions.Add(child.position);
        }

        // 初始化牌包 View 引用（数量 = 牌包数）
        _pocketViews.Clear();
        for (int i = 0; i < CardsStore.Instance.pockets.Count; i++)
        {
            _pocketViews.Add(null);
        }
    }

    /// <summary>按牌包数量显示前 N 个空槽，并同步牌包 View 列表数量</summary>
    private void RefreshPocketSlots()
    {
        int count = CardsStore.Instance.pockets.Count;
        for (int i = 0; i < _pocketSlots.Count; i++)
        {
            _pocketSlots[i].SetActive(i < count);
        }

        // 同步牌包 View 列表数量
        while (_pocketViews.Count < count)
        {
            _pocketViews.Add(null);
        }
        while (_pocketViews.Count > count)
        {
            var last = _pocketViews[_pocketViews.Count - 1];
            if (last != null) CardPoolManager.Instance.Recycle(last);
            _pocketViews.RemoveAt(_pocketViews.Count - 1);
        }
    }

    /// <summary>根据世界坐标判定最近的牌包索引（命中半径内），否则 -1</summary>
    public int GetPocketIndexAt(Vector3 worldPos)
    {
        int count = CardsStore.Instance.pockets.Count;
        int nearest = -1;
        float minDist = PocketHitRadius;
        for (int i = 0; i < count && i < _pocketPositions.Count; i++)
        {
            float d = Vector2.Distance(worldPos, _pocketPositions[i]);
            if (d < minDist)
            {
                minDist = d;
                nearest = i;
            }
        }
        return nearest;
    }

    /// <summary>刷新某个牌包的牌显示（有牌建 View 盖住空槽，无牌清空）</summary>
    private void RefreshPocket(int pocketIndex)
    {
        var store = CardsStore.Instance;
        if (pocketIndex < 0 || pocketIndex >= store.pockets.Count) return;
        if (pocketIndex >= _pocketViews.Count) return;

        // 回收旧 View
        if (_pocketViews[pocketIndex] != null)
        {
            CardPoolManager.Instance.Recycle(_pocketViews[pocketIndex]);
            _pocketViews[pocketIndex] = null;
        }

        // 有牌则新建 View
        var card = store.pockets[pocketIndex];
        if (card != null)
        {
            var view = CardPoolManager.Instance.GetCard();
            if (view != null)
            {
                view.Bind(card);
                view.columnIndex = -1;
                view.SetPosition(GetPocketPosition(pocketIndex));
                view.SetSortingOrder(PocketSortingOrder);
                _pocketViews[pocketIndex] = view;
            }
        }
    }

    /// <summary>获取牌包的世界坐标</summary>
    private Vector3 GetPocketPosition(int pocketIndex)
    {
        if (pocketIndex < 0 || pocketIndex >= _pocketPositions.Count) return Vector3.zero;
        return _pocketPositions[pocketIndex];
    }

    /// <summary>根据世界坐标判定落点列索引（越界返回 -1）</summary>
    public int GetColumnIndexAt(Vector3 worldPos)
    {
        int col = Mathf.RoundToInt((worldPos.x - _startPos.x) / ColumnSpacing);
        if (col < 0 || col >= CardsStore.Instance.columnCount) return -1;
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
