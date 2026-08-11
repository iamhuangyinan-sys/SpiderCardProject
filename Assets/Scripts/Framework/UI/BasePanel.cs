using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// UI 面板基类 —— 所有 UI 面板继承此类
    ///
    /// 生命周期：Open → OnOpen() → ... → Close → OnClose()
    ///
    /// 子类重写 OnOpen / OnClose 处理打开关闭逻辑
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BasePanel : MonoBehaviour
    {
        /// <summary> 所属层级 </summary>
        [HideInInspector] public E_UILayerEnum Layer { get; internal set; }

        /// <summary> 是否已打开 </summary>
        public bool IsOpen { get; private set; }

        /// <summary> 是否首次打开过（区分 Open 和 Show） </summary>
        internal bool HasOpened { get; set; }

        /// <summary> 预制体路径（UIManager 内部使用） </summary>
        internal string PrefabPath { get; set; }

        /// <summary> 面板 RectTransform </summary>
        public RectTransform RectTransform => (RectTransform)transform;

        protected CanvasGroup CanvasGroup { get; private set; }

        protected virtual void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary> 首次打开（UIManager 内部） </summary>
        internal void OpenInternal()
        {
            if (IsOpen) return;
            IsOpen = true;
            HasOpened = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BindUI();
            OnOpen();
        }

        /// <summary> 隐藏后重新显示（UIManager 内部） </summary>
        internal void ShowInternal()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BindUI();
            OnShow();
        }

        /// <summary> 隐藏（保留缓存，UIManager 内部） </summary>
        internal void HideInternal()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnHide();
            gameObject.SetActive(false);
        }

        /// <summary> 永久关闭（UIManager 内部） </summary>
        internal void CloseInternal()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnClose();
            gameObject.SetActive(false);
        }

        // ==================== 生命周期（子类重写） ====================

        /// <summary> 首次打开 </summary>
        protected abstract void OnOpen();

        /// <summary> 隐藏后重新显示 </summary>
        protected abstract void OnShow();

        /// <summary> 隐藏（保留缓存） </summary>
        protected abstract void OnHide();

        /// <summary> 关闭销毁 </summary>
        protected abstract void OnClose();

        // ==================== 快捷操作 ====================

        /// <summary> 绑定 UI 控件（自动调用 UIBinder） </summary>
        private void BindUI()
        {
            var binder = GetComponent<UIBinder>();
            if (binder != null)
                binder.Bind();
        }

        /// <summary> 隐藏自身（保留缓存） </summary>
        public void HideSelf()
        {
            UIManager.Instance.Hide(this);
        }

        /// <summary> 关闭自身（移除缓存并销毁） </summary>
        public void CloseSelf()
        {
            UIManager.Instance.Close(this);
        }

        /// <summary> 设置可交互（通过 CanvasGroup） </summary>
        public void SetInteractable(bool interactable)
        {
            CanvasGroup.interactable = interactable;
            CanvasGroup.blocksRaycasts = interactable;
        }
    }
}
