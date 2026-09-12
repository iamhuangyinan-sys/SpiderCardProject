using Framework.Mgr;
using Framework.Save;
using Framework.UI;

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
        ShopStore.Instance.Init();       // 商店数据
        CardsStore.Instance.Init();      // 牌局数据

        // ===== 业务逻辑层 =====
        LevelManager.Instance.Init();    // 关卡逻辑（依赖 LevelStore + ConfigHelper）
        RunManager.Instance.Init();      // 一大局资产逻辑（依赖 RunStore）
        LevelResManager.Instance.Init(); // 关卡图标（依赖 ResManager + ConfigHelper）
        ShopManager.Instance.Init();     // 商店逻辑（依赖 ShopStore + RunManager + ConfigHelper）

        // ===== 卡牌（资源 → 对象池 → 表现 → 规则 → 逻辑）=====
        CardResManager.Instance.Init();
        CardPoolManager.Instance.Init();
        CardViewManager.Instance.Init();
        CardRuleManager.Instance.Init();
        CardsManager.Instance.Init();
    }

    protected override void OnDispose()
    {
        // 按依赖反序销毁
        CardsManager.Instance.Dispose();
        CardRuleManager.Instance.Dispose();
        CardViewManager.Instance.Dispose();
        CardPoolManager.Instance.Dispose();
        CardResManager.Instance.Dispose();

        ShopManager.Instance.Dispose();
        LevelResManager.Instance.Dispose();
        RunManager.Instance.Dispose();
        LevelManager.Instance.Dispose();

        CardsStore.Instance.Dispose();
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
        && !CardViewManager.Instance.IsAnimating
        && !FullScreenPanel.IsBlockingInput;

    /// <summary>开始一局新游戏（指定列数与牌包数）</summary>
    public void StartNewGame(int columnCount, int pocketCount)
    {
        CardsManager.Instance.StartNewGame(columnCount, pocketCount);
    }

    /// <summary>【测试】直接完成当前关卡（达成接龙次数 + 收牌结算）</summary>
    public void DebugCompleteLevel()
    {
        CardsManager.Instance.DebugCompleteLevel();
    }

    // ==================== 存档 ====================

    /// <summary>把本局进度写入存档（金币 / 牌组 / 关卡进度 / 商店状态）</summary>
    public void SaveRun()
    {
        var data = new RunSaveData();
        RunManager.Instance.ExportTo(data);
        LevelManager.Instance.ExportTo(data);
        ShopManager.Instance.ExportTo(data);

        SaveManager.Instance.Save(E_SaveCustomEnum.RunData, data);
    }

    /// <summary>
    /// 读取存档并恢复到各子系统（需在 Init 之后调用）
    /// 无存档时开始新的一大局面
    /// </summary>
    public void LoadRun()
    {
        if (!SaveManager.Instance.HasKey(E_SaveCustomEnum.RunData))
        {
            RunManager.Instance.StartNewRun();
            return;
        }

        var data = SaveManager.Instance.Load<RunSaveData>(E_SaveCustomEnum.RunData);
        if (data == null)
        {
            RunManager.Instance.StartNewRun();
            return;
        }

        RunManager.Instance.ImportFrom(data);
        LevelManager.Instance.ImportFrom(data);
        ShopManager.Instance.ImportFrom(data);
    }
}
