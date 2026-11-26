#if UNITY_EDITOR || SPIDERCARD_TEST_BUILD
using Framework;
#if UNITY_EDITOR
using System.Globalization;
using UnityEngine;
#endif

/// <summary>控制台加牌表单的目标位置。</summary>
public enum E_ConsoleCardTarget
{
    DrawPile,
    Deck,
    DiscardPile,
    Pocket,
}

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
#if UNITY_EDITOR
        cmd.Register("goto *", CmdLevelGoto, "跳到指定大关和层的选关界面（如 goto 2-4）");
        cmd.Register("goto", CmdLevelGoto, "查看跳关用法");
#endif

        // ===== 资源 =====

        // coin <数量> —— 增加金币（调试用，商店里也能加）
        cmd.Register("coin *", CmdAddCoin, "增加金币（* = 数量，如 coin 60）");

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

#if UNITY_EDITOR
    private static string CmdLevelGoto(string[] args)
    {
        string destination = args.Length > 1 ? args[1] : string.Empty;
        var parts = destination.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int chapter)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int layer))
        {
            const string message = "执行失败：用法为 goto <大关>-<层>（如 goto 2-4）";
            Debug.LogError(message);
            return message;
        }

        if (!CardGameModule.Instance.TryJumpToLevelSelection(chapter, layer, out string error))
        {
            string message = $"执行失败：{error}";
            Debug.LogError(message);
            return message;
        }

        return $"已保存 {chapter}-{layer} 的选关进度，正在重载卡牌场景";
    }
#endif

    /// <summary>加牌表单入口：直接接收结构化牌定义。</summary>
    public static string AddCard(CardDef def, E_ConsoleCardTarget target)
    {
        if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";
        if (target != E_ConsoleCardTarget.Deck && !CardsManager.Instance.IsPlaying) return "执行失败：当前没有进行中的对局";
        if (CardsManager.Instance.IsSettling) return "执行失败：正在结算收牌";
        if (!def.IsValid) return "添加失败：牌定义不合法";

        if (target == E_ConsoleCardTarget.Deck)
        {
            RunManager.Instance.AddCardToDeck(def);
            CardGameModule.Instance.SaveRun();
            return "已加入牌组";
        }

        switch (target)
        {
            case E_ConsoleCardTarget.DrawPile:
                return CardsManager.Instance.WashInCard(def) ? "已洗入发牌堆" : "添加失败：牌定义不合法";
            case E_ConsoleCardTarget.DiscardPile:
                MonoManager.Instance.StartCoroutine(CardsManager.Instance.ShowAndAddToDiscard(def, rollSunk: false));
                return "已加入弃牌堆";
            case E_ConsoleCardTarget.Pocket:
                return CardsManager.Instance.DebugAddCardToPocket(def, out string error) ? "已加入牌包" : $"添加失败：{error}";
            default:
                return "添加失败：未知目标位置";
        }
    }

    /// <summary>coin &lt;数量&gt; —— 增加金币</summary>
    private static string CmdAddCoin(string[] args)
    {
        // 金币是整局资源：选关 / 商店里也要能加，所以只查场景，不查是否在打牌
        if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";

        string text = args.Length > 1 ? args[1] : string.Empty;

        if (!int.TryParse(text, out int amount) || amount <= 0)
        {
            return $"执行失败：金币数量必须是正整数（{text}）";
        }

        RunManager.Instance.AddCoin(amount);
        return $"已增加金币：{amount}（当前 {RunManager.Instance.Coin}）";
    }
}
#endif
