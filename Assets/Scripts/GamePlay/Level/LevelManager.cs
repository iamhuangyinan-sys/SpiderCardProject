using System.Collections.Generic;
using Framework;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 关卡逻辑层 —— 加载关卡图、选关、通关解锁
/// </summary>
public class LevelManager : ManagerBase<LevelManager>
{
    protected override void OnInit()
    {
        LoadLevels();
    }

    protected override void OnDispose() { }

    /// <summary>读配表 → 按层分组 → 初始解锁第一层</summary>
    private void LoadLevels()
    {
        var store = LevelStore.Instance;

        foreach (var cfg in ConfigHelper.GetAll<LevelConfig>())
        {
            store.allLevels[cfg.Id] = cfg;

            int layer = GetLayer(cfg.Id);
            if (!store.levelsByLayer.TryGetValue(layer, out var list))
            {
                list = new List<LevelConfig>();
                store.levelsByLayer[layer] = list;
            }
            list.Add(cfg);
        }

        // 每层按关号排序
        foreach (var list in store.levelsByLayer.Values)
        {
            list.Sort((a, b) => GetLayerIndex(a.Id).CompareTo(GetLayerIndex(b.Id)));
        }

        // 初始解锁第一层全部，当前层 = 最小层
        int minLayer = GetMinLayer();
        store.currentLayer = minLayer;
        if (store.levelsByLayer.TryGetValue(minLayer, out var firstList))
        {
            foreach (var cfg in firstList)
            {
                store.unlockedIds.Add(cfg.Id);
            }
        }
    }

    /// <summary>取层号（id 第 3-4 位）</summary>
    public static int GetLayer(string levelId) => int.Parse(levelId.Substring(2, 2));

    /// <summary>取层内关号（id 第 5-6 位）</summary>
    public static int GetLayerIndex(string levelId) => int.Parse(levelId.Substring(4, 2));

    /// <summary>最小层号</summary>
    public int GetMinLayer()
    {
        int min = int.MaxValue;
        foreach (var layer in LevelStore.Instance.levelsByLayer.Keys)
        {
            if (layer < min) min = layer;
        }
        return min == int.MaxValue ? 0 : min;
    }

    /// <summary>最大层号</summary>
    public int GetMaxLayer()
    {
        int max = int.MinValue;
        foreach (var layer in LevelStore.Instance.levelsByLayer.Keys)
        {
            if (layer > max) max = layer;
        }
        return max == int.MinValue ? 0 : max;
    }

    /// <summary>单层最多关卡数</summary>
    public int GetMaxLayerCount()
    {
        int max = 0;
        foreach (var list in LevelStore.Instance.levelsByLayer.Values)
        {
            if (list.Count > max) max = list.Count;
        }
        return max;
    }

    /// <summary>
    /// 结算完成：发放挂起的通关奖励（加金币）
    /// 由收牌结算结束（打牌关）或商店结束（商店关）时调用，保证金币变化与层号刷新同时机
    /// </summary>
    public void SettleLevelReward()
    {
        var store = LevelStore.Instance;
        if (store.pendingReward <= 0) return;

        int reward = store.pendingReward;
        store.pendingReward = 0;

        RunManager.Instance.AddCoin(reward);
    }

