using System.Collections.Generic;
using Framework;
using Framework.Event;
using Framework.Mgr;
#if UNITY_EDITOR
using System.Linq;
#endif

/// <summary>
/// 关卡逻辑层 —— 加载关卡图、选关、通关解锁
/// </summary>
public class LevelManager : ManagerBase<LevelManager>
{
    // 地图只在新局生成或读档恢复时装载，初始化不再加载固定地图。
    protected override void OnInit() { }

    protected override void OnDispose() { }

    private void ApplyMap(RunMapData map, int chapter)
    {
        var store = LevelStore.Instance;
        store.map = map;
        store.chosenLevelIds.Clear();
        store.selectedLevelId = null;
        store.pendingReward = 0;
        ApplyChapter(chapter);
    }

    private void ApplyChapter(int chapter)
    {
        var store = LevelStore.Instance;
        store.currentChapter = chapter;
        store.currentLayer = 1;
        store.allLevels.Clear();
        store.levelsByLayer.Clear();
        store.unlockedIds.Clear();

        foreach (var node in store.CurrentChapterMap.levels)
        {
            node.initialCards?.Sort();
            node.actualInitialCards?.Sort();
            store.allLevels.Add(node.Id, node);

            int layer = RunLevelData.GetLayer(node.Id);
            if (!store.levelsByLayer.TryGetValue(layer, out var list))
            {
                list = new List<RunLevelData>();
                store.levelsByLayer[layer] = list;
            }
            list.Add(node);
        }

        // 每层按关号排序
        foreach (var list in store.levelsByLayer.Values)
        {
            list.Sort((a, b) => RunLevelData.GetLayerIndex(a.Id).CompareTo(RunLevelData.GetLayerIndex(b.Id)));
        }

        store.unlockedIds.Add(store.levelsByLayer[1][0].Id);
    }

    /// <summary>先生成成功再替换当前地图，避免错误参数破坏正在玩的局。</summary>
    public bool TryCreateRunMap(int deckCount, int difficulty, int? requestedSeed = null)
    {
        try
        {
            int seed = requestedSeed ?? System.Guid.NewGuid().GetHashCode();
            var map = LevelGenerationHelper.Generate(seed, deckCount, difficulty);
            ApplyMap(map, 1);
            ShopManager.Instance.ResetForNewRun();
            LittleShopManager.Instance.ResetForNewRun();
            UnityEngine.Debug.Log($"[Level] 新地图 seed={seed}, version={map.generatorVersion}");
            return true;
        }
        catch (System.Exception error)
        {
            UnityEngine.Debug.LogException(error);
            TipHelper.Show(LanguageHelper.Get("tip_level_generate_failed"));
            return false;
        }
    }

    /// <summary>获取当前局地图种子；没有地图时返回空。</summary>
    public int? GetMapSeed() => LevelStore.Instance.map?.seed;

    /// <summary>开局事件完成后确定实际配额，删牌导致不足时削减深列，并保存避免读档变化。</summary>
    public List<int> GetInitialDeal(int columns, int available)
    {
        var store = LevelStore.Instance;
        if (!string.IsNullOrEmpty(store.selectedLevelId)
            && store.allLevels.TryGetValue(store.selectedLevelId, out var cfg) && cfg is RunLevelData node)
        {
            bool resolved = node.actualInitialCards != null && node.actualInitialCards.Count == columns;
            var result = LevelGenerationHelper.FitDeal(resolved ? node.actualInitialCards : node.initialCards, columns, available);
            node.actualInitialCards = result;
            CardGameModule.Instance.SaveRun();
            return result;
        }
        throw new System.InvalidOperationException("当前关卡缺少本局地图数据，无法发牌");
    }

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
    /// 发放挂起的通关金币。
    /// 收牌结束、商店结束或恢复存档时调用，返回是否需要保存结算结果。
    /// </summary>
    public bool SettleCompletedLevel()
    {
        var store = LevelStore.Instance;
        int reward = store.pendingReward;
        store.pendingReward = 0;
        RunManager.Instance.AddCoin(reward);
        return reward > 0;
    }

