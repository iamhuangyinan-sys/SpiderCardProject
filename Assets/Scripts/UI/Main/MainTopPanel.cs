using Framework.Event;
using Framework.UI;
using UnityEngine;

public partial class MainTopPanel : NormalPanel
{
    protected override void OnOpen()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnStraightCountChanged, RefreshProgress);
        RefreshProgress();
    }

    protected override void OnShow()
    {
        RefreshProgress();
    }

    protected override void OnHide()
    {
        
    }

    protected override void OnClose()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnStraightCountChanged, RefreshProgress);
    }

    /// <summary>刷新接龙进度 "x / y"</summary>
    private void RefreshProgress()
    {
        var store = CardsStore.Instance;
        comps.txtLevelProgress.text = $"{store.straightCount} / {store.needStraightNum}";
    }
}
