using UnityEngine;
using System;

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

        /// <summary>本面板是否实现了「遮挡场景输入」接口（Awake 时转一次，避免每次查询都转）</summary>
        private ISceneInputBlockPanel _sceneInputBlock;
        private Action hiddenCallback;
        private bool closing;
        public bool IsTransitioning { get; protected set; }

        protected virtual void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            _sceneInputBlock = this as ISceneInputBlockPanel;
        }

        /// <summary> 首次打开（UIManager 内部） </summary>
        internal void OpenInternal()
        {
            if (IsOpen) return;
            IsOpen = true;
            closing = false;
            hiddenCallback = null;
            HasOpened = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BindUI();
            OnOpen();
            OnPanelOpened();
            LockSceneInput();
        }

        /// <summary> 隐藏后重新显示（UIManager 内部） </summary>
        internal void ShowInternal()
        {
            if (IsOpen) return;
            IsOpen = true;
            closing = false;
            hiddenCallback = null;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BindUI();
            OnShow();
            OnPanelOpened();
            LockSceneInput();
        }

        /// <summary> 隐藏（保留缓存，UIManager 内部） </summary>
        internal void HideInternal(Action onHidden = null)
        {
            if (!IsOpen) return;
            IsOpen = false;
            hiddenCallback = onHidden;
            OnHide();

            // 子类接管隐藏（如先播关闭动画，动画结束后再自行隐藏）
            if (OnPanelClosing()) return;

            CompleteHide();
        }

        /// <summary> 永久关闭（UIManager 内部） </summary>
        internal void CloseInternal(Action onClosed = null, bool immediate = false)
        {
            if (closing && !immediate) return;
            closing = true;
            hiddenCallback = onClosed;
            bool wasVisible = IsOpen || IsTransitioning;
            IsOpen = false;
            if (!immediate && wasVisible && OnPanelClosing()) return;
            CompleteHide();
        }

        /// <summary>动画结束时调用；完成回调在本帧内交接下一面板。</summary>
        protected void CompleteHide()
        {
            if (IsOpen) return;
            IsTransitioning = false;
            var callback = hiddenCallback;
            hiddenCallback = null;
            if (closing)
            {
                closing = false;
                if (HasOpened) OnClose();
                HasOpened = false;
            }
            gameObject.SetActive(false);
            try { callback?.Invoke(); }
            finally { if (!IsOpen) UnlockSceneInput(); }
        }

        // ==================== 场景输入遮挡（实现 ISceneInputBlockPanel 的面板）====================

        /// <summary>实现了接口才上锁（由生命周期统一调用，子类不用管，也不会被 override 顶掉）</summary>
        private void LockSceneInput()
        {
            if (_sceneInputBlock != null) SceneInputLock.Lock(this);
        }

        /// <summary>解锁（没上过锁时调用也无副作用）</summary>
        private void UnlockSceneInput()
        {
            if (_sceneInputBlock != null) SceneInputLock.Unlock(this);
        }
        // ==================== 面板动画钩子（子类重写） ====================

        /// <summary>
        /// 面板已打开（OnOpen / OnShow 之后调用），子类可在此播入场动画
        /// </summary>
        protected virtual void OnPanelOpened() { }

        /// <summary>
        /// 面板即将隐藏（OnHide 之后调用）
        /// 返回 true 表示子类接管隐藏流程（如等关闭动画播完再自行隐藏），基类不再直接 SetActive(false)
        /// </summary>
        protected virtual bool OnPanelClosing() => false;
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

        public void HideSelf(Action onHidden) => UIManager.Instance.Hide(this, onHidden);

        /// <summary> 关闭自身（移除缓存并销毁） </summary>
        public void CloseSelf()
        {
            UIManager.Instance.Close(this);
        }

        public void CloseSelf(Action onClosed) => UIManager.Instance.Close(this, onClosed);

        /// <summary> 设置可交互（通过 CanvasGroup） </summary>
        public void SetInteractable(bool interactable)
        {
            CanvasGroup.interactable = interactable;
            CanvasGroup.blocksRaycasts = interactable;
        }
    }
}
