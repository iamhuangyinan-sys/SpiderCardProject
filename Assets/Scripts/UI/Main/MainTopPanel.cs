using Framework.Event;
using Framework.UI;
using UnityEngine;

public partial class MainTopPanel : NormalPanel
{
    protected override void OnOpen()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnStraightCountChanged, RefreshProgress);
        EventManager.Instance.AddListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.AddListener(E_EventEnum.OnCardsCollected, OnLevelEnd);

        RefreshLevelInfo();
        SetProgressVisible(false);   // 选关状态不显示进度
    }

    protected override void OnShow()
    {
        RefreshProgress();
        RefreshLevelInfo();
    }

    protected override void OnHide()
    {
        
    }

    protected override void OnClose()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnStraightCountChanged, RefreshProgress);
        EventManager.Instance.RemoveListener(E_EventEnum.OnLevelStart, OnLevelStart);
        EventManager.Instance.RemoveListener(E_EventEnum.OnCardsCollected, OnLevelEnd);
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
}
