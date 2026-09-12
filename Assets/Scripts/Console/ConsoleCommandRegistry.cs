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

        // add <牌 id> —— 往发牌堆随机位置洗入一张牌
        cmd.Register("add *", CmdAddCard, "往发牌堆洗入一张牌（* = 牌 id）");

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

    /// <summary>add <牌 id> —— 往发牌堆随机位置洗入一张牌</summary>
    private static string CmdAddCard(string[] args)
    {
        if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";
        if (!CardsManager.Instance.IsPlaying) return "执行失败：当前没有进行中的对局";
        if (CardsManager.Instance.IsSettling) return "执行失败：正在结算收牌";

        string cardId = args.Length > 1 ? args[1] : string.Empty;
        if (!CardsManager.Instance.WashInCard(cardId)) return $"添加失败：配表里没有这张牌（{cardId}）";

        return $"已洗入发牌堆：{cardId}";
    }
}
