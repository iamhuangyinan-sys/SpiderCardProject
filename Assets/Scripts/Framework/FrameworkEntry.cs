using UnityEngine;
using UnityEngine.SceneManagement;
using Framework.Res;
using Framework.Save;
using Framework.Event;
using Framework.Pool;
using Framework.Audio;

namespace Framework
{
    /// <summary>
    /// 框架启动入口 —— 挂载到场景中的空 GameObject 上
    /// 负责统一初始化所有 Store 和 Mgr（按依赖顺序）
    ///
    /// 使用方式：
    /// 1. 在场景中创建空 GameObject，命名为 [FrameworkEntry]
    /// 2. 挂载此脚本
    /// 3. 在 InitFramework() 中添加你的 Store / Mgr 初始化代码
    /// </summary>
    public class FrameworkEntry : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            InitFramework();
        }

        private void InitFramework()
        {
            Debug.Log("[Framework] 开始初始化...");

            // ===== 1. 基础设施（无依赖）=====
            MonoManager.Instance.Init();     // 协程/Update 服务（唯一 Mono）

            // ===== 2. 初始化 Store（数据层，无依赖）=====
            ResStore.Instance.Init();        // 资源缓存数据
            PoolStore.Instance.Init();       // 对象池数据
            // XxxStore.Instance.Init();

            // ===== 3. 初始化 Manager（业务层，依赖 Store）=====
            ResManager.Instance.Init();          // 资源管理器（依赖 ResStore）
            SaveManager.Instance.Init();     // 本地存档管理器
            EventManager.Instance.Init();    // 全局事件中心
            PoolManager.Instance.Init();     // 对象池管理器（依赖 PoolStore + ResManager）
            BgmManager.Instance.Init();      // 背景音乐管理器（依赖 ResManager）
            SoundManager.Instance.Init();    // 音效管理器（依赖 ResManager + PoolManager）
            // XxxManager.Instance.Init();

            // ===== 4. 注册存档路径 =====
            SaveRegistry.RegisterAll();      // → 见 SaveRegistry.cs

            Debug.Log("[Framework] 初始化完成！");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // 按依赖的反序销毁
            BgmManager.Instance.Dispose();
            SoundManager.Instance.Dispose();
            PoolManager.Instance.Dispose();
            EventManager.Instance.Dispose();
            SaveManager.Instance.Dispose();
            ResManager.Instance.Dispose();
            PoolStore.Instance.Dispose();
            ResStore.Instance.Dispose();
            MonoManager.Instance.Dispose();

            Debug.Log("[Framework] 已销毁");
        }

        /// <summary>
        /// 场景加载完成 → 清除切场景即销毁的池
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PoolManager.Instance.ClearOnSceneChange();
        }
    }
}
