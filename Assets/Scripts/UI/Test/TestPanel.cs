using Framework.UI;
using UnityEngine;

/// <summary>
/// 测试面板 —— 普通面板
/// </summary>
public partial class TestPanel : NormalPanel
{
    protected override void OnOpen()
    {
        TestButtonModule testBtnMod = comps.modTest;
        testBtnMod.comps.btnTest.onClick.AddListener(OnBtnTestClick);
        testBtnMod.comps.imgTest.color = Color.red;
        testBtnMod.comps.txtTest.text = "hello";

        comps.btnClose.onClick.AddListener(() => CloseSelf());
        comps.txtTest.text = "Hello";
        
    }

    protected override void OnShow()
    {
        Debug.Log("[TestPanel] OnShow");
    }

    protected override void OnHide()
    {
        Debug.Log("[TestPanel] OnHide");
    }

    protected override void OnClose()
    {
        Debug.Log("[TestPanel] OnClose");
    }

    private void OnBtnTestClick()
    {
        Debug.Log("[TestPanel] BtnTest Clicked");
    }
}

