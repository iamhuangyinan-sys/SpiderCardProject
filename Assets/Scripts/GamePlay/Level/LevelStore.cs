using System.Collections.Generic;
using Framework.Store;

/// <summary>
/// 关卡数据层 —— 管理关卡图结构、解锁进度
/// </summary>
public class LevelStore : StoreBase<LevelStore>
{
    /// <summary>
    /// 玩家选择过的关卡 id（按选择顺序追加，含正在打的那一关）
    ///   相邻两项 = 玩家实际走过的路线（选关界面 / 地图据此把连线置白）
    /// </summary>
    public readonly List<string> chosenLevelIds = new();

    /// <summary>
    /// 最后通关的关卡 id（不单独记录，纯推导；没通关过任何关卡时为 null）：
    ///   正在打（selectedLevelId 非空）→ 倒数第二个选择
    ///   停在选关界面（selectedLevelId 为空）→ 最后一个选择
    /// </summary>
    public string LastClearedLevelId
    {
        get
        {
            int count = chosenLevelIds.Count;
            if (count == 0) return null;

            if (string.IsNullOrEmpty(selectedLevelId)) return chosenLevelIds[count - 1];

            // 正在打：第一关还在打时说明还没通关过任何关卡
            return count >= 2 ? chosenLevelIds[count - 2] : null;
        }
    }

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
        chosenLevelIds.Clear();
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
