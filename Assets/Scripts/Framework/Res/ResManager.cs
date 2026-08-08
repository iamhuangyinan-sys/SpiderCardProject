using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Framework.Mgr;

namespace Framework.Res
{
    // ============================================================
    //  ResManager —— Resources 资源管理器（纯逻辑层）
    //  数据存储在 ResStore 中，ResManager 只负责 Load / Unload 逻辑
    // ============================================================

    /// <summary>
    /// Resources 资源管理器 —— 纯逻辑，数据交给 ResStore
    ///
    /// 核心机制：引用计数 + 字典缓存 + 同步/异步互转
    ///
    /// 使用方式：
    ///   var prefab = ResManager.Instance.Load<GameObject>("UI/CardPanel");
    ///   ResManager.Instance.LoadAsync<Sprite>("Card/SpadeA", onLoaded);
    ///   ResManager.Instance.UnloadAsset<GameObject>("UI/CardPanel");
    /// </summary>
    public class ResManager : ManagerBase<ResManager>
    {
        // ==================== 同步加载 ====================

        /// <summary>
        /// 同步加载 Resources 下的资源（已加载则直接返回缓存）
        /// </summary>
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            string resName = MakeKey<T>(path);
            var store = ResStore.Instance;

            if (!store.TryGet(resName, out ResInfo<T> resInfo))
            {
                // 首次加载：同步加载 → 缓存到 Store → refCount++
                T res = Resources.Load<T>(path);
                if (res == null)
                {
                    Debug.LogError($"[ResManager] 资源不存在: Resources/{path} 类型={typeof(T).Name}");
                    return null;
                }

                var info = new ResInfo<T> { asset = res };
                info.AddRefCount();
                store.Add(resName, info);
                return res;
            }

            resInfo.AddRefCount();

            // 正在异步加载中 → 取消协程，改为同步返回
            if (resInfo.asset == null)
            {
                MonoManager.Instance.StopCoroutine(resInfo.coroutine);
                T res = Resources.Load<T>(path);
                resInfo.asset = res;
                resInfo.callBack?.Invoke(res);
                resInfo.callBack = null;
                resInfo.coroutine = null;
                return res;
            }

            // 已加载 → 直接返回
            return resInfo.asset;
        }

        // ==================== 异步加载 ====================

        /// <summary>
        /// 异步加载 Resources 下的资源（泛型版本）
        /// </summary>
        public void LoadAsync<T>(string path, UnityAction<T> callBack) where T : UnityEngine.Object
        {
            string resName = MakeKey<T>(path);
            var store = ResStore.Instance;

            if (!store.TryGet(resName, out ResInfo<T> resInfo))
            {
                var info = new ResInfo<T>();
                info.AddRefCount();
                info.callBack += callBack;
                store.Add(resName, info);
                info.coroutine = MonoManager.Instance.StartCoroutine(DoLoadAsync<T>(path, resName));
                return;
            }

            resInfo.AddRefCount();

            if (resInfo.asset == null)
                resInfo.callBack += callBack;   // 还在加载中，排队等回调
            else
                callBack?.Invoke(resInfo.asset); // 已加载，立即回调
        }

        private IEnumerator DoLoadAsync<T>(string path, string resName) where T : UnityEngine.Object
        {
            var rq = Resources.LoadAsync<T>(path);
            yield return rq;

            if (!ResStore.Instance.TryGet(resName, out ResInfo<T> info)) yield break;

            info.asset = rq.asset as T;

            if (info.refCount == 0)
            {
                // 加载过程中引用计数已归零 → 直接卸载
                UnloadInternal<T>(resName, info, isDel: true, isSub: false);
            }
            else
            {
                info.callBack?.Invoke(info.asset);
                info.callBack = null;
                info.coroutine = null;
            }
        }

        // ==================== 卸载 ====================

        /// <summary>
        /// 卸载指定资源
        /// </summary>
        public void UnloadAsset<T>(string path, bool isDel = false, UnityAction<T> callBack = null, bool isSub = true) where T : UnityEngine.Object
        {
            string resName = MakeKey<T>(path);

            if (!ResStore.Instance.TryGet(resName, out ResInfo<T> info)) return;

            if (callBack != null && info.asset == null)
                info.callBack -= callBack;

            UnloadInternal(resName, info, isDel, isSub);
        }

        private void UnloadInternal<T>(string resName, ResInfo<T> info, bool isDel, bool isSub) where T : UnityEngine.Object
        {
            if (isSub) info.SubRefCount();
            info.isDel = isDel;

            if (info.asset != null && info.refCount == 0 && info.isDel)
            {
                ResStore.Instance.Remove(resName);
                Resources.UnloadAsset(info.asset);
            }
        }

        // ==================== 全局卸载 ====================

        /// <summary>
        /// 异步卸载所有未使用的 Resources 资源
        /// </summary>
        public void UnloadUnusedAssets(UnityAction callBack)
        {
            MonoManager.Instance.StartCoroutine(DoUnloadUnusedAssets(callBack));
        }

        private IEnumerator DoUnloadUnusedAssets(UnityAction callBack)
        {
            // 移除所有引用计数为 0 的缓存记录
            ResStore.Instance.RemoveZeroRef();

            var ao = Resources.UnloadUnusedAssets();
            yield return ao;
            callBack?.Invoke();
        }

        /// <summary>
        /// 清空所有缓存并卸载资源
        /// </summary>
        public void ClearAll(UnityAction callBack)
        {
            MonoManager.Instance.StartCoroutine(DoClearAll(callBack));
        }

        private IEnumerator DoClearAll(UnityAction callBack)
        {
            ResStore.Instance.Clear();
            var ao = Resources.UnloadUnusedAssets();
            yield return ao;
            callBack?.Invoke();
        }

        // ==================== 查询 ====================

        /// <summary>
        /// 获取某个资源的当前引用计数
        /// </summary>
        public int GetRefCount<T>(string path) where T : UnityEngine.Object
        {
            return ResStore.Instance.TryGet(MakeKey<T>(path), out ResInfo<T> info) ? info.refCount : 0;
        }

        protected override void OnDispose()
        {
            ResStore.Instance.Clear();
        }

        // ==================== 工具方法 ====================

        private static string MakeKey<T>(string path) => path + "_" + typeof(T).Name;
    }
}

