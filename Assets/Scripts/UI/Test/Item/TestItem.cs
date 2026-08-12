using Framework.UI;
using UnityEngine;

/// <summary>
/// 测试列表项 —— 挂载到 Item 预制体上
/// </summary>
public partial class TestItem : BaseListItem
{
    public override void OnActivate()
    {
        comps.imgFront.color = Color.red;
        comps.txtBack.text = "Item";
    }

    public override void OnRecycle()
    {
        
    }

    public void Refresh(string text)
    {
        comps.txtBack.text = text;
    }
}

