using Framework;
using Framework.UI;
using UnityEngine;

public partial class BeginPanel : NormalPanel
{
    protected override void OnOpen()
    {
        comps.btnStart.onClick.AddListener(() =>
        {
            SceneController.Instance.LoadScene("CardGameScene", CloseSelf);
        });
        comps.btnQuit.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }
    protected override void OnClose()
    {

    }

    protected override void OnShow()
    {

    }

    protected override void OnHide()
    {

    }
}
