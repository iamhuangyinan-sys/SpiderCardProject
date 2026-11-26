using Framework.Mgr;
using Framework.Save;
using Framework.UI;
#if UNITY_EDITOR
using System.IO;
using Framework;
#endif

/// <summary>
/// 纸牌游戏门面 —— 集中管理纸牌游戏各子系统的初始化与销毁
/// 业务入口只需调用本类的 Init / Dispose，无需关心内部各模块的依赖顺序
///
/// 归属范围：关卡（含关卡图标）、商店、一大局资产（金币）、卡牌本身；
/// 框架级模块（ResManager / UIManager / EventManager 等）仍由 FrameworkEntry 初始化。
/// </summary>
public class CardGameModule : ManagerBase<CardGameModule>
{
    protected override void OnInit()
    {
        // ===== 数据层（无依赖）=====
        LevelStore.Instance.Init();      // 关卡数据
        RunStore.Instance.Init();        // 一大局资产数据
        LittleShopStore.Instance.Init();
        ShopStore.Instance.Init();       // 商店数据
        CardsStore.Instance.Init();      // 牌局数据
        BuffStore.Instance.Init();

        // ===== 业务逻辑层 =====
        LevelManager.Instance.Init();    // 关卡逻辑（依赖 LevelStore + ConfigHelper）
        RunManager.Instance.Init();      // 一大局资产逻辑（依赖 RunStore）
        LevelResManager.Instance.Init(); // 关卡图标（依赖 ResManager + ConfigHelper）
        LittleShopManager.Instance.Init();
        ShopManager.Instance.Init();     // 商店逻辑（依赖 ShopStore + RunManager + ConfigHelper）

        // ===== 卡牌（资源 → 对象池 → 表现 → 规则 → 逻辑）=====
        CardResManager.Instance.Init();
        CardPoolManager.Instance.Init();
        CardViewManager.Instance.Init();
        CardRuleManager.Instance.Init();
        BuffManager.Instance.Init();
        BuffResManager.Instance.Init();
        CardsManager.Instance.Init();
    }

    protected override void OnDispose()
    {
        // 按依赖反序销毁
        CardsManager.Instance.Dispose();
        BuffResManager.Instance.Dispose();
        BuffManager.Instance.Dispose();
        CardRuleManager.Instance.Dispose();
        CardViewManager.Instance.Dispose();
        CardPoolManager.Instance.Dispose();
        CardResManager.Instance.Dispose();

        LittleShopManager.Instance.Dispose();
        ShopManager.Instance.Dispose();
        LevelResManager.Instance.Dispose();
        RunManager.Instance.Dispose();
        LevelManager.Instance.Dispose();

        BuffStore.Instance.Dispose();
        CardsStore.Instance.Dispose();
        LittleShopStore.Instance.Dispose();
        ShopStore.Instance.Dispose();
        RunStore.Instance.Dispose();
        LevelStore.Instance.Dispose();
    }

    /// <summary>
    /// 当前是否可接受卡牌操作输入（所有卡牌 Controller 统一用这个判断）
    /// 锁输入：非对局进行中（选关 / 商店界面）/ 收牌结算中 / 动画播放中 / 全屏面板遮挡中
    /// （最后一条必需：UI 只挡得住 UI，挡不住走 Physics.Raycast 的卡牌拾取）
    /// </summary>
    public bool CanCardInput =>
        CardsManager.Instance.IsPlaying
        && !CardsManager.Instance.IsSettling
        && !CardsManager.Instance.IsResolvingBoard
        && !CardViewManager.Instance.IsAnimating
        && !SceneInputLock.IsLocked;

    /// <summary>开始一局新游戏（指定列数与牌包数）</summary>
    public void StartNewGame(int columnCount, int pocketCount)
    {
        BuffManager.Instance.Clear();
        CardsManager.Instance.StartNewGame(columnCount, pocketCount);
    }

#if UNITY_EDITOR || SPIDERCARD_TEST_BUILD
    /// <summary>【测试】直接完成当前关卡（达成接龙次数 + 收牌结算）</summary>
    public void DebugCompleteLevel()
    {
        CardsManager.Instance.DebugCompleteLevel();
    }

#endif

#if UNITY_EDITOR
    private bool _reloadingForLevelJump;

