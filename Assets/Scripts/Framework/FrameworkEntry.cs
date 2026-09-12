using UnityEngine;
using Framework.Res;
using Framework.Save;
using Framework.Event;
using Framework.Pool;
using Framework.Audio;
using Framework.UI;

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
        /// <summary>框架是否已初始化（防止多个场景重复挂载导致重复初始化）</summary>
        private static bool _initialized;

        /// <summary>本实例是否为主实例（重复实例不参与销毁清理）</summary>
        private bool _isPrimary;

        private void Awake()
        {
            // 多个场景都挂了 FrameworkEntry 时，只保留最先初始化的那个
            if (_initialized)
            {
                Debug.LogWarning("[Framework] 已存在 FrameworkEntry（它已 DontDestroyOnLoad），销毁重复实例；场景里只需保留一个");
                Destroy(gameObject);
                return;
            }

            _initialized = true;
            _isPrimary = true;

            DontDestroyOnLoad(gameObject);
            InitFramework();
        }

        private void InitFramework()
        {
            Debug.Log("[Framework] 游戏框架开始初始化...");

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
            SceneController.Instance.Init(); // 场景控制器（依赖 PoolManager + MonoManager）
            UIManager.Instance.Init();       // UI 管理器（依赖 ResManager）
            ConsoleCommandManager.Instance.Init(); // 控制台命令（注册回调，执行时访问业务层）
            // XxxManager.Instance.Init();

            // ===== 4. 注册各类配置 =====
            SaveRegistry.RegisterAll();      // → 见 SaveRegistry.cs
            UIConfig.RegisterAll();          // → 见 UIConfig.cs

            Debug.Log("[Framework] 游戏框架初始化完成");
        }

        private void OnDestroy()
        {
            if (!_isPrimary) return;   // 被销毁的重复实例不做清理

            _initialized = false;

            // 按依赖的反序销毁
            ConsoleCommandManager.Instance.Dispose();
            SceneController.Instance.Dispose();
            UIManager.Instance.Dispose();
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
    }
}
