using System;
using Framework.UI;

public partial class SecondConfirmPanel : NormalPanel, ISceneInputBlockPanel
{
    private string _tipId;
    private bool _useOwnBackdrop;
    private bool _closeBeforeConfirm;
    private Action _onConfirm;
    private Action _onCancel;
    private PanelBackdrop Backdrop => GetComponent<PanelBackdrop>();

    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;

    internal void Configure(string tipId, bool useOwnBackdrop, bool closeBeforeConfirm, Action onConfirm, Action onCancel)
    {
        _tipId = tipId;
        _useOwnBackdrop = useOwnBackdrop;
        _closeBeforeConfirm = closeBeforeConfirm;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
    }

    protected override void OnOpen()
    {
        comps.btnConfirm.onClick.AddListener(OnClickConfirm);
        comps.btnCancel.onClick.AddListener(OnClickCancel);
        RefreshContent();
    }

    protected override void OnShow() => RefreshContent();
    protected override void OnHide()
    {
        _onConfirm = null;
        _onCancel = null;
    }

    protected override bool OnPanelClosing()
    {
        if (_useOwnBackdrop) Backdrop.Hide(true);
        return base.OnPanelClosing();
    }

    protected override void OnClose()
    {
        comps.btnConfirm.onClick.RemoveListener(OnClickConfirm);
        comps.btnCancel.onClick.RemoveListener(OnClickCancel);
    }

    private void RefreshContent()
    {
        comps.txtTip.GetComponent<LocalizedText>().SetId(_tipId);
        if (_useOwnBackdrop) Backdrop.Show(true);
        else Backdrop.Hide(false);
    }

    private void OnClickConfirm()
    {
        if (!IsOpen || IsTransitioning || _onConfirm == null) return;

        var onConfirm = _onConfirm;
        _onConfirm = null;
        if (_closeBeforeConfirm) CloseSelf(onConfirm);
        else onConfirm();
    }

    private void OnClickCancel()
    {
        if (IsOpen && !IsTransitioning) CloseSelf(_onCancel);
    }
}
