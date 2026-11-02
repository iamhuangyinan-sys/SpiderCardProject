using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 多语言助手 —— 代码里只出现配表 Id，文案统一从 LanguageConfig 表取
///
/// 用法：
///   TipHelper.Show(LanguageHelper.Get("tip_draw_empty"));
///   sb.Append(LanguageHelper.Get("ui_level_tip_columns", cfg.ColumnNum));
///
/// 配表：Config/Excel/LanguageConfig.xlsx → 导出到 Resources/Config/LanguageConfig.json
///       列：Id | (空) | Chinese | English
///
/// 语言暂时写死中文（Current = Chinese），要切语言只改 Current 或调用 SetLanguage。
/// 表里查不到的 Id 会返回 Id 本身并打一次警告，方便发现漏配。
/// </summary>
public static class LanguageHelper
{
    /// <summary>当前语言（暂时固定中文）</summary>
    public static E_LanguageEnum Current = E_LanguageEnum.English;

    private static Dictionary<string, LanguageConfig> _table;
    private static readonly HashSet<string> _missingLogged = new();

    /// <summary>切换语言（表里加列后在这里补映射即可）</summary>
    public static void SetLanguage(E_LanguageEnum language)
    {
        Current = language;
    }

    /// <summary>取文案（不含格式化参数）</summary>
    public static string Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;

        var cfg = Find(id);
        if (cfg == null)
        {
            WarnMissing(id);
            return id;
        }

        string text = PickText(cfg);
        if (string.IsNullOrEmpty(text))
        {
            WarnMissing(id);
            return id;
        }

        return text;
    }

    /// <summary>取文案并按 string.Format 填充占位符（如 "场上列数 {0}"）</summary>
    public static string Get(string id, params object[] args)
    {
        string text = Get(id);
        if (args == null || args.Length == 0) return text;

        try
        {
            return string.Format(text, args);
        }
        catch (System.FormatException)
        {
            Debug.LogError($"[LanguageHelper] 文案 \"{id}\" 的占位符和参数数量不匹配: {text}");
            return text;
        }
    }

    /// <summary>表是否已成功读到（编辑器里改表后可 Reload）</summary>
    public static bool IsLoaded => _table != null && _table.Count > 0;

    /// <summary>重新读取配表（热更 / 改表后调用）</summary>
    public static void Reload()
    {
        _table = null;
        _missingLogged.Clear();
    }

    private static string PickText(LanguageConfig cfg)
    {
        switch (Current)
        {
            case E_LanguageEnum.English: return cfg.English;
            default: return cfg.Chinese;
        }
    }

    private static LanguageConfig Find(string id)
    {
        if (_table == null) Build();

        _table.TryGetValue(id, out var result);
        return result;
    }

    private static void Build()
    {
        _table = new Dictionary<string, LanguageConfig>();

        foreach (var cfg in ConfigHelper.GetAll<LanguageConfig>())
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
            _table[cfg.Id] = cfg;
        }
    }

    private static void WarnMissing(string id)
    {
        if (!_missingLogged.Add(id)) return;
        Debug.LogWarning($"[LanguageHelper] LanguageConfig 表里没有 Id: {id}");
    }
}
