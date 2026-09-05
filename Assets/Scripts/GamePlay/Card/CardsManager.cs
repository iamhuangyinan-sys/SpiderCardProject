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

    protected override void OnInit()
    {
        var deck = CreateDeck();   // 1. 组牌
        Shuffle(deck);             // 2. 洗牌
        StoreToDrawPile(deck);     // 3. 压入发牌堆
        Deal();                    // 4. 发牌
    }

    /// <summary>生成一副牌：4 花色 × A-K = 52 张，默认牌背朝上</summary>
    private List<CardData> CreateDeck()
    {
        var deck = new List<CardData>(52);
        foreach (E_CardSuitEnum suit in Enum.GetValues(typeof(E_CardSuitEnum)))
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                deck.Add(new CardData
                {
                    suit = suit,
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
    }
}
