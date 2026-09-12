using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 全屏面板 —— 铺满屏幕、盖住整个场景的面板（展示牌、菜单等）
    ///
    /// 与 NormalPanel 并列（都直接继承 BasePanel）：
    /// - 不带飞入 / 飞出等开关动画（全屏面板用飞入不合适）
    /// - 打开期间自动遮挡场景输入：UI 之间的点击由 EventSystem 挡住，但场景里走
    ///   Physics.Raycast 的拾取（卡牌拖拽等）挡不住，所以由本类统一维护「有无全屏面板打开」，
    ///   业务层查 FullScreenPanel.IsBlockingInput 主动锁输入
    /// </summary>
    public abstract class FullScreenPanel : BasePanel
    {
        /// <summary>当前已打开的全屏面板数量（多个全屏面板可叠加打开）</summary>
        private static int _openCount;

        /// <summary>本面板是否已计入遮挡计数（防止重复加 / 减）</summary>
        private bool _counted;

        /// <summary>是否有全屏面板正在遮挡场景（业务层锁输入用）</summary>
        public static bool IsBlockingInput => _openCount > 0;

        /// <summary>面板已打开（Open / Show 都会走到这里）→ 计入遮挡</summary>
        protected override void OnPanelOpened()
        {
            if (_counted) return;

            _counted = true;
            _openCount++;
        }

        /// <summary>面板即将隐藏（Hook，子类不会绕过）→ 取消遮挡</summary>
        protected override bool OnPanelClosing()
        {
            ReleaseInputBlock();

            return false;   // 无关闭动画，不接管隐藏流程
        }

        /// <summary>兜底：Close 路径（关闭并销毁）不经过 OnPanelClosing，在销毁时确保计数归零</summary>
        protected void OnDestroy()
        {
            ReleaseInputBlock();
        }

        /// <summary>取消遮挡计数（重复调用无副作用）</summary>
        private void ReleaseInputBlock()
        {
            if (!_counted) return;

            _counted = false;
            _openCount = Mathf.Max(0, _openCount - 1);
        }
    }
}
