using System.Collections.Generic;

namespace Framework.UI
{
    /// <summary>
    /// 弹窗基类 —— 栈式管理，后打开的在上层，关闭时自动弹出栈顶
    /// 继承此类：ConfirmDialog、SettingPanel、TipPanel 等
    /// </summary>
    public abstract class PopupPanel : BasePanel
    {
    }
}
