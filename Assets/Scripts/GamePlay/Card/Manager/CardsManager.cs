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

    private readonly Queue<E_FlipSkillEnum> _flipSkills = new();
    private bool _dealing;
    private bool _resolvingFlipSkills;
    private bool _shufflingBack;
    private int _activeRecycles;

    public bool IsResolvingBoard => _dealing || _resolvingFlipSkills || _activeRecycles > 0 || _shufflingBack;

    /// <summary>牌收入弃牌堆时被打上沉底标记的概率。</summary>
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
        _flipSkills.Clear();
        _dealing = true;
        _washInQueue.Clear();   // 丢掉上一局残留的洗入队列

        // 设置本局列数与牌包数，再清空（重建结构）
        store.columnCount = columnCount;
        store.pocketCount = pocketCount;
        store.Clear();              // 1. 清空旧数据

        // 立刻把空桌摆出来（垫底图 / 空牌包槽数量按本关列数、牌包数）
        // 必须放在洗入 / 发牌之前：否则有开局事件时会等到事件动画结束、发牌那一刻才突然出现
        EventManager.Instance.Dispatch(E_EventEnum.OnTableChanged);

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

        // 数量文本按新牌局初始化（发牌前发牌堆就是满的、弃牌堆是空的）
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);

        // 5. 开局事件：有事件就先跑事件流程（具体做什么由 LevelEventRegistry 决定），跑完统一发牌
        var levelEventRoutine = LevelEventRegistry.Get(LevelManager.Instance.GetSelectedLevelEvent());
        if (levelEventRoutine == null)
        {
            Deal();                 // 6. 发牌
        }
        else
        {
            MonoManager.Instance.StartCoroutine(StartWithLevelEventRoutine(levelEventRoutine));
        }

        // 通知进度刷新（x / y）
        EventManager.Instance.Dispatch(E_EventEnum.OnStraightCountChanged);
    }

    /// <summary>按当前牌组（RunManager.Deck）生成一副牌，默认牌背朝上</summary>
    private List<CardData> CreateDeck()
    {
        var deck = new List<CardData>();

        foreach (var def in RunManager.Instance.Deck)
        {
            var card = CardFactory.Create(def);
            if (card == null) continue;

            deck.Add(card);
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

    /// <summary>发牌：每列轮流一张，逐张发出（发牌堆数量逐张减少）</summary>
    private void Deal()
    {
        MonoManager.Instance.StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        var store = CardsStore.Instance;

        // 逐张发牌：每列轮流一张，前几列少发、其余列多发；某列发满时及时翻该列顶牌
        int columnCount = store.columns.Count;
        var quota = LevelManager.Instance.GetInitialDeal(columnCount, store.drawPile.Count);
        int maxQuota = 0;
        for (int col = 0; col < columnCount; col++)
        {
            if (quota[col] > maxQuota) maxQuota = quota[col];
        }

        bool runOut = false;

        // 按轮发牌：一轮给每列各发一张，已发满的列跳过（保持原来的「逐排铺开」节奏）
        for (int round = 0; round < maxQuota && !runOut; round++)
        {
            for (int col = 0; col < columnCount; col++)
            {
                if (IsSettling)
                {
                    _dealing = false;
                    yield break;
                }
                if (round >= quota[col]) continue;

                if (store.drawPile.Count == 0)
                {
                    runOut = true;
                    break;
                }

                var card = store.drawPile.Pop();
                PlaceDealtCard(col, card, faceUp: false);

                EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

                yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);

                if (IsSettling)
                {
                    _dealing = false;
                    yield break;
                }

                // 该列已发满：及时翻该列顶牌（翻牌动画与后续发牌重叠，不额外等待）
                if (store.columns[col].Count >= quota[col])
                {
                    FlipCardUp(store.columns[col][store.columns[col].Count - 1]);
                }
            }
        }

        EventManager.Instance.Dispatch<int>(E_EventEnum.OnDiscardPileChanged, store.discardPile.Count);
        yield return new WaitForSeconds(Mathf.Max(CardAnimationHelper.FlyDuration, 2 * CardAnimationHelper.FlipDuration));
        _dealing = false;
    }

    /// <summary>从发牌堆给每列顶部逐张发一张牌（翻开），发牌堆数量逐张减少</summary>
    public void DealFromDrawPile()
    {
        if (IsSettling || IsResolvingBoard) return;
        if (CardsStore.Instance.drawPile.Count == 0)
        {
            TipHelper.Show(LanguageHelper.Get("tip_draw_empty"));
            return;
        }

        if (!CardRuleManager.Instance.CanDeal())
        {
            TipHelper.Show(LanguageHelper.Get("tip_deal_empty_column"));
            return;
        }

        _dealing = true;
        MonoManager.Instance.StartCoroutine(ResolveManualDealRoutine());
    }

    private IEnumerator ResolveManualDealRoutine()
    {
        yield return DealFromDrawPileRoutine();
        _dealing = false;
        TryShuffleBack();
    }

    private IEnumerator DealFromDrawPileRoutine()
    {
        var store = CardsStore.Instance;
        int dealtColumnCount = 0;

        for (; dealtColumnCount < store.columns.Count; dealtColumnCount++)
        {
            if (IsSettling) yield break;
            if (store.drawPile.Count == 0) break;

            var card = store.drawPile.Pop();
            PlaceDealtCard(dealtColumnCount, card, faceUp: true);

            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, store.drawPile.Count);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
        }

        // 等最后一张落地。沉底牌如果发到了空列，就是这一列的顶牌，需要翻开。
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);
        if (IsSettling) yield break;

        bool flippedCard = false;
        for (int col = 0; col < dealtColumnCount; col++)
        {
            var column = store.columns[col];
            if (column.Count == 1 && !column[0].isFaceUp)
            {
                FlipCardUp(column[0]);
                flippedCard = true;
            }
        }

        if (flippedCard) yield return new WaitForSeconds(2 * CardAnimationHelper.FlipDuration);

        // 翻面完成后检查顺子，回收动画与数据更新由 RecycleFromColumnRoutine 处理。
        for (int col = 0; col < store.columns.Count; col++)
        {
            if (IsSettling) break;
            CheckAndDiscard(col);
        }
    }

    /// <summary>
    /// 把一张刚发出的牌放进指定列（发牌落位的唯一入口）：
    /// 普通牌正面朝上放堆顶（OnColumnAppend 增量追加）；
    /// 带沉底属性的牌背面朝上插到堆底第 0 位（OnColumnPrepend 牌背飞入 + 其余牌下移）。
    /// 沉底属性只服务于「发到堆底」这一次，发出后即清除。
    /// </summary>
    private void PlaceDealtCard(int columnIndex, CardData card, bool faceUp)
    {
        var store = CardsStore.Instance;
        card.isFaceUp = faceUp;

        if (card.HasAttribute(E_CardAttributeEnum.Sunk))
        {
            card.SetAttribute(E_CardAttributeEnum.Sunk, false);
            card.isFaceUp = false;   // 沉底牌以牌背发出，埋进堆底（表面看不出是哪张）
            store.columns[columnIndex].Insert(0, card);
            EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnPrepend, columnIndex);
            return;
        }

        store.columns[columnIndex].Add(card);
        if (faceUp) EnqueueFlipSkill(card);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnColumnAppend, columnIndex);
    }

    /// <summary>翻牌：置为正面并派发单张刷新（供表现层播放翻牌动画）</summary>
    private void FlipCardUp(CardData card)
    {
        if (card == null || card.isFaceUp) return;
        card.isFaceUp = true;
        EnqueueFlipSkill(card);
        EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardChanged, card);
    }

    private void EnqueueFlipSkill(CardData card)
    {
        var skill = card.flipSkill;
        if (IsSettling || skill == E_FlipSkillEnum.None) return;
        if (skill == E_FlipSkillEnum.ForceDeal && CardsStore.Instance.drawPile.Count == 0) return;

        _flipSkills.Enqueue(skill);
        if (_resolvingFlipSkills) return;

        _resolvingFlipSkills = true;
        MonoManager.Instance.StartCoroutine(ResolveFlipSkillsRoutine());
    }

    private IEnumerator ResolveFlipSkillsRoutine()
    {
        while (!IsSettling && _flipSkills.Count > 0)
        {
            // 发牌和回收都可能翻出更多技能。等当前流程收尾后再处理队首。
            while (!IsSettling && (_dealing || _activeRecycles > 0 || _shufflingBack)) yield return null;
            if (IsSettling) break;

            yield return new WaitForSeconds(2 * CardAnimationHelper.FlipDuration);
            if (IsSettling) break;

            switch (_flipSkills.Dequeue())
            {
                case E_FlipSkillEnum.ForceDeal when CardsStore.Instance.drawPile.Count > 0:
                    // 技能发牌直接走发牌流程，允许给空列发牌。
                    yield return DealFromDrawPileRoutine();
                    break;
                case E_FlipSkillEnum.AddDrag:
                    BuffManager.Instance.Add(E_BuffEnum.FreeDrag);
                    break;
            }
        }

        _resolvingFlipSkills = false;
        TryShuffleBack();
    }

    /// <summary>
    /// 把一张牌收进弃牌堆（全项目唯一入口）：按概率打沉底标记 → 移入弃牌堆 → 派发表现事件。
    /// 顺子回收 / 回收技能 / 以后任何「牌进弃牌堆」都走这里，保证沉底规则只有一处
    /// </summary>
    private void TakeToDiscard(CardData card)
    {
        var store = CardsStore.Instance;

        // 沉底属性：进弃牌堆就按概率 roll（每次入堆重新 roll，发出时生效、发完即清）
        card.SetAttribute(E_CardAttributeEnum.Sunk, _random.NextDouble() < SunkProbability);

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
    private void StartRecycleFromColumn(int columnIndex, int count, bool countAsStraight)
    {
        _activeRecycles++;
        MonoManager.Instance.StartCoroutine(ResolveRecycleRoutine(columnIndex, count, countAsStraight));
    }

    private IEnumerator ResolveRecycleRoutine(int columnIndex, int count, bool countAsStraight)
    {
        yield return RecycleFromColumnRoutine(columnIndex, count, countAsStraight);
        _activeRecycles--;
        TryShuffleBack();
    }

    private IEnumerator RecycleFromColumnRoutine(int columnIndex, int count, bool countAsStraight)
    {
        var store = CardsStore.Instance;
        if (columnIndex < 0 || columnIndex >= store.columns.Count) yield break;

        var column = store.columns[columnIndex];

        // 逐张从堆顶往下收
        for (int i = 0; i < count; i++)
        {
            if (IsSettling) yield break;
            if (column.Count == 0) break;

            var card = column[column.Count - 1];
            column.RemoveAt(column.Count - 1);
            TakeToDiscard(card);

            yield return new WaitForSeconds(CardAnimationHelper.FlyInterval);
        }

        // 等最后一张飞到弃牌堆
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

        if (IsSettling) yield break;

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
    }

    /// <summary>
    /// 按落牌规则移动牌串；成功移动后结算实际使用的自由抓取次数。
    /// 移动后：原列若还有牌且新顶牌为反面，则翻开
    /// </summary>
    public bool MoveCards(List<CardData> draggedCards, int targetColumnIndex, bool usedFreeDrag)
    {
        var store = CardsStore.Instance;
        var rules = CardRuleManager.Instance;
        if (IsSettling || !rules.CanMove(draggedCards, targetColumnIndex)) return false;

        int fromColumnIndex = rules.FindColumnIndex(draggedCards[0]);
        if (fromColumnIndex < 0) return false;

        var fromColumn = store.columns[fromColumnIndex];
        var targetColumn = store.columns[targetColumnIndex];

        // 落牌时确认抓起的牌仍是原列末尾同一组正面牌。
        int start = fromColumn.Count - draggedCards.Count;
        if (start < 0) return false;
        for (int i = 0; i < draggedCards.Count; i++)
        {
            var card = fromColumn[start + i];
            if (card != draggedCards[i] || !card.isFaceUp) return false;
        }

        var buffs = BuffManager.Instance;
        if (usedFreeDrag && (!buffs.IsActivated(E_BuffEnum.FreeDrag) || buffs.GetCount(E_BuffEnum.FreeDrag) == 0))
            return false;

        fromColumn.RemoveRange(start, draggedCards.Count);
        targetColumn.AddRange(draggedCards);
        if (usedFreeDrag) buffs.TryConsume(E_BuffEnum.FreeDrag);

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

        return true;
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
                StartRecycleFromColumn(columnIndex, 2, false);
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

        // 单张换面：牌子本来就是正面，直接换成新牌面（不播翻牌动画）
        EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardFaceChanged, anchor);

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
        StartRecycleFromColumn(columnIndex, seq.Count, countAsStraight: true);
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
        LittleShopManager.Instance.Prepare(LevelStore.Instance.selectedLevelId);
        LevelManager.Instance.CompleteLevel(LevelStore.Instance.selectedLevelId);
        CardGameModule.Instance.SaveRun();
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
        _flipSkills.Clear();
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

        BuffManager.Instance.Clear();
        EventManager.Instance.Dispatch(E_EventEnum.OnCardsCollected);
    }

    /// <summary>发牌堆空了且弃牌堆有牌，播放洗回动画后把弃牌堆洗回发牌堆</summary>
    private void TryShuffleBack()
    {
        var store = CardsStore.Instance;
        if (IsSettling || IsResolvingBoard) return;
        if (store.drawPile.Count != 0 || store.discardPile.Count == 0) return;
        _shufflingBack = true;
        MonoManager.Instance.StartCoroutine(ShuffleBackRoutine());
    }

    private IEnumerator ShuffleBackRoutine()
    {
        var store = CardsStore.Instance;

        // 洗回动画：一张牌背从弃牌堆飞到发牌堆
        EventManager.Instance.Dispatch(E_EventEnum.OnShuffleBack);

        // 等动画播完再更新数据
        yield return new WaitForSeconds(CardAnimationHelper.FlyDuration);

        if (IsSettling)
        {
            _shufflingBack = false;
            yield break;
        }

        // 洗牌 + 全部压回发牌堆
        Shuffle(store.discardPile);
        foreach (var card in store.discardPile)
        {
            store.drawPile.Push(card);
        }
        store.discardPile.Clear();
        _shufflingBack = false;

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
    public void MoveFromPocket(int pocketIndex, int targetColumnIndex)    {
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

    /// <summary>
    /// 牌包之间移动 / 交换（全项目唯一入口）：
    ///   目标是空位 → 把牌移过去；目标有牌 → 两张互换位置。
    /// 牌面（主花色 / 主点数）保持原样，由 CardViewManager.RefreshPocket 接手
    /// </summary>
    public void SwapPockets(int fromIndex, int toIndex)
    {
        if (!CardRuleManager.Instance.CanMoveBetweenPockets(fromIndex, toIndex)) return;

        var pockets = CardsStore.Instance.pockets;

        // 空位就是一个 null，互换写法同时覆盖「移过去」和「交换」
        var moving = pockets[fromIndex];
        pockets[fromIndex] = pockets[toIndex];
        pockets[toIndex] = moving;

        // 两个位置都变了（交换时源/目标都有牌）→ 两边都刷
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnPocketChanged, fromIndex);
        EventManager.Instance.Dispatch<int>(E_EventEnum.OnPocketChanged, toIndex);
    }

    // ==================== 开局事件 ====================

    /// <summary>
    /// 开局事件流程：先跑事件自己的流程（洗入牌 / 改状态 / 播演出…，由
    /// <see cref="LevelEventRegistry"/> 里注册的 handler 决定），跑完统一发牌
    /// </summary>
    private IEnumerator StartWithLevelEventRoutine(LevelEventRegistry.LevelEventRoutine routine)
    {
        yield return routine();

        if (IsSettling)
        {
            _dealing = false;
            yield break;
        }

        Deal();
    }

    // ==================== 洗入发牌堆 ====================

    /// <summary>一条待洗入发牌堆的记录（牌定义；属性已经在定义里）</summary>
    private readonly struct WashInRequest
    {
        public readonly CardDef def;

        public WashInRequest(CardDef def)
        {
            this.def = def;
        }
    }

    /// <summary>待洗入发牌堆的牌队列（连续调用时排队，逐张错开飞入）</summary>
    private readonly Queue<WashInRequest> _washInQueue = new();

    /// <summary>洗入协程是否在跑（保证同一时刻只有一个队列在出队）</summary>
    private bool _washingIn;

    /// <summary>
    /// 往发牌堆洗入一张牌：随机插进发牌堆任意位置，
    /// 并派发「中央生成一张正面牌 → 飞入发牌堆」动画。连续调用会排队，逐张错开飞入
    /// </summary>
    /// <param name="def">牌定义（花色 / 点数 / 技能 / 自带属性）</param>
    /// <returns>定义合法返回 true</returns>
    public bool WashInCard(CardDef def)
    {
        if (!def.IsValid) return false;

        _washInQueue.Enqueue(new WashInRequest(def));
        if (!_washingIn) MonoManager.Instance.StartCoroutine(WashInRoutine());

        return true;
    }

    /// <summary>
    /// 洗入若干张牌并等待整段动画播完（关卡开局事件用）。
    /// 与控制台 add 共用同一个队列，所以两者不会在生成点重叠
    /// </summary>
    public IEnumerator WashInAndWait(IReadOnlyList<CardDef> defs)
    {
        if (defs == null || defs.Count == 0) yield break;

        foreach (var def in defs)
        {
            if (def.IsValid) _washInQueue.Enqueue(new WashInRequest(def));
        }

        if (!_washingIn) MonoManager.Instance.StartCoroutine(WashInRoutine());

        while (_washingIn) yield return null;   // 等队列排空
    }

    private IEnumerator WashInRoutine()
    {
        _washingIn = true;

        while (_washInQueue.Count > 0)
        {
            if (IsSettling) break;

            var request = _washInQueue.Dequeue();

            var card = CardFactory.Create(request.def);
            if (card == null) continue;   // 定义不合法：跳过

            InsertIntoDrawPileRandom(card);

            EventManager.Instance.Dispatch<int>(E_EventEnum.OnDrawPileChanged, CardsStore.Instance.drawPile.Count);
            EventManager.Instance.Dispatch<CardData>(E_EventEnum.OnCardWashIn, card);

            // 队列还有牌 → 到下一张该出场的时刻就走；队列空了 → 等到这张落地才收工
            // （等待期间若又有新牌入队，循环会继续，不会丢牌）
            yield return new WaitForSeconds(_washInQueue.Count > 0
                ? CardAnimationHelper.WashInInterval
                : CardAnimationHelper.WashInShowDuration + CardAnimationHelper.FlyDuration);
        }

        _washInQueue.Clear();
        _washingIn = false;
    }

    /// <summary>把一张牌随机插进发牌堆（栈只能后进先出，整体摊开 → 插入 → 反向压回）</summary>
    private void InsertIntoDrawPileRandom(CardData card)    {
        var pile = CardsStore.Instance.drawPile;

        // Stack 的枚举顺序是「栈顶 → 栈底」，插在 index 0 = 下次先发，插在最末 = 最后才发
        var list = new List<CardData>(pile);
        list.Insert(_random.Next(list.Count + 1), card);

        pile.Clear();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            pile.Push(list[i]);
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
            int byRank = MinRank(a).CompareTo(MinRank(b));
            return byRank != 0 ? byRank : MinSuit(a).CompareTo(MinSuit(b));
        });

        return list;
    }

    /// <summary>展示排序用的点数摘要：最小的普通点数（1-13）；没有普通点数的牌（黑色 / 万能）排最后</summary>
    private static int MinRank(CardData card)
    {
        int min = int.MaxValue;
        foreach (int r in card.ranks)
        {
            if (r >= 1 && r <= 13 && r < min) min = r;
        }
        return min;
    }

    /// <summary>展示排序用的花色摘要：最小花色；没有花色的牌排最后</summary>
    private static int MinSuit(CardData card)
    {
        int min = int.MaxValue;
        foreach (int s in card.suits)
        {
            if (s < min) min = s;
        }
        return min;
    }
}
