/// <summary>
/// 卡牌相关枚举集中定义文件
/// 后续新增的卡牌枚举统一加在本文件内
/// </summary>

/// <summary>花色枚举</summary>
public enum E_CardSuitEnum
{
    /// <summary>红心</summary>
    Hearts = 0,

    /// <summary>梅花</summary>
    Clubs = 1,

    /// <summary>黑桃</summary>
    Spades = 2,

    /// <summary>方块</summary>
    Diamonds = 3,

    /// <summary>万能花色（配表填 -1）：可以当作任意花色</summary>
    Wild = -1,
}

/// <summary>落牌技能枚举 —— 牌放下后触发的特殊能力（配表 PlaceSkill）</summary>
public enum E_PlaceSkillEnum
{
    /// <summary>无</summary>
    None = 0,

    /// <summary>回收：放下后，自己和自己下面紧邻的 1 张牌一起进弃牌堆</summary>
    Recycle = 1,

    /// <summary>复制：放下后，自己变成这列自己下面紧邻的那张牌</summary>
    Copy = 2,
}
