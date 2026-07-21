using Framework.Save;

/// <summary>
/// 存档注册中心 —— 集中注册所有 E_SaveBasicEnum / E_SaveCustomEnum 的路径映射
///
/// 使用方法：
///   1. 先在 SaveEnums.cs 中添加你的 Key
///   2. 在此文件的 RegisterAll() 中添加对应的注册代码
///   3. FrameworkEntry 初始化时会自动调用
///
/// 注意事项：
///   - 基础类型：RegisterBasic(枚举Key, 默认值) → 所有基础 Key 共用一个 basic.json
///   - 自定义类型：RegisterCustom(枚举Key, 文件名) → 每个 Key 一个独立 JSON
///
/// 改名处理（避免数据丢失）：
///   - 基础枚举改名 → 加一句 MigrateBasicKey("旧名", 新Key)
///   - 自定义文件名改了 → 加一句 MigrateCustomPath("旧文件名", 新Key)
///   迁移只执行一次（旧数据存在时才搬），发布后可保留或删除
/// </summary>
public static class SaveRegistry
{
    /// <summary>
    /// 注册所有存档 Key（由 FrameworkEntry.InitFramework 调用）
    /// </summary>
    public static void RegisterAll()
    {
        var sm = SaveManager.Instance;

        // ================================================================
        //  基础类型注册：RegisterBasic(枚举Key, 默认值)
        //  默认值的类型决定了该 Key 存储的数据类型
        //  ⚠️ 枚举名会被存入 basic.json，需要改名时参见下方的迁移示例
        // ================================================================

        // sm.RegisterBasic(E_SaveBasicEnum.PlayerName, "无名");       // string
        // sm.RegisterBasic(E_SaveBasicEnum.HighScore, 0);            // int
        // sm.RegisterBasic(E_SaveBasicEnum.Volume, 1.0f);            // float
        // sm.RegisterBasic(E_SaveBasicEnum.IsFirstLaunch, true);     // bool

        // ================================================================
        //  自定义类型注册：RegisterCustom&lt;T&gt;(枚举Key, 文件名)
        //  文件名不含扩展名，最终路径为 /Saves/{fileName}.json
        //  ✅ 枚举名可随意改，只要注册时的文件名不变
        // ================================================================

        // sm.RegisterCustom(E_SaveCustomEnum.PlayerData, "player_data");
        // sm.RegisterCustom(E_SaveCustomEnum.GameSettings, "game_settings");
        // sm.RegisterCustom(E_SaveCustomEnum.CardCollection, "card_collection");

        // ================================================================
        //  数据迁移（改名后使用，保留一段时间后可删除）
        // ================================================================

        // --- 基础类型迁移示例 ---
        // 假设把 E_SaveBasicEnum.HighScore 改名为 E_SaveBasicEnum.BestScore：
        //   1. 在 SaveEnums.cs 中改枚举名 HighScore → BestScore
        //   2. 把上面的 RegisterBasic 改成新名
        //   3. 在下面加迁移，下次启动时自动把旧数据搬过来：
        // sm.MigrateBasicKey("HighScore", E_SaveBasicEnum.BestScore);

        // --- 自定义类型迁移示例 ---
        // 假设把文件名 "player_data" 改为 "hero_data"：
        //   1. 把上面的 RegisterCustom 文件名改成 "hero_data"
        //   2. 在下面加迁移，下次启动时自动重命名旧文件：
        // sm.MigrateCustomPath("player_data", E_SaveCustomEnum.PlayerData);
    }
}
