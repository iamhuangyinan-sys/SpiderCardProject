using System;
using UnityEngine;
using Framework.Singleton;

namespace Framework
{
    /// <summary>
    /// Mono 服务管理器 —— 框架中唯一的 MonoBehaviour 单例
    /// 为纯 C# 的 Manager / Store 提供 MonoBehaviour 能力（协程、Update 等）
    ///
    /// 使用方式：
    ///   MonoManager.Instance.StartCoroutine(MyCoroutine());
    ///   MonoManager.Instance.OnUpdate += MyUpdate;
    /// </summary>
    public class MonoManager : MonoSingleton<MonoManager>
    {
        /// <summary> 每帧更新事件（非 Mono 类可订阅） </summary>
        public event Action OnUpdate;

        /// <summary> LateUpdate 事件 </summary>
        public event Action OnLateUpdate;

        /// <summary> FixedUpdate 事件 </summary>
        public event Action OnFixedUpdate;

        private void Update()        => OnUpdate?.Invoke();
        private void LateUpdate()    => OnLateUpdate?.Invoke();
        private void FixedUpdate()   => OnFixedUpdate?.Invoke();

        protected override void OnDestroy()
        {
            OnUpdate = null;
            OnLateUpdate = null;
            OnFixedUpdate = null;
            base.OnDestroy();
        }

        // StartCoroutine / StopCoroutine / StopAllCoroutines 直接继承自 MonoBehaviour
        // 外部调用：MonoManager.Instance.StartCoroutine(...)
    }
}
