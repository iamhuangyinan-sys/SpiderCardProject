using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 一整局游戏（一大局）的资产逻辑 —— 金币 + 牌组
///
/// 作用域：从进入 card 场景（开始新的一大局）到通关结束，重新开始则清零。
/// 牌组里存的是「牌定义」CardDef（花色 / 点数 / 技能 / 自带属性），没有 id
/// </summary>
public class RunManager : ManagerBase<RunManager>
{
    /// <summary>本局金币</summary>
    public int Coin => RunStore.Instance.coin;

    /// <summary>本局牌组（牌定义列表，同一张出现多次即多份）</summary>
    public IReadOnlyList<CardDef> Deck => RunStore.Instance.deck;

    public bool HasStarted => RunStore.Instance.hasStarted;

    protected override void OnInit() { }

    protected override void OnDispose() { }

    // ==================== 一大局生命周期 ====================

    /// <summary>确认初始牌组后开始新的一大局；配方无效时保留原有状态。</summary>
    public bool StartNewRun(string beginBuildId, int? seed = null)
    {
        if (!BeginBuildHelper.TryBuild(beginBuildId, out var deck)) return false;
        if (!LevelManager.Instance.TryCreateRunMap(deck.Count, seed)) return false;
        RunStore.Instance.Reset();
        foreach (var def in deck) RunStore.Instance.deck.Add(def.Clone());
        RunStore.Instance.hasStarted = true;
        NotifyCoinChanged();
        return true;
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
    public void AddCardToDeck(CardDef def)
    {
        if (!def.IsValid) return;

        RunStore.Instance.deck.Add(def.Clone());
    }

    /// <summary>
    /// 从本局牌组里移除一张牌（商店删牌等）：按内容匹配
    /// 同一张牌有多份时只移除第一份；牌不在牌组里返回 false
    /// </summary>
    public bool RemoveCardFromDeck(CardDef def)
    {
        if (!def.IsValid) return false;

        var deck = RunStore.Instance.deck;
        int index = deck.IndexOf(def);
        if (index < 0) return false;

        deck.RemoveAt(index);
        RunStore.Instance.Refresh();
        return true;
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
        store.hasStarted = true;

        NotifyCoinChanged();
    }
}
