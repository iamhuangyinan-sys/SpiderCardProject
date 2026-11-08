using Framework;
using Framework.Save;
using Framework.UI;
using UnityEngine;

public partial class BeginPanel : NormalPanel
{
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;

    private const string GameSceneName = "CardGameScene";

    private PanelBackdrop Backdrop => GetComponent<PanelBackdrop>();

    protected override void OnOpen()
    {
        Backdrop.Show(false);
        comps.btnStart.onClick.AddListener(OnClickStart);
        comps.btnContinue.onClick.AddListener(OnClickContinue);
        comps.btnQuit.onClick.AddListener(OnClickQuit);
        comps.btnSetting.onClick.AddListener(OnClickSettings);

        RefreshContinueButton();
        RefreshVersion();
    }

    protected override void OnShow()
    {
        Backdrop.Show(false);
        RefreshContinueButton();
        RefreshVersion();
    }

    protected override void OnHide()
    {

    }

    protected override void OnClose()
    {
        Backdrop.Hide(false);
        comps.btnStart.onClick.RemoveListener(OnClickStart);
        comps.btnContinue.onClick.RemoveListener(OnClickContinue);
        comps.btnQuit.onClick.RemoveListener(OnClickQuit);
        comps.btnSetting.onClick.RemoveListener(OnClickSettings);
    }

    private void RefreshVersion()
    {
        comps.txtVersion.text = $"v {BuildInfoHelper.GetVersion()}";
    }

    /// <summary>有存档才显示「继续游戏」</summary>
    private void RefreshContinueButton()
    {
        bool hasSave = SaveManager.Instance.HasKey(E_SaveCustomEnum.RunData);
        comps.btnContinue.gameObject.SetActive(hasSave);
    }

    /// <summary>开始新游戏：立即删除旧存档，再进场景选择牌组</summary>
    private void OnClickStart()
    {
        if (SceneController.Instance.IsLoading) return;
        SaveManager.Instance.DeleteKey(E_SaveCustomEnum.RunData);
        CardGameEntry.RequestNewRun = true;
        SceneController.Instance.LoadScene(GameSceneName, CloseSelf);
    }

    /// <summary>继续游戏：读档接着玩</summary>
    private void OnClickContinue()
    {
        if (SceneController.Instance.IsLoading) return;
        CardGameEntry.RequestNewRun = false;
        SceneController.Instance.LoadScene(GameSceneName, CloseSelf);
    }

    private void OnClickSettings() => SettingsPanel.OpenFrom(this);

    private void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
