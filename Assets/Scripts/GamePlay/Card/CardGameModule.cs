using Framework.Mgr;

/// <summary>
/// 纸牌游戏门面 —— 集中管理纸牌游戏各子系统的初始化与销毁
/// 业务入口只需调用本类的 Init / Dispose，无需关心内部各模块的依赖顺序
/// </summary>
public class CardGameModule : ManagerBase<CardGameModule>
{
    protected override void OnInit()
    {
        // 按依赖顺序初始化：数据层 → 资源 → 对象池 → 表现层 → 逻辑层
        CardsStore.Instance.Init();
        CardResManager.Instance.Init();
        CardPoolManager.Instance.Init();
        CardViewManager.Instance.Init();
        CardsManager.Instance.Init();
    }

    protected override void OnDispose()
    {
        // 按依赖反序销毁
        CardsManager.Instance.Dispose();
        CardViewManager.Instance.Dispose();
        CardPoolManager.Instance.Dispose();
        CardResManager.Instance.Dispose();
        CardsStore.Instance.Dispose();
    }
}
