using System;
using Framework.Singleton;

namespace Framework.Store
{
    /// <summary>
    /// Store 基类 —— 负责数据存储，通过事件通知数据变更
    /// 用法：public class CardStore : StoreBase&lt;CardStore&gt; { }
    /// </summary>
    public abstract class StoreBase<T> : Singleton<T> where T : class, new()
    {
        /// <summary>
        /// 数据变更事件 —— UI/Mgr 可订阅此事件来刷新界面
        /// </summary>
        public event Action OnDataChanged;

        /// <summary>
        /// 触发数据变更通知
        /// </summary>
        protected void NotifyDataChanged()
        {
            OnDataChanged?.Invoke();
        }

        public override void Init()
        {
            base.Init();
            OnInit();
        }

        public override void Dispose()
        {
            OnDispose();
            OnDataChanged = null;
            base.Dispose();
        }

        /// <summary>
        /// 子类重写此方法，初始化数据
        /// </summary>
        protected virtual void OnInit() { }

        /// <summary>
        /// 子类重写此方法，清理资源
        /// </summary>
        protected virtual void OnDispose() { }
    }
}
