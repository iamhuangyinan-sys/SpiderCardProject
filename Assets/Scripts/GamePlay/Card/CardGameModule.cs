using Framework.Mgr;

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
}
