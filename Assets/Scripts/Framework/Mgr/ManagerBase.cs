using Framework.Singleton;

namespace Framework.Mgr
{
    /// <summary>
    /// Manager 基类 —— 负责业务逻辑，不存数据，数据交给 Store
    /// 用法：public class CardManager : ManagerBase&lt;CardManager&gt; { }
    /// </summary>
    public abstract class ManagerBase<T> : Singleton<T> where T : class, new()
    {
        public override void Init()
        {
            base.Init();
            OnInit();
        }

        public override void Dispose()
        {
            OnDispose();
            base.Dispose();
        }

        /// <summary>
        /// 子类重写，注册事件、初始化状态等
        /// </summary>
        protected virtual void OnInit() { }

        /// <summary>
        /// 子类重写，清理资源
        /// </summary>
        protected virtual void OnDispose() { }
    }
}
