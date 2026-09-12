using System.Collections.Generic;
using Framework.Store;

/// <summary>
/// 一整局游戏（一大局：从第一层开始到通关）的玩家资产数据
///
/// 注意：作用域是「一大局」，不是跨局长期资产；开始新的一大局时清零。
/// TODO: 后续接入存档，支持中途退出后继续这一局
/// </summary>
public class RunStore : StoreBase<RunStore>
{
    /// <summary>本局金币</summary>
    public int coin;

    /// <summary>本局牌组（牌 id 列表，同一 id 出现多次即多份）</summary>
    public readonly List<string> deck = new();

    protected override void OnInit()
    {
        Reset();
    }

    protected override void OnDispose()
    {
        Reset();
    }

    /// <summary>重置本局资产（开始新的一大局时调用）</summary>
    public void Reset()
    {
        coin = 0;
        deck.Clear();
        // 以后：临时 buff 等
    }

    /// <summary>通知数据变更（UI 刷新）</summary>
    public void Refresh() => NotifyDataChanged();
}
