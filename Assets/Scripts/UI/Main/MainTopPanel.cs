using Framework.Event;
using Framework.UI;
using UnityEngine;

public partial class MainTopPanel : NormalPanel
{
    /// <summary>地图当前是否开着（btn_Map 用它做开 / 关切换）</summary>
    private bool _mapOpened;

    protected override void OnOpen()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnStraightCountChanged, RefreshProgress);
        EventManager.Instance.AddListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.AddListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
        EventManager.Instance.AddListener(E_EventEnum.OnShopClosed, RefreshLevelInfo);
        EventManager.Instance.AddListener(E_EventEnum.OnCoinChanged, RefreshCoinInfo);
        EventManager.Instance.AddListener(E_EventEnum.OnMapOpen, OnMapOpened);
        EventManager.Instance.AddListener(E_EventEnum.OnMapClose, OnMapClosed);

        comps.btnCardBuild.onClick.AddListener(OnClickCardBuild);
        comps.btnMenu.onClick.AddListener(OnClickMenu);
        comps.btnMap.onClick.AddListener(OnClickMap);

        RefreshLevelInfo();
        RefreshCoinInfo();
        SetProgressVisible(false);   // 选关状态不显示进度
        SetMapButtonVisible(false);  // 选关界面本身就是地图，默认藏起来
    }

    protected override void OnShow()
    {
        RefreshProgress();
        RefreshLevelInfo();
        RefreshCoinInfo();
    }

    protected override void OnHide()
    {
        
    }

    protected override void OnClose()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnStraightCountChanged, RefreshProgress);
        EventManager.Instance.RemoveListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopClosed, RefreshLevelInfo);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCoinChanged, RefreshCoinInfo);
        EventManager.Instance.RemoveListener(E_EventEnum.OnMapOpen, OnMapOpened);
        EventManager.Instance.RemoveListener(E_EventEnum.OnMapClose, OnMapClosed);

        comps.btnCardBuild.onClick.RemoveListener(OnClickCardBuild);
        comps.btnMenu.onClick.RemoveListener(OnClickMenu);
        comps.btnMap.onClick.RemoveListener(OnClickMap);
    }

    /// <summary>查看当前牌组</summary>
    private void OnClickCardBuild()
    {
        ShowCardsPanel.ShowCards(RunManager.Instance.Deck);
    }

    /// <summary>打开菜单（保存 / 回主菜单 / 退出）</summary>
    private void OnClickMenu()
    {
        // 只挡收牌结算期间：此时关卡已完成但奖励还没发，存档会留下坏档
        // （选关界面 / 商店 / 打牌中随时可开，牌局本身不入档）
        if (CardsManager.Instance.IsSettling) return;

        UIManager.Instance.Show<MenuPanel>();
    }

    /// <summary>地图按钮：打开 / 关闭地图（再点一次关闭）</summary>
    private void OnClickMap()
    {
        // 收牌结算期间不开：此时关卡已完成但奖励还没发，容易和进度刷新打架
        if (CardsManager.Instance.IsSettling) return;

        EventManager.Instance.Dispatch(_mapOpened ? E_EventEnum.OnMapClose : E_EventEnum.OnMapOpen);
    }

    private void OnMapOpened() => _mapOpened = true;

    private void OnMapClosed() => _mapOpened = false;

    /// <summary>地图按钮显隐（选关界面不显示 —— 那里本身就是地图）</summary>
    public void SetMapButtonVisible(bool visible)
    {
        if (comps.btnMap != null) comps.btnMap.gameObject.SetActive(visible);
    }

    /// <summary>开始一局：显示接龙进度并刷新</summary>
    private void OnLevelStart()
    {
        SetProgressVisible(true);
        RefreshProgress();
    }

    /// <summary>一局结束（收牌结算完成）：隐藏接龙进度 + 刷新层号</summary>
    private void OnLevelEnd()
    {
        SetProgressVisible(false);
        RefreshLevelInfo();
    }

    /// <summary>接龙进度显示开关</summary>
    private void SetProgressVisible(bool visible)
    {
        if (comps.txtLevelProgress != null)
        {
            comps.txtLevelProgress.gameObject.SetActive(visible);
        }
    }

    /// <summary>刷新接龙进度 "x / y"</summary>
    private void RefreshProgress()
    {
        var store = CardsStore.Instance;
        comps.txtLevelProgress.text = $"{store.straightCount} / {store.needStraightNum}";
    }

    /// <summary>刷新层数信息（显示当前层，只显示数字）</summary>
    private void RefreshLevelInfo()
    {
        comps.txtLevelInfo.text = LevelStore.Instance.currentLayer.ToString();
    }

    /// <summary>刷新金币数量（只显示数字）</summary>
    private void RefreshCoinInfo()
    {
        comps.txtCoinInfo.text = RunManager.Instance.Coin.ToString();
    }
}
