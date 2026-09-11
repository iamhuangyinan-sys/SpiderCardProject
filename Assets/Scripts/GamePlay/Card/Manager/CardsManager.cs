using System;
using System.Collections;
using System.Collections.Generic;
using Framework;
using Framework.Event;
using Framework.Mgr;
using UnityEngine;

/// <summary>
/// 卡牌业务逻辑层 —— 负责组牌、洗牌、发牌等场景所有纸牌逻辑
/// </summary>
public class CardsManager : ManagerBase<CardsManager>
{
    private readonly System.Random _random = new System.Random();

    /// <summary>测试模式：跳过洗牌直接发牌（仅编辑器下生效）</summary>
    public bool isTestMode = true;

    /// <summary>是否正在结算收牌（收牌期间锁输入、不再触发新的回收/洗回）</summary>
    public bool IsSettling { get; private set; }

    /// <summary>是否正在一局对局中（发牌开始 → 收牌结算结束）</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>进行中的回收协程（结算开始时会被中止）</summary>
    private Coroutine _recycleRoutine;

    /// <summary>开始一局新游戏：设置列数/牌包数 → 清空数据 → 组牌 → 洗牌 → 发牌</summary>
    public void StartNewGame(int columnCount, int pocketCount)
    {
        var store = CardsStore.Instance;

        // 读取当前关卡所需接龙次数，清零当前次数
        store.needStraightNum = LevelManager.Instance.GetSelectedNeedStraightNum();
        store.straightCount = 0;
        IsSettling = false;
        IsPlaying = true;

        // 设置本局列数与牌包数，再清空（重建结构）
        store.columnCount = columnCount;
        store.pocketCount = pocketCount;
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

        // 通知进度刷新（x / y）
        EventManager.Instance.Dispatch(E_EventEnum.OnStraightCountChanged);
    }

    /// <summary>按配表生成一副牌，默认牌背朝上</summary>
    private List<CardData> CreateDeck()
    {
        var deck = new List<CardData>();
        foreach (var cfg in ConfigHelper.GetAll<CardConfig>())
        {
            deck.Add(CreateCard(cfg));
        }
        
        return deck;
    }

