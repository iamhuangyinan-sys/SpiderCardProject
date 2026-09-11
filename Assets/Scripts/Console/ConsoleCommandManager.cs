using System;
using System.Collections.Generic;
using Framework.Mgr;

/// <summary>
/// 控制台命令管理器 —— 注册 / 匹配 / 执行命令行
///
/// 命令格式：空格分隔的若干「段」，每段支持 * 通配符（匹配任意字符）
///   Register("level skip", handler)     精确匹配 level skip
///   Register("card add *", handler)     匹配 card add &lt;任意&gt;，handler 收到完整分段
///   Register("set * *", handler)        匹配 set 后跟任意两段
///   Register("lv* skip", handler)       段内也可用 *（lv1 / lv2 都能命中）
///
/// 使用方式：
///   ConsoleCommandManager.Instance.Register("xxx yyy", args => "输出", "说明");
///   if (ConsoleCommandManager.Instance.Execute("level skip", out var output)) { ... }
///
/// 新增命令请统一写在 ConsoleCommandRegistry.RegisterAll 中
/// </summary>
public class ConsoleCommandManager : ManagerBase<ConsoleCommandManager>
{
    /// <summary>命令回调：入参为命令行的完整分段（含命令本身），返回输出文本</summary>
    public delegate string ConsoleCommandHandler(string[] args);

    private class CommandEntry
    {
        public string Pattern;
        public string[] Segments;
        public ConsoleCommandHandler Handler;
        public string Description;
    }

    private readonly List<CommandEntry> _commands = new();

    protected override void OnInit()
    {
        ConsoleCommandRegistry.RegisterAll();
    }

    protected override void OnDispose()
    {
        _commands.Clear();
    }

    // ==================== 注册 / 执行 ====================

    /// <summary>注册一条命令（重复注册同一 pattern 时以先注册的为准）</summary>
    public void Register(string pattern, ConsoleCommandHandler handler, string description = "")
    {
        if (string.IsNullOrWhiteSpace(pattern) || handler == null) return;

        _commands.Add(new CommandEntry
        {
            Pattern = pattern.Trim(),
            Segments = Split(pattern),
            Handler = handler,
            Description = description,
        });
    }

    /// <summary>执行一行命令（返回是否命中已注册的命令）</summary>
    public bool Execute(string input, out string output)
    {
        output = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var args = Split(input);
        if (args.Length == 0) return false;

        foreach (var cmd in _commands)
        {
            if (!IsMatch(cmd.Segments, args)) continue;

            output = cmd.Handler?.Invoke(args);
            return true;
        }

        return false;
    }

    /// <summary>所有已注册命令的说明（形如 "level skip - 直接完成当前关卡"）</summary>
    public List<string> GetHelp()
    {
        var list = new List<string>();
        foreach (var cmd in _commands)
        {
            list.Add(string.IsNullOrEmpty(cmd.Description)
                ? cmd.Pattern
                : $"{cmd.Pattern} - {cmd.Description}");
        }
        return list;
    }

    // ==================== 内部 ====================

    private static string[] Split(string text) =>
        text.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>段数一致且每段都匹配才算命中</summary>
    private static bool IsMatch(string[] pattern, string[] args)
    {
        if (pattern.Length != args.Length) return false;

        for (int i = 0; i < pattern.Length; i++)
        {
            if (!MatchSegment(pattern[i], args[i])) return false;
        }
        return true;
    }

    /// <summary>单段匹配：* 可匹配任意字符（大小写不敏感）</summary>
    private static bool MatchSegment(string pattern, string input)
    {
        if (pattern == "*") return true;
        if (pattern.IndexOf('*') < 0)
            return string.Equals(pattern, input, StringComparison.OrdinalIgnoreCase);

        var parts = pattern.Split('*');
        int pos = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0) continue;

            int idx = input.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            // 通配符在首段 → 必须从头匹配；在尾段 → 必须到结尾
            if (i == 0 && idx != 0) return false;
            if (i == parts.Length - 1 && idx + part.Length != input.Length) return false;

            pos = idx + part.Length;
        }

        return true;
    }
}
