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

    /// <summary>沉底概率：牌收入弃牌堆时被打上沉底标记的概率（后续可移入配表）</summary>
    private const float SunkProbability = 0.8f;

    /// <summary>测试模式：跳过洗牌直接发牌（仅编辑器下生效）</summary>
    public bool isTestMode = true;

    /// <summary>是否正在结算收牌（收牌期间锁输入、不再触发新的回收/洗回）</summary>
    public bool IsSettling { get; private set; }

    /// <summary>是否正在一局对局中（发牌开始 → 收牌结算结束）</summary>
    public bool IsPlaying { get; private set; }

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

    /// <summary>按当前牌组（RunManager.Deck）生成一副牌，默认牌背朝上</summary>
    private List<CardData> CreateDeck()
    {
        var deck = new List<CardData>();

        foreach (var cardId in RunManager.Instance.Deck)
        {
            var cfg = ConfigHelper.Get<CardConfig>(cardId);
            if (cfg == null) continue;

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
            isAnyTarget = cfg.AnyTarget,   // 配表只给默认值，运行时还可被赋予（同 SingleGrab）
            placeSkill = (E_PlaceSkillEnum)cfg.PlaceSkill,
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
            PlaceDealtCard(col, card, faceUp: false);

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
        if (!CardRuleManager.Instance.CanDeal())
        {
            // 有空列时不能发牌（文案先写死，后续接提示表 / 语言表时改 TipHelper）
            TipHelper.Show("有空列时不能发牌");
            return;
        }

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
            PlaceDealtCard(col, card, faceUp: true);

            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
        }

        // 等最后一张落地，再检查顺子（顺子的回收动画与数据更新由 RecycleToDiscardRoutine 处理）
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

    /// <summary>
    /// 把一张刚发出的牌放进指定列（发牌落位的唯一入口）：
    /// 普通牌正面朝上放堆顶（OnColumnAppend 增量追加）；
    /// 沉底牌背面朝上插到堆底第 0 位（OnColumnPrepend 牌背飞入 + 其余牌下移）。
    /// 沉底标记只服务于「发到堆底」这一次，发出后即清除。
    /// </summary>
    private void PlaceDealtCard(int columnIndex, CardData card, bool faceUp)
    {
        var store = CardsStore.Instance;
        card.isFaceUp = faceUp;

        if (card.isSunk)
        {
            card.isSunk = false;
            card.isFaceUp = false;   // 沉底牌以牌背发出，埋进堆底（表面看不出是哪张）
            store.columns[columnIndex].Insert(0, card);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnPrepend, columnIndex);
            return;
        }

        store.columns[columnIndex].Add(card);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnAppend, columnIndex);
    }

    /// <summary>翻牌：置为正面并派发单张刷新（供表现层播放翻牌动画）</summary>
    private void FlipCardUp(CardData card)
    {
        if (card == null || card.isFaceUp) return;
        card.isFaceUp = true;
        EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardChanged, card);
    }

    /// <summary>
    /// 把一张牌收进弃牌堆（全项目唯一入口）：按概率打沉底标记 → 移入弃牌堆 → 派发表现事件。
    /// 顺子回收 / 回收技能 / 以后任何「牌进弃牌堆」都走这里，保证沉底规则只有一处
    /// </summary>
    private void TakeToDiscard(CardData card)
    {
        var store = CardsStore.Instance;

        // 沉底标记：进弃牌堆就按概率 roll（每次入堆重新 roll，发出时生效、发完即清）
        card.isSunk = _random.NextDouble() < SunkProbability;

        store.discardPile.Add(card);

        EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardToDiscard, card);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);
    }

    /// <summary>
    /// 从某列堆顶往下逐张收进弃牌堆（顺子回收 / 回收技能共用）：
    /// 数据逐张同步推进并播飞行动画，全部落地后统一收尾（重建列 → 翻新顶牌 → 复判顺子 → 洗回检查）
    /// </summary>
    /// <param name="columnIndex">目标列</param>
    /// <param name="count">收几张（从堆顶往下数）</param>
    /// <param name="countAsStraight">是否计入接龙次数（顺子回收 = true，回收技能 = false）</param>
    private IEnumerator RecycleFromColumnRoutine(int columnIndex, int count, bool countAsStraight)
    {
        var store = CardsStore.Instance;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) yield break;

        var column = store.columns[columnIndex];

        // 逐张从堆顶往下收
        for (int i = 0; i < count; i++)
        {
            if (column.Count == 0) break;

            var card = column[column.Count - 1];
            column.RemoveAt(column.Count - 1);
            TakeToDiscard(card);

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

        if (countAsStraight)
        {
            // 接龙次数 +1：等回收动画播完才增加，进度面板此时才刷新
            store.straightCount++;
            EventManager.Instance.Dispatch(E_EventEnum.OnStraightCountChanged);

            // 达标 → 关卡完成 + 收牌结算（弃牌堆连同刚收的顺子一起飞回发牌堆）
            if (CheckWin())
            {
                StartCollectAll();
                yield break;
            }
        }
        else if (CheckAndDiscard(columnIndex))
        {
            // 回收技能：把堆顶的牌撑走后，这列可能才凑成顺子
            // （后续含洗回交给顺子回收协程收尾）
            yield break;
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

        // 目标列：顺子优先；没触发顺子才看落牌技能
        if (!CheckAndDiscard(targetColumnIndex))
        {
            TryApplyPlaceSkill(targetColumnIndex);
        }
    }

    // ==================== 落牌技能 ====================

    /// <summary>
    /// 落牌技能：牌放下到列上之后触发（回收 / 复制）。
    /// 放在空列上不触发（放下后该列只有它一张，没有"下面的牌"）。
    /// </summary>
    private void TryApplyPlaceSkill(int columnIndex)
    {
        var store = CardsStore.Instance;
        if (IsSettling) return;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) return;

        var column = store.columns[columnIndex];
        if (column.Count < 2) return;   // 放在空列上：不触发

        var anchor = column[column.Count - 1];   // 刚放下的牌 = 堆顶
        if (anchor.placeSkill == E_PlaceSkillEnum.None) return;

        switch (anchor.placeSkill)
        {
            case E_PlaceSkillEnum.Recycle:
                // 自己 + 自己下面紧邻的 1 张一起进弃牌堆（不计接龙次数）
                MonoManager.Instance.StartCoroutine(RecycleFromColumnRoutine(columnIndex, 2, false));
                break;

            case E_PlaceSkillEnum.Copy:
                ApplyCopy(columnIndex, anchor);
                break;
        }
    }

    /// <summary>复制：把自己变成下面紧邻那张牌（复制其全部属性，含技能 → 复制牌可以继续复制）</summary>
    private void ApplyCopy(int columnIndex, CardData anchor)
    {
        var column = CardsStore.Instance.columns[columnIndex];
        int idx = column.IndexOf(anchor);
        if (idx <= 0) return;   // 下面没牌，没得复制

        anchor.CopyFrom(column[idx - 1]);

        // 单张刷新：表现层播翻牌动画，翻到中途 Refresh() 按新 id 换上新的牌面
        EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardChanged, anchor);

        // 复制来的牌可能是万能牌，这列可能刚好凑成顺子
        CheckAndDiscard(columnIndex);
    }

    /// <summary>检查某列是否形成 A-K 同花顺，是则播放回收动画（数据在动画播完后统一更新）</summary>
    private bool CheckAndDiscard(int columnIndex)
    {
        var store = CardsStore.Instance;
        if (IsSettling) return false;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) return false;

        var column = store.columns[columnIndex];
        var seq = CardRuleManager.Instance.GetCompletedSequence(column);
        if (seq == null) return false;

        // 先播回收动画：列数据、接龙计数、达标结算都在动画播完后统一处理
        MonoManager.Instance.StartCoroutine(RecycleFromColumnRoutine(columnIndex, seq.Count, countAsStraight: true));
        return true;
    }

    /// <summary>接龙次数达到要求则关卡完成（解锁下一批 + 挂起奖励；返回是否达标）</summary>
    private bool CheckWin()
    {
        var store = CardsStore.Instance;
        if (IsSettling) return false;
        if (store.needStraightNum <= 0) return false;
        if (store.straightCount < store.needStraightNum) return false;

        // 关卡完成：记录进度 + 解锁下一关 + 挂起奖励，奖励在结算动画结束后发放
        LevelManager.Instance.CompleteLevel(LevelStore.Instance.selectedLevelId);
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
        StartCollectAll();   // 测试接口：没有顺子回收动画，直接进结算
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

        // 结算动画已结束 → 发放本关奖励（与层号刷新同时机）
        LevelManager.Instance.SettleLevelReward();

        // 关卡进度 + 金币落盘（正在进行中的关不存牌局，重进后重开）
        CardGameModule.Instance.SaveRun();

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

        // 目标列：顺子优先；没触发顺子才看落牌技能
        if (!CheckAndDiscard(targetColumnIndex))
        {
            TryApplyPlaceSkill(targetColumnIndex);
        }
    }

    // ==================== 牌堆展示 ====================

    /// <summary>取发牌堆全部牌（按点数、花色排序，供展示用；带沉底标记）</summary>
    public List<CardData> GetDrawPileCards() => SortForDisplay(CardsStore.Instance.drawPile);

    /// <summary>取弃牌堆全部牌（按点数、花色排序，供展示用；带沉底标记）</summary>
    public List<CardData> GetDiscardPileCards() => SortForDisplay(CardsStore.Instance.discardPile);

    /// <summary>按点数（A→K）→ 花色 排序后返回（展示用，不改变原数据顺序）</summary>
    private static List<CardData> SortForDisplay(IEnumerable<CardData> cards)
    {
        var list = new List<CardData>(cards);
        list.Sort((a, b) =>
        {
            int byRank = a.rank.CompareTo(b.rank);
            return byRank != 0 ? byRank : a.suit.CompareTo(b.suit);
        });

        return list;
    }
}
