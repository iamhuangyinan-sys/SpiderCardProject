using System;
using Framework.UI;

public partial class WinPanel : NormalPanel, ISceneInputBlockPanel
{
    public Action OnReturn;
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;

    protected override void OnOpen() => comps.btnReturn.onClick.AddListener(OnClickReturn);
    protected override void OnShow() { }
    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnReturn.onClick.RemoveListener(OnClickReturn);
        OnReturn = null;
    }

    private void OnClickReturn()
    {
        if (IsOpen && !IsTransitioning) OnReturn?.Invoke();
    }
}
