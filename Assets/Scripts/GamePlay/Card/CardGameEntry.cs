using System.Collections;
using Framework;
using Framework.Event;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 纸牌游戏场景入口 —— 挂载在 CardGameScene 场景的 GameObject 上
/// 场景加载完成后初始化纸牌游戏系统，并管理选关/打牌的 UI 流程
/// </summary>
public class CardGameEntry : MonoBehaviour
{
    /// <summary>卡牌场景是否已就绪（场景中存在 CardGameEntry 且已初始化）</summary>
    public static bool IsInCardScene { get; private set; }

    private LevelPanel _levelPanel;
    private MainTopPanel _mainTopPanel;
    private ShopPanel _shopPanel;

    /// <summary>「等选关面板挡住牌桌后再隐藏牌桌物件」的协程句柄（开新局时取消）</summary>
    private Coroutine _hideTableRoutine;

    private void Start()
    {
        CardGameModule.Instance.Init();
        IsInCardScene = true;

        // 进场景先隐藏牌桌场景物件（发牌堆 / 弃牌堆 / 空列垫底等），选关打牌时再显示
        // 时间点比黑屏遮罩淡出早，所以看不到闪烁
        CardViewManager.Instance.SetTableVisible(false);

        EventManager.Instance.AddListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.AddListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
        EventManager.Instance.AddListener(E_EventEnum.OnShopOpen, OnShopOpen);
        EventManager.Instance.AddListener(E_EventEnum.OnShopClosed, OnShopClosed);
        EventManager.Instance.AddListener(E_EventEnum.OnMapOpen, OnMapOpen);
        EventManager.Instance.AddListener(E_EventEnum.OnMapClose, OnMapClose);

        // 有档读档，无档开新的一大局面
        CardGameModule.Instance.LoadRun();

        // 顶部 UI 常驻显示（放在读档之后，确保层号 / 金币读到的是恢复后的值）
        _mainTopPanel = UIManager.Instance.Show<MainTopPanel>();

        // 存档里停在某一关 → 直接重开这一关（商店关由 SelectLevel 内部弹出商店面板）
        var currentId = LevelStore.Instance.selectedLevelId;
        if (string.IsNullOrEmpty(currentId))
        {
            ShowLevelSelect();
        }
        else
        {
            LevelManager.Instance.SelectLevel(currentId);
        }

        // 内容就绪：告知场景控制器可以淡出黑屏遮罩
        SceneController.Instance.NotifySceneReady();

        // 编辑器模式：控制台入口（面板会被切场景的 DestroyAll 清掉，每个场景入口重新显示）
        ConsoleEntryPanel.ShowInEditor();
    }

    /// <summary>选关开局：关选关面板（顶部 UI 保持显示），显示牌桌场景物件</summary>
    private void OnLevelStart()
    {
        if (_levelPanel != null) UIManager.Instance.Hide(_levelPanel);
        _mainTopPanel = UIManager.Instance.Show<MainTopPanel>();
        SetMapButtonVisible(true);   // 关卡中：可以开地图看路线

        // 上一局残留的「延迟隐藏」（玩家在选关面板飞入途中又开了新局）
        if (_hideTableRoutine != null)
        {
            MonoManager.Instance.StopCoroutine(_hideTableRoutine);
            _hideTableRoutine = null;
        }

        CardViewManager.Instance.SetTableVisible(true);
    }

    /// <summary>收牌结束（关卡结束）：弹选关面板，等它飞入挡住牌桌后再隐藏牌桌物件</summary>
    private void OnLevelEnd()
    {
        ShowLevelSelect();
        _hideTableRoutine = MonoManager.Instance.StartCoroutine(HideTableRoutine());
    }

    /// <summary>等选关面板飞入到位（挡住牌桌）再隐藏，避免玩家看到牌桌物件凭空消失</summary>
    private IEnumerator HideTableRoutine()
    {
        yield return new WaitForSeconds(AnimationHelper.PanelFlyInDuration);

        _hideTableRoutine = null;
        CardViewManager.Instance.SetTableVisible(false);
    }

    /// <summary>选中商店关：关选关面板，开商店面板（顶部 UI 保留）</summary>
    private void OnShopOpen()
    {
        if (_levelPanel != null) UIManager.Instance.Hide(_levelPanel);

        SetMapButtonVisible(true);   // 商店也算「关卡中」，也能开地图看路线
        _shopPanel = UIManager.Instance.Show<ShopPanel>();
    }

    /// <summary>商店结束：关商店面板，回选关</summary>
    private void OnShopClosed()
    {
        if (_shopPanel != null) UIManager.Instance.Hide(_shopPanel);
        ShowLevelSelect();
    }

    // ==================== 选关界面 / 地图 ====================

    /// <summary>显示选关面板（可点关）；顺手把地图按钮藏掉 —— 选关界面本身就是地图</summary>
    private void ShowLevelSelect()
    {
        _levelPanel = UIManager.Instance.Show<LevelPanel>();
        if (_levelPanel == null) return;

        _levelPanel.SetViewOnly(false);
        SetMapButtonVisible(false);
    }

    /// <summary>关卡中打开地图：只读查看，箭头标出当前所在关卡</summary>
    private void OnMapOpen()
    {
        // 选关界面正开着（它本身就是地图）→ 忽略，否则会把它切成只读、关掉后玩家没法选关
        if (_levelPanel != null && _levelPanel.IsOpen && !_levelPanel.IsViewOnly) return;

        _levelPanel = UIManager.Instance.Show<LevelPanel>();
        if (_levelPanel == null) return;

        _levelPanel.SetViewOnly(true);
        SetMapButtonVisible(true);   // 保持可见，再点一次就是关闭
    }

    /// <summary>关闭地图（只关「查看模式」，避免误把选关界面关掉）</summary>
    private void OnMapClose()
    {
        if (_levelPanel != null && _levelPanel.IsViewOnly) UIManager.Instance.Hide(_levelPanel);
    }

    private void SetMapButtonVisible(bool visible)
    {
        if (_mainTopPanel != null) _mainTopPanel.SetMapButtonVisible(visible);
    }

    private void OnDestroy()
    {
        IsInCardScene = false;

        // 关场景时如果「延迟隐藏牌桌」还没跑完，先掐掉（避免它在模块 Dispose 之后再去访问管理器）
        if (_hideTableRoutine != null)
        {
            MonoManager.Instance.StopCoroutine(_hideTableRoutine);
            _hideTableRoutine = null;
        }

        EventManager.Instance.RemoveListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopOpen, OnShopOpen);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopClosed, OnShopClosed);
        EventManager.Instance.RemoveListener(E_EventEnum.OnMapOpen, OnMapOpen);
        EventManager.Instance.RemoveListener(E_EventEnum.OnMapClose, OnMapClose);

        // 关闭本场景自己的面板：必须走 Close 而不是只 Hide，
        // 否则下个场景再进时 Show 只走 OnShow（不重建列表），
        // 而 OnClose 里的解绑事件 / 回收列表项也永远跑不到。
        // 此刻画面已经在黑幕下，看不到关闭过程。
        if (_levelPanel != null) UIManager.Instance.Close(_levelPanel);
        if (_mainTopPanel != null) UIManager.Instance.Close(_mainTopPanel);
        if (_shopPanel != null) UIManager.Instance.Close(_shopPanel);

        CardGameModule.Instance.Dispose();
    }
}