    /// <summary>解析 NextLevelId（; 分割）</summary>
    public List<string> GetNextIds(string levelId)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(levelId)) return result;
        if (!LevelStore.Instance.allLevels.TryGetValue(levelId, out var cfg)) return result;
        if (string.IsNullOrEmpty(cfg.NextLevelId)) return result;

        foreach (var id in cfg.NextLevelId.Split(';'))
        {
            if (!string.IsNullOrEmpty(id)) result.Add(id);
        }
        return result;
    }

    /// <summary>选中关卡所需接龙次数（未选关或无此关卡返回 0）</summary>
    public int GetSelectedNeedStraightNum()
    {
        var store = LevelStore.Instance;
        if (string.IsNullOrEmpty(store.selectedLevelId)) return 0;
        if (!store.allLevels.TryGetValue(store.selectedLevelId, out var cfg)) return 0;
        return cfg.NeedStraightNum;
    }

    /// <summary>选择关卡：商店关开商店，普通 / BOSS 关开始一局新游戏</summary>
    public void SelectLevel(string levelId)
    {
        var store = LevelStore.Instance;
        if (!store.IsUnlocked(levelId)) return;
        if (!store.allLevels.TryGetValue(levelId, out var cfg)) return;

        // 记为进行中的关卡，并立即落盘（退出重进时直接重开这一关）
        store.selectedLevelId = levelId;
        store.Refresh();

        // 商店关：备好商品后弹商店面板（商品只在首次进入时随机）
        if (cfg.LevelType == (int)E_LevelTypeEnum.Shop)
        {
            ShopManager.Instance.EnterShop(levelId);
            CardGameModule.Instance.SaveRun();

            EventManager.Instance.Dispatch(E_EventEnum.OnShopOpen);
            return;
        }

        if (cfg.ColumnNum <= 0) return;   // 其他非打牌关暂不进入

        CardGameModule.Instance.SaveRun();

        // 开始一局新游戏
        CardGameModule.Instance.StartNewGame(cfg.ColumnNum, cfg.PocketNum);

        // 通知：一局游戏开始
        EventManager.Instance.Dispatch(E_EventEnum.OnLevelStart);
    }

    /// <summary>
    /// 通关：记录完成关卡 + 挂起奖励 + 解锁下一批关卡
    /// 进入更高层时切层（清掉旧层解锁状态，不走回头路）；已完成的关卡不再可选
    /// 奖励在结算动画结束后由 SettleLevelReward() 发放
    /// </summary>
    public void CompleteLevel(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return;

        var store = LevelStore.Instance;
        store.lastLevelId = levelId;      // 记录最后完成的关卡
        store.selectedLevelId = null;     // 开完了，回到选关状态

        // 挂起通关奖励：结算动画结束后由 SettleLevelReward() 发放
        if (store.allLevels.TryGetValue(levelId, out var cfg))
        {
            store.pendingReward = cfg.FinishReward;
        }

        UnlockNext(levelId);
        store.Refresh();

        // 通知：通关
        EventManager.Instance.Dispatch(E_EventEnum.OnLevelComplete);
    }

    /// <summary>
    /// 解锁某关的下一批关卡（通关与读档共用）
    /// 进入更高层时切层（清掉旧层解锁状态，不走回头路）；已完成的关卡不再可选
    /// </summary>
    private void UnlockNext(string levelId)
    {
        var store = LevelStore.Instance;

        // 已完成关卡不再可选（不能重复打）
        store.unlockedIds.Remove(levelId);

        var nextIds = GetNextIds(levelId);

        // 下一批所在的高层号
        int nextLayer = 0;
        foreach (var nextId in nextIds)
        {
            int layer = GetLayer(nextId);
            if (layer > nextLayer) nextLayer = layer;
        }

        // 进入新层：旧层的解锁状态全部作废
        if (nextLayer > store.currentLayer)
        {
            store.currentLayer = nextLayer;
            store.unlockedIds.Clear();
        }

        foreach (var nextId in nextIds)
        {
            store.unlockedIds.Add(nextId);
        }
    }

    // ==================== 存档 ====================

    /// <summary>导出关卡进度（存档用）</summary>
    public void ExportTo(RunSaveData data)
    {
        data.lastLevelId = LevelStore.Instance.lastLevelId;
        data.selectedLevelId = LevelStore.Instance.selectedLevelId;
    }

    /// <summary>从存档恢复关卡进度：用「最后完成的关卡」重建解锁状态与当前层</summary>
    public void ImportFrom(RunSaveData data)
    {
        var store = LevelStore.Instance;

        store.lastLevelId = data.lastLevelId;
        store.selectedLevelId = data.selectedLevelId;

        // 未通关过任何关卡 → 保持初始第一层解锁状态
        if (!string.IsNullOrEmpty(store.lastLevelId))
        {
            UnlockNext(store.lastLevelId);
        }

        store.Refresh();
    }
}
