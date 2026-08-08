using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Framework.Store;

namespace Framework.Res
{
    // ============================================================
    //  ResInfo —— 资源信息数据结构
    // ============================================================

    /// <summary>
    /// 资源信息基类 —— 用于里氏替换，父类容器装子类对象
    /// </summary>
    public abstract class ResInfoBase
    {
        /// <summary> 引用计数：Load +1，Unload -1 </summary>
        public int refCount;
    }

    /// <summary>
    /// 泛型资源信息 —— 存储资源、异步回调、协程引用
    /// </summary>
    public class ResInfo<T> : ResInfoBase where T : UnityEngine.Object
    {
        /// <summary> 已加载的资源（异步加载完成前为 null） </summary>
        public T asset;

        /// <summary> 异步加载完成后的回调列表 </summary>
        public UnityAction<T> callBack;

        /// <summary> 异步加载协程引用（用于取消） </summary>
        public Coroutine coroutine;

        /// <summary> 引用计数归零时是否立即卸载 </summary>
        public bool isDel;

        public void AddRefCount() => ++refCount;

        public void SubRefCount()
        {
            --refCount;
            if (refCount < 0)
                Debug.LogError($"[ResManager] 资源 {typeof(T).Name} 引用计数 < 0，请检查 Load/Unload 是否配对！");
        }
    }

    // ============================================================
    //  ResStore —— 资源缓存数据层（纯数据，不含加载逻辑）
    // ============================================================

    /// <summary>
    /// 资源缓存数据存储 —— 只有数据的增删查
    /// 所有加载/卸载逻辑在 ResManager 中
    /// </summary>
    public class ResStore : StoreBase<ResStore>
    {
        /// <summary> 资源缓存字典 key = "路径_类型名" </summary>
        private readonly Dictionary<string, ResInfoBase> _resDic = new();
        private readonly object _lock = new();

        protected override void OnInit() { }

        protected override void OnDispose()
        {
            lock (_lock) { _resDic.Clear(); }
        }

        // ==================== 基础操作 ====================

        /// <summary> 是否已存在（含加载中） </summary>
        public bool Contains(string resName)
        {
            lock (_lock) { return _resDic.ContainsKey(resName); }
        }

        /// <summary> 获取资源信息（泛型） </summary>
        public bool TryGet<T>(string resName, out ResInfo<T> info) where T : UnityEngine.Object
        {
            lock (_lock)
            {
                if (_resDic.TryGetValue(resName, out var baseInfo))
                {
                    info = baseInfo as ResInfo<T>;
                    return info != null;
                }
                info = null;
                return false;
            }
        }

        /// <summary> 获取资源信息（非泛型） </summary>
        public bool TryGet(string resName, out ResInfoBase info)
        {
            lock (_lock) { return _resDic.TryGetValue(resName, out info); }
        }

        /// <summary> 添加新资源记录 </summary>
        public void Add(string resName, ResInfoBase info)
        {
            lock (_lock) { _resDic[resName] = info; }
        }

        /// <summary> 移除资源记录 </summary>
        public bool Remove(string resName)
        {
            lock (_lock) { return _resDic.Remove(resName); }
        }

        /// <summary> 清空所有记录 </summary>
        public void Clear()
        {
            lock (_lock) { _resDic.Clear(); }
        }

        // ==================== 批量操作 ====================

        /// <summary> 获取所有引用计数为 0 的 key 列表 </summary>
        public List<string> GetZeroRefKeys()
        {
            lock (_lock)
            {
                var list = new List<string>();
                foreach (var kv in _resDic)
                    if (kv.Value.refCount == 0)
                        list.Add(kv.Key);
                return list;
            }
        }

        /// <summary> 移除所有引用计数为 0 的记录 </summary>
        public void RemoveZeroRef()
        {
            lock (_lock)
            {
                foreach (var key in GetZeroRefKeys())
                    _resDic.Remove(key);
            }
        }
    }
}
