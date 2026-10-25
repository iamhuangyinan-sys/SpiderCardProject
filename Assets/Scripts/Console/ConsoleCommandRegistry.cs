/// <summary>
/// 控制台命令注册表 —— 所有命令集中在此注册（由 ConsoleCommandManager 初始化时调用）
///
/// 新增命令：在 RegisterAll 中加一行 Register 即可，例如
///   cmd.Register("card add *", CmdCardAdd, "向场上添加一张牌（* = 牌 id）");
/// 回调签名：string Handler(string[] args)，args 是命令行按空格切开的完整分段
/// </summary>
public static class ConsoleCommandRegistry
{
    public static void RegisterAll()
    {
        var cmd = ConsoleCommandManager.Instance;

        // ===== 关卡 =====

        // level skip —— 直接完成当前关卡（走正常收牌结算流程）
        cmd.Register("skip", CmdLevelSkip, "直接完成当前关卡");

        // add <牌定义> —— 往发牌堆随机位置洗入一张牌
        //   写法：花色_点数[_技能[_属性]]（用 _ 隔开，第一个下划线必写，后面可省）
        //   花色 0红心 1方块 2黑桃 3梅花；点数 1-9 A-9、0=10、j/q/k、s=-2、m=-1
        //   技能 10001 回收 / 10002 复制；属性 10001 沉底 / 10002 毒（多个用分号）
        //   例：add 0123_123 / add _m_10002 / add 0123_s_10002_10002
        cmd.Register("add *", CmdAddCard, "往发牌堆洗入一张牌（* = 花色_点数[_技能[_属性]]，如 0123_123）");

        // ===== 示例（后续按需开启）=====
        // cmd.Register("card add *", CmdCardAdd, "向场上添加一张牌（* = 牌 id）");
        // cmd.Register("help", args => string.Join("\n", ConsoleCommandManager.Instance.GetHelp()), "查看所有命令");
    }

    /// <summary>level skip —— 直接完成当前关卡</summary>
    private static string CmdLevelSkip(string[] args)
    {
        if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";
        if (!CardsManager.Instance.IsPlaying) return "执行失败：当前没有进行中的对局";
        if (CardsManager.Instance.IsSettling) return "执行失败：正在结算收牌";

        CardGameModule.Instance.DebugCompleteLevel();
        return "已跳过当前关卡";
    }

    /// <summary>add &lt;牌定义&gt; —— 往发牌堆随机位置洗入一张牌</summary>
    private static string CmdAddCard(string[] args)
    {
        if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";
        if (!CardsManager.Instance.IsPlaying) return "执行失败：当前没有进行中的对局";
        if (CardsManager.Instance.IsSettling) return "执行失败：正在结算收牌";

        string text = args.Length > 1 ? args[1] : string.Empty;

        if (!CardDefCodec.TryParse(text, out var def, out string error))
        {
            return $"添加失败：{error}（{text}）";
        }

        if (!CardsManager.Instance.WashInCard(def))
        {
            return $"添加失败：牌定义不合法（{text}）";
        }

        return $"已洗入发牌堆：{CardDefCodec.ToText(def)}";
    }
}
