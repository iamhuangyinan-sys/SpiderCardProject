using Framework.UI;
using UnityEngine;

/// <summary>
/// 测试面板 —— 普通面板
/// </summary>
public partial class TestPanel : NormalPanel
{
    protected override void OnOpen()
    {
        base.OnOpen();
        Comps.btnTest.onClick.AddListener(OnBtnTestClick);
        Comps.btnClose.onClick.AddListener(() => CloseSelf());
        Comps.txtTest.text = "Hello";
        Comps.imgTest.color = Color.red;
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("[TestPanel] OnShow");
    }

    protected override void OnHide()
    {
        base.OnHide();
        Debug.Log("[TestPanel] OnHide");
    }

    protected override void OnClose()
    {
        base.OnClose();
        Debug.Log("[TestPanel] OnClose");
    }

    private void OnBtnTestClick()
    {
        Debug.Log("[TestPanel] BtnTest Clicked");
    }
}

