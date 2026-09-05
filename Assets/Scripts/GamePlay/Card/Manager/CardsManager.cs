using System;
using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 卡牌业务逻辑层 —— 负责组牌、洗牌、发牌等场景所有纸牌逻辑
/// </summary>
public class CardsManager : ManagerBase<CardsManager>
{
    private readonly Random _random = new Random();

    /// <summary>测试模式：跳过洗牌直接发牌（仅编辑器下生效）</summary>
    public bool isTestMode = true;

    /// <summary>开始一局新游戏：清空数据 → 组牌 → 洗牌 → 发牌</summary>
    public void StartNewGame()
    {
        var store = CardsStore.Instance;
        store.Clear();              // 1. 清空旧数据

        var deck = CreateDeck();    // 2. 组牌

#if UNITY_EDITOR
        if (!isTestMode)
        {
            Shuffle(deck);          // 3. 洗牌（测试模式下跳过）
        }
#else
        Shuffle(deck);              // 3. 洗牌
#endif

        StoreToDrawPile(deck);      // 4. 压入发牌堆
        Deal();                     // 5. 发牌
    }

    /// <summary>生成一副牌：全红桃 A-K，4 组 = 52 张（测试用），默认牌背朝上</summary>
    private List<CardData> CreateDeck()
    {
        var deck = new List<CardData>(52);
        for (int group = 0; group < 4; group++)
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                deck.Add(new CardData
                {
                    suit = E_CardSuitEnum.Hearts,
                    rank = rank,
                    isFaceUp = false,
                });
            }
        }
        return deck;
    }

    /// <summary>Fisher-Yates 洗牌（O(n)，等概率生成任意排列）</summary>
    private void Shuffle(List<CardData> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    /// <summary>把一副牌压入发牌堆（栈）</summary>
    private void StoreToDrawPile(List<CardData> deck)
    {
        var store = CardsStore.Instance;
        store.drawPile.Clear();
        foreach (var card in deck)
        {
            store.drawPile.Push(card);
        }
    }

    /// <summary>发牌：10 列每列 3 张（轮流发），每列末张翻开</summary>
    private void Deal()
    {
        var store = CardsStore.Instance;

        foreach (var column in store.columns)
        {
            column.Clear();
        }

        const int dealCount = CardsStore.ColumnCount * 3;
        for (int i = 0; i < dealCount; i++)
        {
            var card = store.drawPile.Pop();
            store.columns[i % CardsStore.ColumnCount].Add(card);
        }

        foreach (var column in store.columns)
        {
            if (column.Count > 0)
            {
                column[column.Count - 1].isFaceUp = true;
            }
        }

        // 派发事件，渲染层据此创建/刷新 CardView
        EventManager.Instance.Dispatch(E_EventEnum.OnTableChanged);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);
    }

    /// <summary>从发牌堆给每列顶部发一张牌（翻开），发完为止</summary>
    public void DealFromDrawPile()
    {
        if (!CardRuleManager.Instance.CanDeal()) return;

        var store = CardsStore.Instance;

        for (int col = 0; col < store.columns.Count; col++)
        {
            if (store.drawPile.Count == 0) break;

            var card = store.drawPile.Pop();
            card.isFaceUp = true;
            store.columns[col].Add(card);

            // 该列末尾增量追加一张
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnAppend, col);
        }

        // 发牌堆数量变化
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);
    }

    /// <summary>
    /// 把一串牌从原列移动到目标列（调用前需先通过 CardRuleManager 判定）
    /// 移动后：原列若还有牌且新顶牌为反面，则翻开
    /// </summary>
    public void MoveCards(List<CardData> draggedCards, int targetColumnIndex)
    {
        var store = CardsStore.Instance;
        if (draggedCards == null || draggedCards.Count == 0) return;
        if (targetColumnIndex < 0 || targetColumnIndex >= store.columns.Count) return;

        // 找原列
        int fromColumnIndex = -1;
        for (int i = 0; i < store.columns.Count; i++)
        {
            if (store.columns[i].Contains(draggedCards[0]))
            {
                fromColumnIndex = i;
                break;
            }
        }

        if (fromColumnIndex < 0 || fromColumnIndex == targetColumnIndex) return;

        var fromColumn = store.columns[fromColumnIndex];
        var targetColumn = store.columns[targetColumnIndex];

        // 从原列移除被拖的牌
        foreach (var card in draggedCards)
        {
            fromColumn.Remove(card);
        }

        // 原列新顶牌若为反面，翻开
        if (fromColumn.Count > 0)
        {
            var newTop = fromColumn[fromColumn.Count - 1];
            if (!newTop.isFaceUp)
            {
                newTop.isFaceUp = true;
            }
        }

        // 加入目标列
        foreach (var card in draggedCards)
        {
            targetColumn.Add(card);
        }

        // 通知刷新两列
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, fromColumnIndex);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, targetColumnIndex);
    }
}
