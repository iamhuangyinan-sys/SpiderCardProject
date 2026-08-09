namespace Framework.UI
{
    /// <summary>
    /// UI 层级枚举 —— 数值越大越靠前
    /// </summary>
    public enum E_UILayerEnum
    {
        Bottom  = 0,   // 背景层
        Normal  = 100, // 普通面板
        Popup   = 200, // 弹窗
        Top     = 300, // 顶层提示
        System  = 400, // 系统级
    }
}
