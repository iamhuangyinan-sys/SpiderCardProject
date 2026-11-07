using System;
using UnityEngine;

/// <summary>随游戏一起发布的版本信息，由项目打包工具写入。</summary>
[Serializable]
public sealed class BuildInfoData
{
    public string version;
}

/// <summary>打包信息的统一读取入口，不缓存版本，兼容编辑器关闭 Domain Reload 的情况。</summary>
public static class BuildInfoHelper
{
    public const string ResourcePath = "BuildInfo";
    public const string AssetPath = "Assets/Resources/BuildInfo.json";

    public static BuildInfoData Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("打包信息 JSON 为空。");

        var info = JsonUtility.FromJson<BuildInfoData>(json);
        if (info == null || string.IsNullOrWhiteSpace(info.version))
            throw new FormatException("打包信息 JSON 缺少有效的 version 字段。");

        return info;
    }

    public static string GetVersion()
    {
        try
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
                throw new InvalidOperationException($"找不到 {AssetPath}。");

            return Parse(asset.text).version;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BuildInfo] 读取版本失败：{exception.Message}");
            return "未知版本";
        }
    }
}
