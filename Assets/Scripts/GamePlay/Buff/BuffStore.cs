using System.Collections.Generic;
using Framework.Store;

public class BuffStore : StoreBase<BuffStore>
{
    internal readonly Dictionary<E_BuffEnum, BuffState> buffs = new();

    protected override void OnDispose()
    {
        buffs.Clear();
    }
}

public readonly struct BuffState
{
    public int Count { get; }
    public bool IsActivated { get; }

    public BuffState(int count, bool isActivated)
    {
        Count = count;
        IsActivated = isActivated;
    }
}