    /// <summary>保存调试选关进度并通过续玩入口重载当前场景。</summary>
    public bool TryJumpToLevelSelection(int chapter, int layer, out string error)
    {
        if (!CardGameEntry.IsInCardScene || !RunManager.Instance.HasStarted)
        {
            error = "请先开始或继续一局游戏";
            return false;
        }

        var scene = SceneController.Instance;
        if (scene.IsLoading || _reloadingForLevelJump
            || CardsManager.Instance.IsSettling || CardsManager.Instance.IsResolvingBoard
            || UIManager.Instance.GetPanel<LevelPanel>()?.IsEnteringLevel == true)
        {
            error = "请等待进关、牌桌操作或场景切换完成";
            return false;
        }

        if (!LevelManager.Instance.TryCreateDebugLevelSelection(chapter, layer, out var data))
        {
            error = $"本局不存在关卡 {chapter}-{layer}";
            return false;
        }

        RunManager.Instance.ExportTo(data);
        try
        {
            SaveManager.Instance.Save(E_SaveCustomEnum.RunData, data);
        }
        catch (System.Exception exception) when (exception is IOException or System.UnauthorizedAccessException)
        {
            error = $"无法保存跳关进度（{exception.Message}）";
            return false;
        }

        _reloadingForLevelJump = true;
        scene.LoadScene(scene.CurrentScene, () => _reloadingForLevelJump = false);
        error = null;
        return true;
    }
#endif

    // ==================== 存档 ====================

    /// <summary>把本局进度写入存档（金币 / 牌组 / 关卡进度 / 商店状态）</summary>
    public void SaveRun()
    {
#if UNITY_EDITOR
        // 等旧场景销毁期间，延迟执行的选关或牌桌流程不能覆盖跳关存档。
        if (_reloadingForLevelJump) return;
#endif
        // 选初始牌组期间尚无新局，退出或其他保存入口不能生成半成品存档。
        if (!RunManager.Instance.HasStarted) return;
        var data = new RunSaveData();
        RunManager.Instance.ExportTo(data);
        LevelManager.Instance.ExportTo(data);
        ShopManager.Instance.ExportTo(data);
        LittleShopManager.Instance.ExportTo(data);

        SaveManager.Instance.Save(E_SaveCustomEnum.RunData, data);
    }

    /// <summary>
    /// 读取存档并恢复到各子系统（需在 Init 之后调用）
    /// 返回是否成功恢复；无存档时由场景入口显示初始牌组选择
    /// </summary>
    public bool LoadRun()
    {
        if (!SaveManager.Instance.HasKey(E_SaveCustomEnum.RunData))
        {
            return false;
        }

        var data = SaveManager.Instance.Load<RunSaveData>(E_SaveCustomEnum.RunData);
        if (data == null || string.IsNullOrEmpty(data.difficultyId)
            || ConfigHelper.Get<DifficultyConfig>(data.difficultyId) == null)
        {
            return false;
        }

        if (!LevelGenerationHelper.Validate(data.map, out string mapError))
        {
            UnityEngine.Debug.LogError($"[Level] 存档地图校验失败：{mapError}");
            return false;
        }
        if (data.currentChapter < 1 || data.currentChapter > data.map.chapters.Count)
        {
            UnityEngine.Debug.LogError($"[Level] 存档大关编号无效：{data.currentChapter}");
            return false;
        }

        RunManager.Instance.ImportFrom(data);
        LevelManager.Instance.ImportFrom(data);
        ShopManager.Instance.ImportFrom(data);
        LittleShopManager.Instance.ImportFrom(data);
        // 结算动画期间退出时，补发奖励并完成待处理的大关切换，一起保存结算结果。
        if (LevelManager.Instance.SettleCompletedLevel())
        {
            SaveRun();
        }
        return true;
    }
}
