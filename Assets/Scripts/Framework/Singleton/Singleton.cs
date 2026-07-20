using System;

namespace Framework.Singleton
{
    /// <summary>
    /// 普通 C# 类单例基类（非 MonoBehaviour）
    /// 用法：public class MyClass : Singleton&lt;MyClass&gt; { }
    /// </summary>
    public class Singleton<T> where T : class, new()
    {
        private static T _instance;
        private static readonly object _lock = new object();

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new T();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 初始化（由 FrameworkEntry 统一调用）
        /// </summary>
        public virtual void Init() { }

        /// <summary>
        /// 销毁时释放资源
        /// </summary>
        public virtual void Dispose()
        {
            _instance = null;
        }
    }
}
