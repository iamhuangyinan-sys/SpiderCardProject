using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

public class BuffManager : ManagerBase<BuffManager>
{
    public bool Add(E_BuffEnum id, int amount = 1)
    {
        if (amount <= 0) return false;
        var config = BuffHelper.GetConfig(id);
        if (config == null) return false;

        var buffs = BuffStore.Instance.buffs;
        if (config.IsCount)
        {
            buffs.TryGetValue(id, out var current);
            if (amount > int.MaxValue - current.Count) return false;
            buffs[id] = new BuffState(current.Count + amount, current.IsActivated);
        }
        else if (!buffs.TryAdd(id, default))
        {
            return false;
        }

        NotifyChanged();
        return true;
    }

    public bool Has(E_BuffEnum id) => BuffStore.Instance.buffs.ContainsKey(id);

    public int GetCount(E_BuffEnum id)
    {
        return BuffStore.Instance.buffs.TryGetValue(id, out var state) ? state.Count : 0;
    }

    public bool IsActivated(E_BuffEnum id)
    {
        return BuffStore.Instance.buffs.TryGetValue(id, out var state) && state.IsActivated;
    }

    public bool ToggleActivation(E_BuffEnum id)
    {
        var buffs = BuffStore.Instance.buffs;
        if (!buffs.TryGetValue(id, out var state) || BuffHelper.GetConfig(id)?.CanActivate != true) return false;

        buffs[id] = new BuffState(state.Count, !state.IsActivated);
        NotifyChanged();
        return true;
    }

    public bool TryConsume(E_BuffEnum id, int amount = 1)
    {
        if (amount <= 0 || BuffHelper.GetConfig(id)?.IsCount != true) return false;

        var buffs = BuffStore.Instance.buffs;
        if (!buffs.TryGetValue(id, out var state) || state.Count < amount) return false;
        if (state.Count == amount) return Remove(id);

        buffs[id] = new BuffState(state.Count - amount, state.IsActivated);
        NotifyChanged();
        return true;
    }

    public bool Remove(E_BuffEnum id)
    {
        if (!BuffStore.Instance.buffs.Remove(id)) return false;
        NotifyChanged();
        return true;
    }

    public void Clear()
    {
        var buffs = BuffStore.Instance.buffs;
        if (buffs.Count == 0) return;

        buffs.Clear();
        NotifyChanged();
    }

    public IReadOnlyList<KeyValuePair<E_BuffEnum, BuffState>> GetActiveBuffs()
    {
        var snapshot = new List<KeyValuePair<E_BuffEnum, BuffState>>(BuffStore.Instance.buffs);
        snapshot.Sort((a, b) => a.Key.CompareTo(b.Key));
        return snapshot;
    }

    private static void NotifyChanged()
    {
        EventManager.Instance.Dispatch(E_EventEnum.OnBuffChanged);
    }
}
