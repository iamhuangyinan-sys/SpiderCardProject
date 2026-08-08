using System.Collections.Generic;
using UnityEngine;
using Framework.Store;

namespace Framework.Pool
{
    /// <summary>
    /// 单个对象池的数据
    /// </summary>
    public class PoolData
    {
        /// <summary> 池名称（取自预制体名） </summary>
        public string poolName;

        /// <summary> 原始预制体引用（模板） </summary>
        public GameObject prefab;

        /// <summary> 池的根节点 {prefabName}Pool </summary>
        public Transform poolRoot;

        /// <summary> 可用的闲置对象 </summary>
        public readonly Stack<GameObject> available = new();

        /// <summary> 正在使用中的对象集合 </summary>
        public readonly HashSet<GameObject> inUse = new();

        /// <summary> 池配置（来自预制体上的 PoolConfig） </summary>
        public PoolConfig config;

        /// <summary> 当前池中总对象数 </summary>
        public int TotalCount => available.Count + inUse.Count;
    }

    /// <summary>
    /// 对象池数据层 —— 管理所有池的数据
    /// </summary>
    public class PoolStore : StoreBase<PoolStore>
    {
        /// <summary> prefab 路径 → 池数据 </summary>
        private readonly Dictionary<string, PoolData> _pools = new();

        /// <summary> 实例物体 → 所属池路径（反查用） </summary>
        private readonly Dictionary<GameObject, string> _instanceToPath = new();

        /// <summary> 对象池总根节点 </summary>
        private Transform _rootPool;

        protected override void OnInit()
        {
            var go = new GameObject("ObjectPool");
            Object.DontDestroyOnLoad(go);
            go.transform.SetAsFirstSibling();
            _rootPool = go.transform;
        }

        protected override void OnDispose()
        {
            foreach (var kv in _pools)
            {
                if (kv.Value.poolRoot != null)
                    Object.Destroy(kv.Value.poolRoot.gameObject);
            }
            _pools.Clear();
            _instanceToPath.Clear();

            if (_rootPool != null)
                Object.Destroy(_rootPool.gameObject);
        }

        // ==================== 池操作 ====================

        /// <summary> 获取池（可能为 null） </summary>
        public bool TryGetPool(string prefabPath, out PoolData pool)
        {
            return _pools.TryGetValue(prefabPath, out pool);
        }

        /// <summary> 创建新池（预制体已加载后调用） </summary>
        public PoolData CreatePool(string prefabPath, GameObject prefab)
        {
            var cfg = prefab.GetComponent<PoolConfig>();
            if (cfg == null) cfg = prefab.AddComponent<PoolConfig>(); // 没挂就默认

            string poolName = prefab.name + "Pool";
            var poolGo = new GameObject(poolName);
            poolGo.transform.SetParent(_rootPool);

            var pool = new PoolData
            {
                poolName = poolName,
                prefab = prefab,
                poolRoot = poolGo.transform,
                config = cfg,
            };

            _pools[prefabPath] = pool;
            return pool;
        }

        /// <summary> 添加可用对象到池 </summary>
        public void PushAvailable(string prefabPath, GameObject obj)
        {
            if (_pools.TryGetValue(prefabPath, out var pool))
            {
                obj.SetActive(false);
                obj.transform.SetParent(pool.poolRoot);
                pool.available.Push(obj);
                pool.inUse.Remove(obj);
            }
        }

        /// <summary> 从池中取出一个可用对象（可能为 null） </summary>
        public GameObject PopAvailable(string prefabPath)
        {
            if (_pools.TryGetValue(prefabPath, out var pool) && pool.available.Count > 0)
            {
                var obj = pool.available.Pop();
                pool.inUse.Add(obj);
                return obj;
            }
            return null;
        }

        /// <summary> 标记对象为使用中 </summary>
        public void MarkInUse(string prefabPath, GameObject obj)
        {
            if (_pools.TryGetValue(prefabPath, out var pool))
            {
                pool.inUse.Add(obj);
                _instanceToPath[obj] = prefabPath;
            }
        }

        /// <summary> 反查对象属于哪个池 </summary>
        public bool TryGetPoolPath(GameObject obj, out string prefabPath)
        {
            return _instanceToPath.TryGetValue(obj, out prefabPath);
        }

        // ==================== 清理 ====================

        /// <summary> 清除所有切场景即销毁的池 </summary>
        public void ClearScenePools()
        {
            var toRemove = new List<string>();
            foreach (var kv in _pools)
            {
                if (kv.Value.config.destroyOnSceneLoad)
                    toRemove.Add(kv.Key);
            }

            foreach (var path in toRemove)
                DestroyPool(path);
        }

        /// <summary> 清除所有池 </summary>
        public void ClearAllPools()
        {
            foreach (var path in new List<string>(_pools.Keys))
                DestroyPool(path);
        }

        /// <summary> 销毁整个池（立即执行，避免延迟销毁导致层级错乱） </summary>
        private void DestroyPool(string prefabPath)
        {
            if (!_pools.TryGetValue(prefabPath, out var pool)) return;

            // 立即销毁所有闲置实例
            while (pool.available.Count > 0)
            {
                var obj = pool.available.Pop();
                if (obj != null) Object.DestroyImmediate(obj);
            }

            // 立即销毁使用中的实例
            foreach (var obj in pool.inUse)
            {
                _instanceToPath.Remove(obj);
                if (obj != null) Object.DestroyImmediate(obj);
            }
            pool.inUse.Clear();

            // 立即销毁池根节点
            if (pool.poolRoot != null)
                Object.DestroyImmediate(pool.poolRoot.gameObject);

            _pools.Remove(prefabPath);
        }
    }
}
