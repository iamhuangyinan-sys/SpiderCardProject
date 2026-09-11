using System.Collections.Generic;
using Framework.Mgr;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 关卡资源管理器 —— 读 LevelTypeConfig 记录图标路径并预热，按类型 id 提供 Icon
/// 图标缓存/引用计数由 ResManager 负责，这里只维护「类型 id → 资源路径」的配表映射
///
/// 使用方式：
///   var sprite = LevelResManager.Instance.GetIcon(E_LevelTypeEnum.Shop);
/// </summary>
public class LevelResManager : ManagerBase<LevelResManager>
{
    /// <summary>关卡图标资源根目录（相对 Resources）</summary>
    public const string IconRoot = "GamePlay/Level/Icon/";

    /// <summary>类型 id → 完整资源路径</summary>
    private readonly Dictionary<int, string> _iconPaths = new();

    protected override void OnInit()
    {
        PreloadIcons();
    }

    protected override void OnDispose()
    {
        // 图标为关卡常驻资源，统一交给 ResManager.ClearAll / UnloadUnusedAssets 释放
        _iconPaths.Clear();
    }

    /// <summary>读 LevelTypeConfig → 异步预加载全部关卡类型图标</summary>
    private void PreloadIcons()
    {
        foreach (var cfg in ConfigHelper.GetAll<LevelTypeConfig>())
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.IconImage)) continue;
            if (!int.TryParse(cfg.Id, out int type)) continue;

            string path = IconRoot + cfg.IconImage;
            _iconPaths[type] = path;

            ResManager.Instance.Load<Sprite>(path);  // 预热，之后 GetIcon 直接命中缓存
        }
    }

    /// <summary>取类型图标（走 ResManager 缓存，加载中会自动转同步）</summary>
    public Sprite GetIcon(int levelType) =>
        _iconPaths.TryGetValue(levelType, out var path) ? ResManager.Instance.Load<Sprite>(path) : null;

    /// <summary>取类型图标（枚举版本）</summary>
    public Sprite GetIcon(E_LevelTypeEnum levelType) => GetIcon((int)levelType);
}
