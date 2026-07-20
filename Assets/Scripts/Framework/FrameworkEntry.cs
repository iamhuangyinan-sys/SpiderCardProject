using UnityEngine;
using Framework.Res;

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
            InitFramework();
        }

        private void InitFramework()
        {
            Debug.Log("[Framework] 开始初始化...");

            // ===== 1. 基础设施（无依赖）=====
            MonoManager.Instance.Init();     // 协程/Update 服务（唯一 Mono）

            // ===== 2. 初始化 Store（数据层，无依赖）=====
            ResStore.Instance.Init();        // 资源缓存数据
            // XxxStore.Instance.Init();

            // ===== 3. 初始化 Manager（业务层，依赖 Store）=====
            ResMgr.Instance.Init();          // 资源管理器（依赖 ResStore）
            // XxxManager.Instance.Init();

            Debug.Log("[Framework] 初始化完成！");
        }

        private void OnDestroy()
        {
            // 按依赖的反序销毁
            // XxxManager.Instance.Dispose();
            ResMgr.Instance.Dispose();
            // XxxStore.Instance.Dispose();
            ResStore.Instance.Dispose();
            MonoManager.Instance.Dispose();

            Debug.Log("[Framework] 已销毁");
        }
    }
}
