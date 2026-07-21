namespace Framework.Save
{
    /// <summary>
    /// 基础类型存档 Key 枚举 —— 所有基础类型（int/string/float/bool）共用一个存档文件
    /// 使用时在代码中通过 RegisterBasic 注册 key 和默认值
    ///
    /// 示例：
    ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.HighScore, 0);
    ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.PlayerName, "无名");
    /// </summary>
    public enum E_SaveBasicEnum
    {
        // ---- 示例 key，按需增删 ----
        // PlayerName,     // string
        // HighScore,      // int
        // Volume,         // float
        // IsFirstLaunch,  // bool
    }

    /// <summary>
    /// 自定义类型存档 Key 枚举 —— 每个 key 对应一个独立的 JSON 文件
    /// 使用时在代码中通过 RegisterCustom 注册 key 和文件名
    ///
    /// 示例：
    ///   SaveManager.Instance.RegisterCustom<PlayerData>(E_SaveCustomEnum.PlayerData, "player_data");
    /// </summary>
    public enum E_SaveCustomEnum
    {
        // ---- 示例 key，按需增删 ----
        // PlayerData,
        // GameSettings,
        // CardCollection,
    }
}
