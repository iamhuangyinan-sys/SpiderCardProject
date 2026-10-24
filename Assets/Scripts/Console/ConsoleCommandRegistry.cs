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

        // add <编码 id> [属性] —— 往发牌堆随机位置洗入一张牌
        //   普通牌：N{花色}_{点数}            例 add N0_1（红心A）、add N02_0jk（红心+黑桃 的 10/J/K）
        //   特殊牌：S{花色}_{点数}_{配表id}    例 add S_m_0200001（复制）、add S__0300001（黑色）
        //   属性（可选）：E_CardAttributeEnum 的值，多个用分号隔开，例 add N02_789 10001;10002
        //   花色 0红心 1方块 2黑桃 3梅花；点数 1-9 A-9、0=10、j/q/k、s=-2、m=-1
        cmd.Register("add *", CmdAddCard, "往发牌堆洗入一张牌（* = 编码 id，如 N0_1 / S_m_0200001）");
        cmd.Register("add * *", CmdAddCard, "同上，第二段是属性（分号隔开，如 10001;10002）");

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

    /// <summary>add &lt;编码 id&gt; [属性] —— 往发牌堆随机位置洗入一张牌</summary>
    private static string CmdAddCard(string[] args)
    {
        if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";
        if (!CardsManager.Instance.IsPlaying) return "执行失败：当前没有进行中的对局";
        if (CardsManager.Instance.IsSettling) return "执行失败：正在结算收牌";

        string cardId = args.Length > 1 ? args[1] : string.Empty;
        string attributeText = args.Length > 2 ? args[2] : string.Empty;

        if (!CardAttributeHelper.TryParse(attributeText, out var attributes))
        {
            return $"添加失败：属性不合法（{attributeText}）—— 用分号隔开枚举值，如 10001;10002";
        }

        if (!CardsManager.Instance.WashInCard(cardId, attributes))
        {
            return $"添加失败：牌 id 不合法（{cardId}）—— 点数最多 {CardRankConst.MaxCount} 个（含主点数），原因见 Console";
        }

        return attributes.Count > 0
            ? $"已洗入发牌堆：{cardId}（属性 {attributeText}）"
            : $"已洗入发牌堆：{cardId}";
    }
}
