namespace Framework.UI
{
    /// <summary>
    /// 「打开期间遮挡场景输入」行为接口 —— 标记接口，不需要实现任何成员
    ///
    /// 解决的问题：
    ///   UI 之间的点击由 EventSystem 挡住，但牌桌走的是场景拾取
    ///   （CardController 自己 Physics.RaycastAll 判定命中的牌），uGUI 的遮罩挡不住它。
    ///   所以需要一层「有没有遮挡型面板打开着」的状态，业务层据此锁输入
    ///   （CardGameModule.CanCardInput 查 SceneInputLock.IsLocked）。
    ///
    /// 为什么是接口而不是 FullScreenPanel 那种抽象基类：
    ///   面板还要继承 NormalPanel 拿飞入 / 飞出动画，而 NormalPanel 和 FullScreenPanel
    ///   都要 override OnPanelOpened / OnPanelClosing —— 两者并列就只能二选一。
    ///   做成接口后，动画与输入遮挡互不干扰。
    ///
    /// 生命周期由 BasePanel 统一挂钩（Open / Show 上锁，Hide / Close 解锁），
    /// 子类不用写一行代码，也不会被子类的 override 顶掉。
    ///
    /// 用法：
    ///   public partial class MenuPanel : NormalPanel, ISceneInputBlockPanel
    /// </summary>
    public interface ISceneInputBlockPanel { }
}
