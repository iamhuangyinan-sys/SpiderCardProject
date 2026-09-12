using Framework;
using Framework.Save;
using Framework.UI;
using UnityEngine;

public partial class BeginPanel : NormalPanel
{
    private const string GameSceneName = "CardGameScene";

    protected override void OnOpen()
    {
        comps.btnStart.onClick.AddListener(OnClickStart);
        comps.btnContinue.onClick.AddListener(OnClickContinue);
        comps.btnQuit.onClick.AddListener(OnClickQuit);

        RefreshContinueButton();
    }

    protected override void OnShow()
    {
        RefreshContinueButton();
    }

    protected override void OnHide()
    {

    }

    protected override void OnClose()
    {
        comps.btnStart.onClick.RemoveListener(OnClickStart);
        comps.btnContinue.onClick.RemoveListener(OnClickContinue);
        comps.btnQuit.onClick.RemoveListener(OnClickQuit);
    }

    /// <summary>有存档才显示「继续游戏」</summary>
    private void RefreshContinueButton()
    {
        bool hasSave = SaveManager.Instance.HasKey(E_SaveCustomEnum.RunData);
        comps.btnContinue.gameObject.SetActive(hasSave);
    }

    /// <summary>开始新游戏：删档，重新开始一整局</summary>
    private void OnClickStart()
    {
        SaveManager.Instance.DeleteKey(E_SaveCustomEnum.RunData);
        SceneController.Instance.LoadScene(GameSceneName, CloseSelf);
    }

    /// <summary>继续游戏：读档接着玩</summary>
    private void OnClickContinue()
    {
        SceneController.Instance.LoadScene(GameSceneName, CloseSelf);
    }

    private void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
