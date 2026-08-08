using UnityEngine;
using UnityEngine.Events;
using Framework.Mgr;
using Framework.Res;

namespace Framework.Pool
{
    /// <summary>
    /// 对象池管理器 —— 通过 ResManager 加载预制体，管理游戏物体的复用
    ///
    /// 场景结构：
    ///   ObjectPool（DontDestroyOnLoad）
    ///     ├── CardPrefabPool
    ///     │     ├── CardPrefab(Clone) [inactive]
    ///     │     └── CardPrefab(Clone) [inactive]
    ///     └── BulletPrefabPool
    ///           └── BulletPrefab(Clone) [inactive]
    ///
    /// 使用方式：
    ///   PoolManager.Instance.SpawnAsync("Card/CardPrefab", card => { card.transform.position = ...; });
    ///   PoolManager.Instance.Despawn(gameObject);
    ///
    /// 池配置：在预制体上挂 PoolConfig 组件即可
    /// </summary>
    public class PoolManager : ManagerBase<PoolManager>
    {
        // ==================== 同步生成 ====================

        /// <summary>
        /// 同步生成（预制体需已加载或正在缓存中）
        /// </summary>
        /// <param name="prefabPath">Resources 下的预制体路径</param>
        /// <returns>生成的 GameObject，失败返回 null</returns>
        public GameObject Spawn(string prefabPath)
        {
            // 先从池中取
            var obj = PoolStore.Instance.PopAvailable(prefabPath);
            if (obj != null)
            {
                obj.SetActive(true);
                return obj;
            }

            // 池中没有 → 加载预制体并实例化
            var prefab = ResManager.Instance.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PoolManager] 预制体不存在: Resources/{prefabPath}");
                return null;
            }

            return SpawnFromPrefab(prefabPath, prefab);
        }

        // ==================== 异步生成 ====================

        /// <summary>
        /// 异步生成（推荐，避免卡顿）
        /// </summary>
        /// <param name="prefabPath">Resources 下的预制体路径</param>
        /// <param name="onSpawned">生成完成回调（参数为生成的 GameObject）</param>
        public void SpawnAsync(string prefabPath, UnityAction<GameObject> onSpawned)
        {
            // 先从池中取
            var obj = PoolStore.Instance.PopAvailable(prefabPath);
            if (obj != null)
            {
                obj.SetActive(true);
                onSpawned?.Invoke(obj);
                return;
            }

            // 异步加载预制体
            ResManager.Instance.LoadAsync<GameObject>(prefabPath, prefab =>
            {
                if (prefab == null)
                {
                    Debug.LogError($"[PoolManager] 预制体不存在: Resources/{prefabPath}");
                    onSpawned?.Invoke(null);
                    return;
                }

                var instance = SpawnFromPrefab(prefabPath, prefab);
                onSpawned?.Invoke(instance);
            });
        }

        // ==================== 回收 ====================

        /// <summary>
        /// 回收对象到池中
        /// </summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;

            if (!PoolStore.Instance.TryGetPoolPath(obj, out string prefabPath))
            {
                Debug.LogWarning($"[PoolManager] 回收失败：{obj.name} 不属于任何对象池，直接销毁");
                Object.Destroy(obj);
                return;
            }

            PoolStore.Instance.PushAvailable(prefabPath, obj);
        }

        // ==================== 清理 ====================

        /// <summary>
        /// 清除切场景即销毁的池（一般在场景切换时调用）
        /// </summary>
        public void ClearOnSceneChange()
        {
            PoolStore.Instance.ClearScenePools();
        }

        /// <summary>
        /// 清除所有池
        /// </summary>
        public void ClearAll()
        {
            PoolStore.Instance.ClearAllPools();
        }

        // ==================== 内部 ====================

        private GameObject SpawnFromPrefab(string prefabPath, GameObject prefab)
        {
            // 确保池已创建
            if (!PoolStore.Instance.TryGetPool(prefabPath, out var pool))
                pool = PoolStore.Instance.CreatePool(prefabPath, prefab);

            // 检查容量限制
            if (pool.config.maxSize > 0 && pool.TotalCount >= pool.config.maxSize)
            {
                Debug.LogWarning($"[PoolManager] 池 [{pool.poolName}] 已达最大容量 {pool.config.maxSize}，无法生成新对象");
                return null;
            }

            // 实例化
            var instance = Object.Instantiate(prefab, pool.poolRoot);
            instance.name = prefab.name;

            // 标记为使用中
            PoolStore.Instance.MarkInUse(prefabPath, instance);

            // 预生成剩余（首次创建池时）
            if (pool.config.preloadCount > 0 && pool.TotalCount < pool.config.preloadCount)
            {
                int need = pool.config.preloadCount - pool.TotalCount;
                for (int i = 0; i < need; i++)
                {
                    var preObj = Object.Instantiate(prefab, pool.poolRoot);
                    preObj.name = prefab.name;
                    PoolStore.Instance.PushAvailable(prefabPath, preObj);
                }
            }

            return instance;
        }
    }
}
