using System.Collections.Generic;
using Framework.Store;

/// <summary>
/// 关卡数据层 —— 管理关卡图结构、解锁进度
/// </summary>
public class LevelStore : StoreBase<LevelStore>
{
    /// <summary>最后完成的关卡 id（空 = 还没通关过任何关卡）—— 用于重建解锁状态与当前层</summary>
    public string lastLevelId;

    /// <summary>当前层数（只解锁当前层的关卡，不走回头路）</summary>
    public int currentLayer;

    /// <summary>待发放的通关奖励（接龙达标时挂起，结算动画结束后发放）</summary>
    public int pendingReward;

    /// <summary>进行中的关卡 id（空 = 停在选关界面）</summary>
    public string selectedLevelId;

    /// <summary>已解锁的关卡 id 集合</summary>
    public readonly HashSet<string> unlockedIds = new();

    /// <summary>全部关卡（id → 配置）</summary>
    public readonly Dictionary<string, LevelConfig> allLevels = new();

    /// <summary>层号 → 该层关卡配置列表（按关号排序）</summary>
    public readonly Dictionary<int, List<LevelConfig>> levelsByLayer = new();

    protected override void OnInit()
    {
        Clear();
    }

    protected override void OnDispose()
    {
        Clear();
    }

    private void Clear()
    {
        lastLevelId = null;
        selectedLevelId = null;
        currentLayer = 0;
        pendingReward = 0;
        unlockedIds.Clear();
        allLevels.Clear();
        levelsByLayer.Clear();
    }

    /// <summary>某关卡是否已解锁</summary>
    public bool IsUnlocked(string levelId) => unlockedIds.Contains(levelId);

    /// <summary>通知数据变更（UI 刷新）</summary>
    public void Refresh() => NotifyDataChanged();
}
