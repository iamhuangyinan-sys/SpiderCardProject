/// <summary>
/// 关卡类型枚举
/// </summary>
public enum E_LevelTypeEnum
{
    /// <summary>普通关</summary>
    Normal = 0,

    /// <summary>商店关</summary>
    Shop = 1,

    /// <summary>BOSS 关</summary>
    Boss = 2,
}

/// <summary>关卡开局事件枚举（配表 LevelEvent）—— 开局先往发牌堆洗入牌，再发牌</summary>
public enum E_LevelEventEnum
{
    /// <summary>无事件</summary>
    None = 0,

    /// <summary>洗入 1 张万能蜘蛛（4 花色 + 无视点数）</summary>
    AddSpider = 1,

    /// <summary>洗入 2 张随机梅花 A-K + 1 张无花色无点数的牌（BOSS 关加难度）</summary>
    AddRandom = 2,
}
