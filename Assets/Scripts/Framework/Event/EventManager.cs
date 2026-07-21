using System;
using System.Collections.Generic;
using UnityEngine;
using Framework.Mgr;

namespace Framework.Event
{
    /// <summary>
    /// 全局事件中心 —— 基于枚举的事件总线
    ///
    /// 使用方式：
    ///   EventManager.Instance.AddListener(E_EventEnum.OnGameStart, OnGameStart);
    ///   EventManager.Instance.AddListener&lt;int&gt;(E_EventEnum.OnScoreChanged, OnScoreChanged);
    ///
    ///   // 取消（在 OnDisable / OnDestroy 中，避免内存泄漏）
    ///   EventManager.Instance.RemoveListener(E_EventEnum.OnGameStart, OnGameStart);
    ///   EventManager.Instance.RemoveListener&lt;int&gt;(E_EventEnum.OnScoreChanged, OnScoreChanged);
    ///
    ///   // 触发（任何地方）
    ///   EventManager.Instance.Dispatch(E_EventEnum.OnGameStart);
    ///   EventManager.Instance.Dispatch(E_EventEnum.OnScoreChanged, 999);
    ///
    /// 注意：
    ///   注册和取消必须一一对应，否则回调泄漏或丢失
    ///   建议在 OnEnable 注册、OnDisable 取消
    /// </summary>
    public class EventManager : ManagerBase<EventManager>
    {
        /// <summary> 无参事件：E_EventEnum → Action 列表 </summary>
        private readonly Dictionary<E_EventEnum, Action> _eventsNoArg = new();

        /// <summary> 带参事件：E_EventEnum → Delegate 列表（运行时按类型匹配） </summary>
        private readonly Dictionary<E_EventEnum, Delegate> _eventsWithArg = new();

        // ==================== 添加监听 ====================

        /// <summary>
        /// 添加无参事件监听
        /// </summary>
        public void AddListener(E_EventEnum key, Action callback)
        {
            if (!_eventsNoArg.ContainsKey(key))
                _eventsNoArg[key] = null;
            _eventsNoArg[key] += callback;
        }

        /// <summary>
        /// 添加带参事件监听（一个 Key 只能有一种参数类型）
        /// </summary>
        public void AddListener<T>(E_EventEnum key, Action<T> callback)
        {
            if (_eventsWithArg.TryGetValue(key, out var del))
            {
                _eventsWithArg[key] = Delegate.Combine(del, callback);
            }
            else
            {
                _eventsWithArg[key] = callback;
            }
        }

        // ==================== 移除监听 ====================

        /// <summary>
        /// 移除无参事件监听
        /// </summary>
        public void RemoveListener(E_EventEnum key, Action callback)
        {
            if (_eventsNoArg.TryGetValue(key, out var del))
            {
                del -= callback;
                if (del == null)
                    _eventsNoArg.Remove(key);
                else
                    _eventsNoArg[key] = del;
            }
        }

        /// <summary>
        /// 移除带参事件监听
        /// </summary>
        public void RemoveListener<T>(E_EventEnum key, Action<T> callback)
        {
            if (_eventsWithArg.TryGetValue(key, out var del))
            {
                del = Delegate.Remove(del, callback);
                if (del == null)
                    _eventsWithArg.Remove(key);
                else
                    _eventsWithArg[key] = del;
            }
        }

        // ==================== 派发事件 ====================

        /// <summary>
        /// 派发无参事件
        /// </summary>
        public void Dispatch(E_EventEnum key)
        {
            if (_eventsNoArg.TryGetValue(key, out var del))
            {
                del?.Invoke();
            }
        }

        /// <summary>
        /// 派发带参事件
        /// </summary>
        public void Dispatch<T>(E_EventEnum key, T arg)
        {
            if (_eventsWithArg.TryGetValue(key, out var del))
            {
                (del as Action<T>)?.Invoke(arg);
            }
        }

        // ==================== 清理 ====================

        /// <summary>
        /// 清除某个 Key 下的所有监听
        /// </summary>
        public void Clear(E_EventEnum key)
        {
            _eventsNoArg.Remove(key);
            _eventsWithArg.Remove(key);
        }

        /// <summary>
        /// 清除所有事件监听
        /// </summary>
        public void ClearAll()
        {
            _eventsNoArg.Clear();
            _eventsWithArg.Clear();
        }

        protected override void OnDispose()
        {
            ClearAll();
        }
    }
}
