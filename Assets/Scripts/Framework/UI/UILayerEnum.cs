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

    /// <summary>
    /// 提示框相对于目标的位置方向（HoverTipPanel）
    /// 首选方向放不下时，会自动按 Down → Up → Right → Left 的顺序换方向
    /// </summary>
    public enum E_TipDirection
    {
        /// <summary>在目标下方</summary>
        Down = 0,

        /// <summary>在目标上方</summary>
        Up = 1,

        /// <summary>在目标右侧</summary>
        Right = 2,

        /// <summary>在目标左侧</summary>
        Left = 3,
    }
}
