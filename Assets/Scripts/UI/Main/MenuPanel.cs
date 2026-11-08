using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 游戏菜单面板 —— 保存并回主菜单 / 保存并退出
/// 遮挡场景输入（实现 ISceneInputBlockPanel）
/// </summary>
public partial class MenuPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;
    private bool keepBackground;
    private PanelBackdrop Backdrop => GetComponent<PanelBackdrop>();

    protected override void OnOpen()
    {
        keepBackground = false;
        Backdrop.Show(true);
        comps.btnClose.onClick.AddListener(CloseSelf);
        comps.btnSettings.onClick.AddListener(OnClickSettings);
        comps.btnSaveReturn.onClick.AddListener(OnClickSaveReturn);
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
        comps.btnSaveQuit.onClick.RemoveListener(OnClickSaveQuit);
    }

    private void OnClickSettings()
    {
        if (!IsOpen || IsTransitioning || SceneController.Instance.IsLoading
            || UIManager.Instance.IsOpen<SettingsPanel>()) return;
        keepBackground = true;
        SettingsPanel.OpenFrom(this);
    }

    /// <summary>保存并回到主菜单</summary>
    private void OnClickSaveReturn()
    {
        CardGameModule.Instance.SaveRun();
        SceneController.Instance.LoadScene("BeginPanelScene", CloseSelf);
    }

    /// <summary>保存并退出游戏</summary>
    private void OnClickSaveQuit()
    {
        CardGameModule.Instance.SaveRun();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