    /// <summary>由配表数据创建一张牌</summary>
    private CardData CreateCard(CardConfig cfg)
    {
        return new CardData
        {
            id = cfg.Id,
            suit = (E_CardSuitEnum)cfg.Suit,
            rank = cfg.Rank,
            isFaceUp = false,
            isSingleGrab = cfg.SingleGrab,
        };
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

    /// <summary>发牌：每列轮流一张，逐张发出（发牌堆数量逐张减少）</summary>
    private void Deal()
    {
        MonoManager.Instance.StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        var store = CardsStore.Instance;

        // 清空各列，派发整桌刷新（空桌）
        foreach (var column in store.columns)
        {
            column.Clear();
        }
        EventManager.Instance.Dispatch(E_EventEnum.OnTableChanged);

        // 逐张发牌：每列轮流一张，某列发满 3 张时及时翻该列顶牌
        int perColumn = 3;
        int dealCount = store.columnCount * perColumn;
        for (int i = 0; i < dealCount; i++)
        {
            if (store.drawPile.Count == 0) break;

            var card = store.drawPile.Pop();
            int col = i % store.columnCount;
            store.columns[col].Add(card);

            EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnAppend, col);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);

            // 该列已发满：及时翻该列顶牌（翻牌动画与后续发牌重叠，不额外等待）
            if (store.columns[col].Count >= perColumn)
            {
                FlipCardUp(store.columns[col][store.columns[col].Count - 1]);
            }
        }

        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);
    }

    /// <summary>从发牌堆给每列顶部逐张发一张牌（翻开），发牌堆数量逐张减少</summary>
    public void DealFromDrawPile()
    {
        if (!CardRuleManager.Instance.CanDeal()) return;
        MonoManager.Instance.StartCoroutine(DealFromDrawPileRoutine());
    }

    private IEnumerator DealFromDrawPileRoutine()
    {
        var store = CardsStore.Instance;

        for (int col = 0; col < store.columns.Count; col++)
        {
            if (IsSettling) yield break;
            if (store.drawPile.Count == 0) break;

            var card = store.drawPile.Pop();
            card.isFaceUp = true;
            store.columns[col].Add(card);

            EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnAppend, col);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
        }

        // 等最后一张落地，再检查顺子（顺子回收由 RecycleToDiscard 异步完成）
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

        bool anyRecycled = false;
        for (int col = 0; col < store.columns.Count; col++)
        {
            if (IsSettling) break;
            if (CheckAndDiscard(col)) anyRecycled = true;
        }

        // 没有触发回收时，检查洗回（洗回动画与数量派发由洗回协程内部完成）
        if (!anyRecycled && !IsSettling)
        {
            TryShuffleBack();
        }
    }

    /// <summary>翻牌：置为正面并派发单张刷新（供表现层播放翻牌动画）</summary>
    private void FlipCardUp(CardData card)
    {
        if (card == null || card.isFaceUp) return;
        card.isFaceUp = true;
        EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardChanged, card);
    }

    /// <summary>回收某列顶部 count 张牌到弃牌堆（统一回收入口，逐张飞行动画）</summary>
    private void RecycleToDiscard(int columnIndex, int count)
    {
        if (IsSettling) return;

        // 同一时刻只允许一个回收协程（连点/连锁触发时以最后一次为准）
        if (_recycleRoutine != null) MonoManager.Instance.StopCoroutine(_recycleRoutine);
        _recycleRoutine = MonoManager.Instance.StartCoroutine(RecycleToDiscardRoutine(columnIndex, count));
    }

    private IEnumerator RecycleToDiscardRoutine(int columnIndex, int count)
    {
        var store = CardsStore.Instance;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) yield break;

        var column = store.columns[columnIndex];

        // 逐张从列顶（A）往下回收
        for (int i = 0; i < count; i++)
        {
            if (column.Count == 0) break;

            var card = column[column.Count - 1];
            column.RemoveAt(column.Count - 1);
            store.discardPile.Add(card);

            EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardToDiscard, card);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
        }

        // 等最后一张飞到弃牌堆
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

        // 整列重建（剩余牌重新摆位）
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, columnIndex);

        // 新顶牌若为反面，翻牌
        if (column.Count > 0)
        {
            FlipCardUp(column[column.Count - 1]);
        }

        // 发牌堆空了则把弃牌堆洗回（洗回动画与数量派发由洗回协程内部完成）
        TryShuffleBack();
    }

    /// <summary>
    /// 把一串牌从原列移动到目标列（调用前需先通过 CardRuleManager 判定）
    /// 移动后：原列若还有牌且新顶牌为反面，则翻开
    /// </summary>
    public void MoveCards(List<CardData> draggedCards, int targetColumnIndex)
    {
        var store = CardsStore.Instance;
        if (IsSettling) return;
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

        // 加入目标列
        foreach (var card in draggedCards)
        {
            targetColumn.Add(card);
        }

        // 先整列重建（原列新顶牌此时仍是背面）
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, fromColumnIndex);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, targetColumnIndex);

        // 检查顺子：触发回收则翻牌由回收协程收尾；否则原列翻新顶牌
        if (!CheckAndDiscard(fromColumnIndex) && fromColumn.Count > 0)
        {
            FlipCardUp(fromColumn[fromColumn.Count - 1]);
        }
        CheckAndDiscard(targetColumnIndex);
    }

    /// <summary>检查某列是否形成 A-K 同花顺，是则接龙次数 +1 并触发逐张回收动画</summary>
    private bool CheckAndDiscard(int columnIndex)
    {
        var store = CardsStore.Instance;
        if (IsSettling) return false;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) return false;

        var column = store.columns[columnIndex];
        var seq = CardRuleManager.Instance.GetCompletedSequence(column);
        if (seq == null) return false;

        // 接龙次数 +1，刷新进度，检查通关
        store.straightCount++;
        EventManager.Instance.Dispatch(E_EventEnum.OnStraightCountChanged);

        // 达标 → 关卡完成，改走收牌结算（这 13 张顺子连同场上其他牌一起收回发牌堆）
        if (CheckWin()) return true;

        // 触发逐张回收（异步）：统一回收入口，动画/翻牌/洗回都由它收尾
        RecycleToDiscard(columnIndex, seq.Count);
        return true;
    }

    /// <summary>接龙次数达到要求则关卡完成并开始收牌结算（返回是否已进入结算）</summary>
    private bool CheckWin()
    {
        var store = CardsStore.Instance;
        if (IsSettling) return true;
        if (store.needStraightNum <= 0) return false;
        if (store.straightCount < store.needStraightNum) return false;

        // 关卡完成：记录进度 + 解锁下一关
        LevelManager.Instance.CompleteLevel(LevelStore.Instance.selectedLevelId);

        // 收牌结算：全部牌收回发牌堆，收完派发 OnCardsCollected
        StartCollectAll();
        return true;
    }

    /// <summary>【测试】直接达成接龙次数 → 关卡完成 + 收牌结算（供后续测试使用）</summary>
    public void DebugCompleteLevel()
    {
        if (IsSettling) return;

        var store = CardsStore.Instance;
        if (store.needStraightNum <= 0) store.needStraightNum = 1;

        store.straightCount = store.needStraightNum;
        EventManager.Instance.Dispatch(E_EventEnum.OnStraightCountChanged);

        CheckWin();
    }

    // ==================== 结算收牌 ====================

    /// <summary>开始收牌结算：弃牌堆 → 场上各列 → 各牌包，全部收回发牌堆</summary>
    private void StartCollectAll()
    {
        if (IsSettling) return;
        IsSettling = true;
        MonoManager.Instance.StartCoroutine(CollectAllRoutine());
    }

    private IEnumerator CollectAllRoutine()
    {
        var store = CardsStore.Instance;

        // 中止进行中的回收协程，避免它与结算抢同一批牌
        if (_recycleRoutine != null)
        {
            MonoManager.Instance.StopCoroutine(_recycleRoutine);
            _recycleRoutine = null;
        }

        // 1. 弃牌堆 → 发牌堆（与洗回动画一致）
        if (store.discardPile.Count > 0)
        {
            EventManager.Instance.Dispatch(E_EventEnum.OnDiscardToDrawPile);
            yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

            Shuffle(store.discardPile);
            foreach (var card in store.discardPile)
            {
                store.drawPile.Push(card);
            }
            store.discardPile.Clear();

            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);
        }

        // 2. 场上各列 → 发牌堆（逐列、逐张：从列底往上飞本体，与顺子收进弃牌堆同一编排）
        // 列与列之间不等待，上一列最后一张起飞后紧接着发起下一列第一张，动画更连贯
        for (int col = 0; col < store.columns.Count; col++)
        {
            var column = store.columns[col];

            while (column.Count > 0)
            {
                var card = column[column.Count - 1];
                column.RemoveAt(column.Count - 1);
                store.drawPile.Push(card);

                EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardToDrawPile, card);
                EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

                yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
            }
        }

        // 3. 各牌包 → 发牌堆（逐包飞本体，同样不等前一张落地）
        for (int i = 0; i < store.pockets.Count; i++)
        {
            var card = store.pockets[i];
            if (card == null) continue;

            store.pockets[i] = null;
            store.drawPile.Push(card);

            EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardToDrawPile, card);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
        }

        // 4. 等最后一批牌落地，再解锁并通知结束（由 UI 层弹选关）
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

        IsSettling = false;
        IsPlaying = false;
        EventManager.Instance.Dispatch(E_EventEnum.OnCardsCollected);
    }

    /// <summary>发牌堆空了且弃牌堆有牌，播放洗回动画后把弃牌堆洗回发牌堆</summary>
    private void TryShuffleBack()
    {
        var store = CardsStore.Instance;
        if (IsSettling) return;
        if (store.drawPile.Count != 0 || store.discardPile.Count == 0) return;
        MonoManager.Instance.StartCoroutine(ShuffleBackRoutine());
    }

    private IEnumerator ShuffleBackRoutine()
    {
        var store = CardsStore.Instance;

        // 洗回动画：一张牌背从弃牌堆飞到发牌堆
        EventManager.Instance.Dispatch(E_EventEnum.OnShuffleBack);

        // 等动画播完再更新数据
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

        // 洗牌 + 全部压回发牌堆
        Shuffle(store.discardPile);
        foreach (var card in store.discardPile)
        {
            store.drawPile.Push(card);
        }
        store.discardPile.Clear();

        // 数量更新
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);
    }

    /// <summary>把一张牌移到指定牌包（暂存）</summary>
    public void MoveToPocket(CardData card, int pocketIndex)
    {
        var store = CardsStore.Instance;
        if (!CardRuleManager.Instance.CanMoveToPocket(card, pocketIndex)) return;

        // 从原列移除
        int fromColumn = CardRuleManager.Instance.FindColumnIndex(card);
        if (fromColumn >= 0)
        {
            store.columns[fromColumn].Remove(card);
        }

        card.isFaceUp = true;
        store.pockets[pocketIndex] = card;

        if (fromColumn >= 0)
        {
            // 先整列重建（原列新顶牌此时仍是背面）
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, fromColumn);

            // 原列新顶牌若为反面，播放翻牌动画
            var fromCol = store.columns[fromColumn];
            if (fromCol.Count > 0)
            {
                FlipCardUp(fromCol[fromCol.Count - 1]);
            }
        }
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnPocketChanged, pocketIndex);
    }

    /// <summary>从牌包把牌移到目标列</summary>
    public void MoveFromPocket(int pocketIndex, int targetColumnIndex)
    {
        var store = CardsStore.Instance;
        if (!CardRuleManager.Instance.CanMoveFromPocket(pocketIndex, targetColumnIndex)) return;

        var card = store.pockets[pocketIndex];
        store.pockets[pocketIndex] = null;
        card.isFaceUp = true;
        store.columns[targetColumnIndex].Add(card);

        EventManager.Instance.Dispatch<int>(E_EventEnum.OnPocketChanged, pocketIndex);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnChanged, targetColumnIndex);
    }
}
