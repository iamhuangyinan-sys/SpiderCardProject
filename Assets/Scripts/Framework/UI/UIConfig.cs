using System;
using System.Collections.Generic;

namespace Framework.UI
{
    /// <summary>
    /// UI 面板注册信息
    /// </summary>
    public class UIPanelInfo
    {
        public Type PanelType;
        public string PrefabPath;
        public E_UILayerEnum Layer;
    }

    /// <summary>
    /// UI 配置注册中心 —— 集中注册所有 UI 面板的类型、预制体路径、层级
    ///
    /// 使用方式：
    ///   1. 在 RegisterAll() 中添加你的面板
    ///   2. 业务代码中直接 UIManager.Instance.Show&lt;MainPanel&gt;()
    ///      UIManager 会自动从注册表查找路径和层级
    /// </summary>
    public static class UIConfig
    {
        private static readonly Dictionary<Type, UIPanelInfo> _registry = new();

        /// <summary>
        /// 注册所有 UI 面板（由 FrameworkEntry 调用）
        /// </summary>
        public static void RegisterAll()
        {
            // ===== 示例（基础路径 Prefab/UI/ 自动拼接） =====

            // Register<SettingPanel>("Popup/SettingPanel", E_UILayerEnum.Popup);
            // Register<ConfirmDialog>("Popup/ConfirmDialog", E_UILayerEnum.Popup);
            // Register<HudPanel>("HudPanel", E_UILayerEnum.Normal);

            // ===== 测试 =====
            Register<TestPanel>("Test/TestPanel", E_UILayerEnum.Normal);
            // ===== 主界面 =====
            Register<BeginPanel>("Main/BeginPanel", E_UILayerEnum.Normal);
            Register<MainTopPanel>("Main/MainTopPanel", E_UILayerEnum.Normal);
            Register<MenuPanel>("Main/MenuPanel", E_UILayerEnum.Normal);
            // ===== 通用提示（置顶层，避免被面板遮挡）=====
            Register<TipPanel>("Main/TipPanel", E_UILayerEnum.Top);
            // ===== 关卡 =====
            Register<LevelPanel>("GamePlay/LevelPanel", E_UILayerEnum.Normal);
            Register<ShopPanel>("GamePlay/ShopPanel", E_UILayerEnum.Normal);
            Register<ShowCardsPanel>("GamePlay/ShowCardsPanel", E_UILayerEnum.Normal);
            // ===== 控制台（调试工具，置顶层避免被遮挡）=====
            Register<ConsoleEntryPanel>("Console/ConsoleEntryPanel", E_UILayerEnum.Top);
            Register<ConsolePanel>("Console/ConsolePanel", E_UILayerEnum.Top);
        }

        /// <summary>
        /// 注册一个 UI 面板
        /// </summary>
        /// <typeparam name="T">面板类型（继承 BasePanel）</typeparam>
        /// <param name="prefabPath">预制体子路径（基础路径 Prefab/UI/ 会自动拼接）</param>
        /// <param name="layer">所属层级</param>
        public static void Register<T>(string prefabPath, E_UILayerEnum layer) where T : BasePanel
        {
            _registry[typeof(T)] = new UIPanelInfo
            {
                PanelType = typeof(T),
                PrefabPath = prefabPath,
                Layer = layer,
            };
        }

        /// <summary>
        /// 查询注册信息
        /// </summary>
        public static bool TryGet<T>(out UIPanelInfo info) where T : BasePanel
        {
            return _registry.TryGetValue(typeof(T), out info);
        }

        /// <summary>
        /// 查询注册信息（Type 版本）
        /// </summary>
        public static bool TryGet(Type type, out UIPanelInfo info)
        {
            return _registry.TryGetValue(type, out info);
        }
    }
}
