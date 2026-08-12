using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 滚动列表 Cell 基类 —— 挂载在 Cell 预制体根节点上
    ///
    /// 生命周期：
    ///   OnActivate() → 从池取出显示（绑定数据）
    ///   OnRecycle()  → 放回池隐藏（清理数据）
    /// </summary>
    public abstract class BaseListItem : MonoBehaviour
    {
        public RectTransform RectTransform { get; private set; }

        /// <summary> 当前显示的逻辑索引 </summary>
        public int DataIndex { get; internal set; }

        /// <summary> 是否正在使用中 </summary>
        internal bool IsInUse { get; set; }

        /// <summary> 所属的池 </summary>
        internal ListItemPool OwnerPool { get; set; }

        private void Awake()
        {
            RectTransform = (RectTransform)transform;

            // 绑定自身控件（在 OnActivate 之前完成）
            var binder = GetComponent<UIBinder>();
            binder?.Bind();
        }

        /// <summary> 从池取出激活时（子类必须实现，绑定数据） </summary>
        public abstract void OnActivate();

        /// <summary> 放回池隐藏时（子类必须实现，清理数据） </summary>
        public abstract void OnRecycle();
    }
}
