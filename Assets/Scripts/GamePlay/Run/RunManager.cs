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
    /// <summary>【编辑器测试】额外塞进初始牌组的牌</summary>
    private static readonly string[] TestExtraCardIds =
    {
        // "0200001", "0200003", "0200005",   // 万能牌：♥蜘蛛 / ♠蜘蛛 / 全万能
        // "0300001",                         // 黑色牌
        // "0200006", "0200006",              // 复制 ×2
        // "0200007", "0200007",              // 回收 ×2
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

    /// <summary>构建初始牌组：红桃 A-K + 黑桃 A-K，各两份</summary>
    private void BuildDefaultDeck()
    {
        var deck = RunStore.Instance.deck;
        deck.Clear();

        foreach (var cfg in ConfigHelper.GetAll<CardConfig>())
        {
            if (!IsDefaultDeckCard(cfg)) continue;

            for (int i = 0; i < DefaultDeckCopies; i++)
            {
                deck.Add(cfg.Id);
            }
        }

        // 【编辑器测试】额外塞几张特殊牌（万能牌），出包不生效
#if UNITY_EDITOR
        foreach (var id in TestExtraCardIds)
        {
            deck.Add(id);
        }
#endif
    }

    /// <summary>是否属于初始牌组：红桃 / 黑桃的 A-K</summary>
    private static bool IsDefaultDeckCard(CardConfig cfg)
    {
        if (cfg.Suit != (int)E_CardSuitEnum.Hearts && cfg.Suit != (int)E_CardSuitEnum.Spades) return false;
        return cfg.Rank >= 1 && cfg.Rank <= 13;
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
