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
        //comps.btnTest.onClick.AddListener(OnBtnTestClick);
        //comps.imgTest.color = Color.red;
        TestButtonModule testBtnMod = comps.modTest;
        testBtnMod.comps.btnTest.onClick.AddListener(OnBtnTestClick);
        testBtnMod.comps.imgTest.color = Color.red;
        testBtnMod.comps.txtTest.text = "hello";

        comps.btnClose.onClick.AddListener(() => CloseSelf());
        comps.txtTest.text = "Hello";
        
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

