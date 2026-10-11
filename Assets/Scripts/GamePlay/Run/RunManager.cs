using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 一整局游戏（一大局）的资产逻辑 —— 金币 + 牌组
///
/// 作用域：从进入 card 场景（开始新的一大局）到通关结束，重新开始则清零。
/// TODO: 后续接入存档，支持中途退出后继续这一局
/// </summary>
public class RunManager : ManagerBase<RunManager>
{
    /// <summary>本局金币</summary>
    public int Coin => RunStore.Instance.coin;

    /// <summary>本局牌组（牌 id 列表，同一 id 出现多次即多份）</summary>
    public IReadOnlyList<string> Deck => RunStore.Instance.deck;

    /// <summary>初始牌组每组点数份数</summary>
    private const int DefaultDeckCopies = 2;

#if UNITY_EDITOR
    /// <summary>【编辑器测试】额外塞进初始牌组的牌（编码 id）</summary>
    private static readonly string[] TestExtraCardIds =
    {
        // "N0_s", "N2_s",              // 蜘蛛：红心 / 黑桃
        // "N01_0jk",                   // 多重花色 + 多重点数
        // "S__0300001",                // 黑色牌
        // "S_m_0200001", "S_m_0200001",  // 复制 ×2
        // "S_m_0200002", "S_m_0200002",  // 回收 ×2
    };
#endif

    protected override void OnInit()
    {
        // 保证任何时候读牌组都是有效的（进 card 场景时 CardGameEntry 会再调一次）
        StartNewRun();
    }

    protected override void OnDispose() { }

    // ==================== 一大局生命周期 ====================

    /// <summary>开始新的一大局：本局资产清零 + 重建初始牌组</summary>
    public void StartNewRun()
    {
        RunStore.Instance.Reset();
        BuildDefaultDeck();
        NotifyCoinChanged();
    }

    // ==================== 金币 ====================

    /// <summary>金币是否足够</summary>
    public bool IsEnough(int amount) => RunStore.Instance.coin >= amount;

    /// <summary>增加金币（通关奖励等）</summary>
    public void AddCoin(int amount)
    {
        if (amount <= 0) return;

        RunStore.Instance.coin += amount;
        NotifyCoinChanged();
    }

    /// <summary>尝试扣除金币（不足则返回 false，不扣）</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (!IsEnough(amount)) return false;

        RunStore.Instance.coin -= amount;
        NotifyCoinChanged();
        return true;
    }

    /// <summary>通知金币变化（数据层 + 事件）</summary>
    private void NotifyCoinChanged()
    {
        RunStore.Instance.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnCoinChanged);
    }

    // ==================== 牌组 ====================

    /// <summary>把一张牌加入本局牌组（商店购买等）</summary>
    public void AddCardToDeck(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        RunStore.Instance.deck.Add(cardId);
    }

    /// <summary>
    /// 从本局牌组里移除一张牌（商店删牌等）
    /// 同一张牌有多份时只移除第一份；牌不在牌组里返回 false
    /// </summary>
    public bool RemoveCardFromDeck(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        var deck = RunStore.Instance.deck;
        int index = deck.IndexOf(cardId);
        if (index < 0) return false;

        deck.RemoveAt(index);
        RunStore.Instance.Refresh();
        return true;
    }

    /// <summary>构建初始牌组：红桃 A-K + 黑桃 A-K，各两份（普通牌按花色点数生成编码 id，不查配表）</summary>
    private void BuildDefaultDeck()
    {
        var deck = RunStore.Instance.deck;
        deck.Clear();

        AddDefaultDeckCards(deck, E_CardSuitEnum.Hearts);
        AddDefaultDeckCards(deck, E_CardSuitEnum.Spades);

        // 【编辑器测试】额外塞几张（编码 id），出包不生效
#if UNITY_EDITOR
        foreach (var id in TestExtraCardIds)
        {
            deck.Add(id);
        }
#endif
    }

    /// <summary>把一个花色的 A-K 各塞 DefaultDeckCopies 份</summary>
    private static void AddDefaultDeckCards(List<string> deck, E_CardSuitEnum suit)
    {
        for (int rank = 1; rank <= 13; rank++)
        {
            string id = CardIdCodec.Encode((int)suit, rank);

            for (int i = 0; i < DefaultDeckCopies; i++)
            {
                deck.Add(id);
            }
        }
    }

    // ==================== 存档 ====================

    /// <summary>导出本局资产（存档用）</summary>
    public void ExportTo(RunSaveData data)
    {
        var store = RunStore.Instance;

        data.coin = store.coin;
        data.deck.Clear();
        data.deck.AddRange(store.deck);
    }

    /// <summary>从存档恢复本局资产（金币 + 牌组）</summary>
    public void ImportFrom(RunSaveData data)
    {
        var store = RunStore.Instance;
        store.Reset();

        store.coin = data.coin;
        if (data.deck != null) store.deck.AddRange(data.deck);

        NotifyCoinChanged();
    }
}
