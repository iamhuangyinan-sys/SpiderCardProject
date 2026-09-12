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

    private void Start()
    {
        CardGameModule.Instance.Init();
        IsInCardScene = true;

        // 进入卡牌场景 = 开始新的一大局：本局资产清零
        RunManager.Instance.StartNewRun();

        EventManager.Instance.AddListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.AddListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
        EventManager.Instance.AddListener(E_EventEnum.OnShopOpen, OnShopOpen);
        EventManager.Instance.AddListener(E_EventEnum.OnShopClosed, OnShopClosed);

        // 进入场景先弹选关，顶部 UI 一并显示
        _levelPanel = UIManager.Instance.Show<LevelPanel>();
        _mainTopPanel = UIManager.Instance.Show<MainTopPanel>();
    }

    /// <summary>选关开局：关选关面板（顶部 UI 保持显示）</summary>
    private void OnLevelStart()
    {
        if (_levelPanel != null) UIManager.Instance.Hide(_levelPanel);
        _mainTopPanel = UIManager.Instance.Show<MainTopPanel>();
    }

    /// <summary>收牌结束（关卡结束）：弹选关面板，顶部游戏 UI 保留不关</summary>
    private void OnLevelEnd()
    {
        _levelPanel = UIManager.Instance.Show<LevelPanel>();
    }

    /// <summary>选中商店关：关选关面板，开商店面板（顶部 UI 保留）</summary>
    private void OnShopOpen()
    {
        if (_levelPanel != null) UIManager.Instance.Hide(_levelPanel);
        _shopPanel = UIManager.Instance.Show<ShopPanel>();
    }

    /// <summary>商店结束：关商店面板，回选关</summary>
    private void OnShopClosed()
    {
        if (_shopPanel != null) UIManager.Instance.Hide(_shopPanel);
        _levelPanel = UIManager.Instance.Show<LevelPanel>();
    }

    private void OnDestroy()
    {
        IsInCardScene = false;

        EventManager.Instance.RemoveListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopOpen, OnShopOpen);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopClosed, OnShopClosed);
        CardGameModule.Instance.Dispose();
    }
}