    public bool IsWaitingForMainSuit
    {
        get
        {
            var store = LevelStore.Instance;
            if (store.map == null || store.currentChapter >= store.map.chapters.Count
                || !string.IsNullOrEmpty(store.selectedLevelId)) return false;
            string lastLevelId = store.LastClearedLevelId;
            return !string.IsNullOrEmpty(lastLevelId)
                && store.allLevels.TryGetValue(lastLevelId, out var node)
                && node.LevelType == (int)E_LevelTypeEnum.Boss;
        }
    }

    public bool TryAdvanceChapterWithSuit(E_CardSuitEnum suit)
    {
        if (!IsWaitingForMainSuit || !RunManager.Instance.TryAddMainSuit(suit)) return false;
        var store = LevelStore.Instance;
        ApplyChapter(store.currentChapter + 1);
        store.Refresh();
        return true;
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

    /// <summary>选中关卡的开局事件 Id（配表 LevelEventConfig 主键；0 = 没有开局事件）</summary>
    public int GetSelectedLevelEvent()
    {
        var store = LevelStore.Instance;
        if (string.IsNullOrEmpty(store.selectedLevelId)) return 0;
        if (!store.allLevels.TryGetValue(store.selectedLevelId, out var cfg)) return 0;
        return cfg.LevelEvent;
    }

    /// <summary>选择关卡：商店关开商店，普通 / BOSS 关开始一局新游戏</summary>
    public void SelectLevel(string levelId)
    {
        var store = LevelStore.Instance;
        if (!store.IsUnlocked(levelId)) return;
        if (!store.allLevels.TryGetValue(levelId, out var cfg)) return;

        // 记为进行中的关卡，并立即落盘（退出重进时直接重开这一关）
        store.selectedLevelId = levelId;

        // 记录选择顺序：路线 = 相邻两次选择之间的连线（选关当下就记，不等通关）
        // 同一关连续重复不记 —— 读档续玩会再调一次 SelectLevel 重开同一关，靠这个去重
        if (store.chosenLevelIds.Count == 0 || store.chosenLevelIds[^1] != levelId)
        {
            store.chosenLevelIds.Add(levelId);
        }

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
    /// 走完当前层后仅保留后继节点的解锁状态。
    /// 收牌结束后发放金币，前两个 Boss 通关后等待增加花色。
    /// </summary>
    public void CompleteLevel(string levelId)
    {
        var store = LevelStore.Instance;
        if (string.IsNullOrEmpty(levelId) || store.selectedLevelId != levelId
            || !store.allLevels.TryGetValue(levelId, out var node)) return;

        // 开完了，回到选关状态。
        // 路线不在这里记：关卡 id 是「选择时」就写进 chosenLevelIds 的，
        // 清掉 selectedLevelId 后 LastClearedLevelId 自然指向这一关
        store.selectedLevelId = null;

        // 挂起通关奖励，等结算动画结束后发放。
        store.pendingReward = node.FinishReward;

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

        var nextIds = GetNextIds(levelId);
        store.currentLayer = RunLevelData.GetLayer(nextIds.Count > 0 ? nextIds[0] : levelId);
        store.unlockedIds.Clear();

        foreach (var nextId in nextIds)
        {
            store.unlockedIds.Add(nextId);
        }
    }

    // ==================== 路线（选关界面的连线） ====================

    /// <summary>从 fromId 出发到 toId 是否是配表里的一条连线（用于判断玩家是否正沿路线前进）</summary>
    public bool IsNextLevel(string fromId, string toId)
    {
        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return false;
        return GetNextIds(fromId).Contains(toId);
    }

    /// <summary>
    /// a、b 两关之间的连线是否走过（true → 画白色，false → 画灰色）
    /// 连线是无向的（同一对关卡只画一条），所以两个方向都算
    /// </summary>
    public bool HasTraversedEdge(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;

        var path = LevelStore.Instance.chosenLevelIds;
        for (int i = 0; i < path.Count - 1; i++)
        {
            string cur = path[i];
            string next = path[i + 1];

            if ((cur == a && next == b) || (cur == b && next == a)) return true;
        }
        return false;
    }

    /// <summary>
    /// 玩家当前所在关卡 id（选关界面标「你在这里」用）：
    /// 关卡中标记进行中的关卡，选关时只标记当前大关最后通关的关卡。
    /// </summary>
    public string GetPlayerLevelId()
    {
        var store = LevelStore.Instance;
        if (!string.IsNullOrEmpty(store.selectedLevelId)) return store.selectedLevelId;
        string lastLevelId = store.LastClearedLevelId;
        return !string.IsNullOrEmpty(lastLevelId) && store.allLevels.ContainsKey(lastLevelId)
            ? lastLevelId
            : null;
    }

    // ==================== 存档 ====================

#if UNITY_EDITOR
    /// <summary>按每层最上方的可选节点重建路线，停在目标层选关之前。</summary>
    public bool TryCreateDebugLevelSelection(int chapter, int layer, out RunSaveData data)
    {
        data = null;
        var map = LevelStore.Instance.map;
        if (map == null || chapter < 1 || chapter > map.chapters.Count
            || layer < 1 || layer > LevelGenerationSettings.LayerCount) return false;

        var route = new List<string>();
        for (int chapterNumber = 1; chapterNumber <= chapter; chapterNumber++)
        {
            var levels = map.chapters[chapterNumber - 1].levels;
            int endLayer = chapterNumber == chapter ? layer : LevelGenerationSettings.LayerCount;
            string[] reachableIds = null;

            for (int currentLayer = 1; currentLayer <= endLayer; currentLayer++)
            {
                var node = levels.Where(n => RunLevelData.GetLayer(n.Id) == currentLayer
                        && (reachableIds == null || reachableIds.Contains(n.Id)))
                    .OrderBy(n => RunLevelData.GetLayerIndex(n.Id))
                    .FirstOrDefault();
                if (node == null) return false;

                // 确认目标可达即可，留给玩家从这一层的所有解锁节点中选择。
                if (chapterNumber == chapter && currentLayer == layer) break;

                route.Add(node.Id);
                reachableIds = node.NextLevelId?.Split(';') ?? System.Array.Empty<string>();
            }
        }

        data = new RunSaveData { map = map, currentChapter = chapter, chosenLevelIds = route };
        return true;
    }
#endif

    /// <summary>导出关卡进度（存档用）</summary>
    public void ExportTo(RunSaveData data)
    {
        var store = LevelStore.Instance;

        data.selectedLevelId = store.selectedLevelId;
        data.map = store.map;
        data.currentChapter = store.currentChapter;
        data.pendingLevelReward = store.pendingReward;

        data.chosenLevelIds.Clear();
        data.chosenLevelIds.AddRange(store.chosenLevelIds);
    }

    /// <summary>从存档恢复关卡进度：用「最后通关的关卡」重建解锁状态与当前层</summary>
    public void ImportFrom(RunSaveData data)
    {
        var store = LevelStore.Instance;
        ApplyMap(data.map, data.currentChapter);
        store.pendingReward = data.pendingLevelReward;

        store.selectedLevelId = data.selectedLevelId;

        if (data.chosenLevelIds != null) store.chosenLevelIds.AddRange(data.chosenLevelIds);

        // 新大关尚无已通关节点时，保留入口解锁状态。
        string lastLevelId = store.LastClearedLevelId;
        if (!string.IsNullOrEmpty(lastLevelId) && store.allLevels.ContainsKey(lastLevelId))
        {
            UnlockNext(lastLevelId);
        }

        store.Refresh();
    }
}
