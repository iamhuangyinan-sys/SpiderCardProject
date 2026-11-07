using System;
using System.IO;

/// <summary>版本目录命名规则；预览和实际构建共用。</summary>
public static class ProjectBuildPaths
{
    public static string NormalizeVersion(string version)
    {
        string value = version?.Trim();
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("请输入版本号，例如 0.3.0。");

        if (value == "." || value == ".." || value.EndsWith(".") ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
            throw new ArgumentException("版本号不能包含路径、非法文件名字符，也不能以点结尾。");

        string stem = value.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
             stem[3] >= '1' && stem[3] <= '9'))
            throw new ArgumentException("版本号不能使用 Windows 保留的设备名称。");

        return value;
    }

    public static string GetAvailableDirectory(string outputRoot, string version)
    {
        string value = NormalizeVersion(version);
        string root = Path.GetFullPath(outputRoot);
        string candidate = Path.Combine(root, value);
        int suffix = 0;
        while (Directory.Exists(candidate) || File.Exists(candidate))
            candidate = Path.Combine(root, $"{value}.{checked(++suffix)}");

        return candidate;
    }
}
