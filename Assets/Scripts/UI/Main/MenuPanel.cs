using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 游戏菜单面板 —— 保存并回主菜单 / 保存并退出
/// </summary>
public partial class MenuPanel : FullScreenPanel
{
    protected override void OnOpen()
    {
        comps.btnClose.onClick.AddListener(CloseSelf);
        comps.btnSaveReturn.onClick.AddListener(OnClickSaveReturn);
        comps.btnSaveQuit.onClick.AddListener(OnClickSaveQuit);
    }

    protected override void OnShow() { }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnClose.onClick.RemoveListener(CloseSelf);
        comps.btnSaveReturn.onClick.RemoveListener(OnClickSaveReturn);
        comps.btnSaveQuit.onClick.RemoveListener(OnClickSaveQuit);
    }

    /// <summary>保存并回到主菜单</summary>
    private void OnClickSaveReturn()
    {
        CardGameModule.Instance.SaveRun();
        SceneController.Instance.LoadScene("Main", CloseSelf);
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
