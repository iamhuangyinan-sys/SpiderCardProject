using Framework.UI;
using UnityEngine;
using System.Collections.Generic;

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
        ScrollList testList = comps.listTest;

        // ===== 准备数据 =====
        var dataList = new List<string>
        {
            "1st Item",
            "2nd Item",
            "3rd Item",
            "4th Item",
            "5th Item",
            "6th Item",
            "7th Item",
            "8th Item",
            "9th Item",
            "10th Item",
            "11th Item",
            "12th Item",
            "13th Item",
            "14th Item",
            "15th Item",
            "16th Item",
            "17th Item",
            "18th Item",
            "19th Item",
            "20th Item"
        };

        // ===== 设置渲染回调：每条数据刷新对应 item =====
        testList.OnItemRender = (index, item) =>
        {
            var testItem = item as TestItem;
            testItem.Refresh(dataList[index]);
        };

        // ===== 设条数触发刷新 =====
        testList.ItemCount = dataList.Count;
    }

    protected override void OnShow()
    {
        
    }

    protected override void OnHide()
    {
        
    }

    protected override void OnClose()
    {
        
    }

    private void OnBtnTestClick()
    {
        Debug.Log("[TestPanel] BtnTest Clicked");
    }
}

