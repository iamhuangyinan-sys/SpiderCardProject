using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 一整局游戏（一大局）的资产逻辑 —— 目前只有金币
///
/// 作用域：从进入 card 场景（开始新的一大局）到通关结束，重新开始则清零。
/// TODO: 后续接入存档，支持中途退出后继续这一局
/// </summary>
public class RunManager : ManagerBase<RunManager>
{
    /// <summary>本局金币</summary>
    public int Coin => RunStore.Instance.coin;

    protected override void OnInit() { }

    protected override void OnDispose() { }

    // ==================== 一大局生命周期 ====================

    /// <summary>开始新的一大局：本局资产清零</summary>
    public void StartNewRun()
    {
        RunStore.Instance.Reset();
        NotifyChanged();
    }

    // ==================== 金币 ====================

    /// <summary>金币是否足够</summary>
    public bool IsEnough(int amount) => RunStore.Instance.coin >= amount;

    /// <summary>增加金币（通关奖励等）</summary>
    public void AddCoin(int amount)
    {
        if (amount <= 0) return;

        RunStore.Instance.coin += amount;
        NotifyChanged();
    }

    /// <summary>尝试扣除金币（不足则返回 false，不扣）</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (!IsEnough(amount)) return false;

        RunStore.Instance.coin -= amount;
        NotifyChanged();
        return true;
    }

    /// <summary>通知金币变化（数据层 + 事件）</summary>
    private void NotifyChanged()
    {
        RunStore.Instance.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnCoinChanged);
    }
}
