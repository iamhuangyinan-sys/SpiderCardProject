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
    /// 框架启动入口 —— 挂载在启动场景 Main 的空 GameObject 上
    /// 负责统一初始化所有 Store 和 Mgr（按依赖顺序），
    /// 初始化完成后（Start）切到主菜单场景 BeginPanelScene。
    ///
    /// 使用方式：
    /// 1. 在启动场景中创建空 GameObject，命名为 [FrameworkEntry]
    /// 2. 挂载此脚本
    /// 3. 在 InitFramework() 中添加你的 Store / Mgr 初始化代码
    ///
    /// 注意：Main 只由应用启动进入，业务跳转不要指回 Main（否则会再触发一次初始化流程）；
    /// 本对象 DontDestroyOnLoad，切到 BeginPanelScene 后依然常驻。
    /// </summary>
    public class FrameworkEntry : MonoBehaviour
    {
        /// <summary>启动场景名（只负责初始化框架，业务不会跳回来）</summary>
        private const string BootstrapSceneName = "Main";

        /// <summary>启动场景加载完成后进入的第一个业务场景</summary>
        private const string StartupSceneName = "BeginPanelScene";

        /// <summary>框架是否已初始化（防止多个场景重复挂载导致重复初始化）</summary>
        private static bool _initialized;

        /// <summary>本实例是否为主实例（重复实例不参与销毁清理）</summary>
        private bool _isPrimary;

        /// <summary>
        /// 关闭 Domain Reload 时静态字段会跨 Play 残留，这里统一重置，
        /// 否则第二次 Play 会被误判为「重复实例」
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _initialized = false;
        }

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

        private void Start()
        {
            // 重复实例在 Awake 里已 Destroy(gameObject)，但销毁是在帧末执行，Start 仍可能被调用一次
            if (!_isPrimary) return;

            // 此时所有 Manager 已在 Awake 中初始化完毕（所有 Awake 早于 Start）
            // 启动场景没有画面，直接压黑再切，避免看到启动场景一闪
            SceneController.Instance.LoadScene(StartupSceneName, fadeIn: false);
        }

#if UNITY_EDITOR
        /// <summary>编辑器里直接从业务场景 Play 时，先回启动场景把框架跑起来</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RedirectToBootstrapSceneInEditor()
        {
            if (_initialized) return;

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.name == BootstrapSceneName) return;

            Debug.LogWarning($"[Framework] 直接从 {active.name} 进入 Play，先加载 {BootstrapSceneName} 初始化框架");
            UnityEngine.SceneManagement.SceneManager.LoadScene(BootstrapSceneName);
        }
#endif

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

            // MonoManager 是唯一的 Mono 单例：退出播放 / 关游戏时 OnApplicationQuit 已经把它置空，
            // 这里只能用 InstanceOrNull（Instance 会返回 null 并刷警告）；真正的清理在它自己的 OnDestroy
            var mono = MonoManager.InstanceOrNull;
            if (mono != null) mono.Dispose();

            Debug.Log("[Framework] 已销毁");
        }
    }
}
