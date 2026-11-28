using Framework;
using Framework.UI;

/// <summary>
/// 游戏菜单面板 —— 保存并回主菜单 / 放弃本局 / 保存并退出
/// 遮挡场景输入（实现 ISceneInputBlockPanel）
/// </summary>
public partial class MenuPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;
    private bool keepBackground;
    private PanelBackdrop Backdrop => GetComponent<PanelBackdrop>();

    internal void KeepBackground() => keepBackground = true;

    protected override void OnOpen()
    {
        keepBackground = false;
        Backdrop.Show(true);
        comps.btnClose.onClick.AddListener(CloseSelf);
        comps.btnSettings.onClick.AddListener(OnClickSettings);
        comps.btnSaveReturn.onClick.AddListener(OnClickSaveReturn);
        comps.btnGiveUpReturn.onClick.AddListener(OnClickGiveUpReturn);
        comps.btnSaveQuit.onClick.AddListener(OnClickSaveQuit);
    }

    protected override void OnShow()
    {
        keepBackground = false;
        Backdrop.Show(true);
    }

    protected override void OnHide() { }

    protected override bool OnPanelClosing()
    {
        if (!keepBackground) Backdrop.Hide(true);
        return base.OnPanelClosing();
    }

    protected override void OnClose()
    {
        comps.btnClose.onClick.RemoveListener(CloseSelf);
        comps.btnSettings.onClick.RemoveListener(OnClickSettings);
        comps.btnSaveReturn.onClick.RemoveListener(OnClickSaveReturn);
        comps.btnGiveUpReturn.onClick.RemoveListener(OnClickGiveUpReturn);
        comps.btnSaveQuit.onClick.RemoveListener(OnClickSaveQuit);
    }

    private void OnClickSettings()
    {
        if (!IsOpen || IsTransitioning || SceneController.Instance.IsLoading
            || UIManager.Instance.IsOpen<SettingsPanel>()) return;
        KeepBackground();
        SettingsPanel.OpenFrom(this);
    }

    /// <summary>保存并回到主菜单</summary>
    private void OnClickSaveReturn()
    {
        CardGameModule.Instance.SaveRun();
        SceneController.Instance.LoadScene("BeginPanelScene", CloseSelf);
    }

    private void OnClickGiveUpReturn()
    {
        SecondConfirmHelper.OpenFrom(this, "ui_confirm_give_up_tip", GiveUpRun);
    }

    private bool GiveUpRun()
    {
        var scene = SceneController.Instance;
        if (scene.IsLoading || !CardGameModule.Instance.TryDiscardRun()) return false;

        scene.LoadScene("BeginPanelScene", CloseSelf);
        return true;
    }

    /// <summary>确认后保存并退出游戏。</summary>
    private void OnClickSaveQuit() => SecondConfirmHelper.ConfirmQuit(this, () => CardGameModule.Instance.SaveRun());
}
