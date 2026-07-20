using UnityEngine;

namespace Framework.Singleton
{
    /// <summary>
    /// MonoBehaviour 单例基类
    /// 用法：public class AudioManager : MonoSingleton&lt;AudioManager&gt; { }
    /// </summary>
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isQuitting;

        public static T Instance
        {
            get
            {
                if (_isQuitting)
                {
                    Debug.LogWarning($"[MonoSingleton] {typeof(T).Name} 已销毁，返回 null");
                    return null;
                }

                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance = FindAnyObjectByType<T>();
                        if (_instance == null)
                        {
                            var go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }

            Init();
        }

        /// <summary>
        /// 初始化（由 FrameworkEntry 统一调用，也可由 Awake 自动触发）
        /// </summary>
        public virtual void Init() { }

        /// <summary>
        /// 释放资源（由 FrameworkEntry 统一调用）
        /// </summary>
        public virtual void Dispose() { }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
        }
    }
}
