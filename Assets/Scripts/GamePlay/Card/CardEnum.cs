/// <summary>
/// 卡牌相关枚举集中定义文件
/// 后续新增的卡牌枚举统一加在本文件内
/// </summary>

/// <summary>
/// 花色枚举 —— 数值同时就是牌定义里花色的大小顺序（0 红心 / 1 方块 / 2 黑桃 / 3 梅花），
/// 也是「多重花色时第一个当主花色」的优先顺序。
/// 改数值等于改顺序与图案推导，别随便动
/// </summary>
public enum E_CardSuitEnum
{
    /// <summary>红心</summary>
    Hearts = 0,

    /// <summary>方块</summary>
    Diamonds = 1,

    /// <summary>黑桃</summary>
    Spades = 2,

    /// <summary>梅花</summary>
    Clubs = 3,
}

/// <summary>
/// 特殊点数常量（配表 Rank 列表里的负值，与 1-13 的普通点数共存）
/// 不是枚举，只是与花色枚举放一起方便查
/// </summary>
public static class CardRankConst
{
    /// <summary>万能放下：移动到任意牌上（不看点数也不看花色）；连接时无视点数（仍要看花色）</summary>
    public const int Wild = -1;

    /// <summary>无视点数：连接 / 放下都不比点数，但要看花色（红心牌才能压到红心蜘蛛上）</summary>
    public const int Ignore = -2;

    /// <summary>
    /// 多重「点数」的数量上限（含主点数）—— 逻辑规定，不是表现层限制，**普通牌和特殊牌都一样**。
    /// 超过的牌定义不合法：CardDef 构造 / 控制台解析时会警告并拒绝
    /// </summary>
    public const int MaxCount = 5;
}

/// <summary>
/// 落牌技能枚举 —— 牌放下后触发的特殊能力。
/// 数值就是配表 PlaceSkillConfig 的主键（改这里要同步改表）：
/// 带技能的牌 = 以前的「特殊牌」，整图读 PlaceSkillConfig.Image
/// </summary>
public enum E_PlaceSkillEnum
{
    /// <summary>无</summary>
    None = 0,

    /// <summary>回收：放下后，自己和自己下面紧邻的 1 张牌一起进弃牌堆</summary>
    Recycle = 10001,

    /// <summary>复制：放下后，自己变成这列自己下面紧邻的那张牌</summary>
    Copy = 10002,
}

/// <summary>
/// 卡牌 tip 类型 —— Id 就是配表 CardTipConfig 的主键（改这里要同步改表）
/// 1xx = 点数相关，2xx = 花色相关；牌的状态类描述（沉底 / 毒）走 E_CardAttributeEnum + CardAttributeConfig
/// </summary>
public enum E_CardTipEnum
{
    // ===== 点数（1xx）=====

    /// <summary>多重点数：可用其中任意一个点数连接</summary>
    MultiRank = 101,

    /// <summary>点数含 -1：万能放下，可以放在任意牌上</summary>
    RankWild = 102,

    /// <summary>点数含 -2：蜘蛛，作为任意点数连接，但花色要相同</summary>
    RankIgnore = 103,

    /// <summary>没有点数：不能与任何牌连接，只能单独拖动</summary>
    NoRank = 104,

    // ===== 花色（2xx）=====

    /// <summary>多重花色：可用其中任意一种花色连接</summary>
    MultiSuit = 201,

    /// <summary>没有花色：跟谁都不是同花，不参与花色判断</summary>
    NoSuit = 202,
}

/// <summary>
/// 卡牌属性 —— 数值就是配表 CardAttributeConfig 的主键（改这里要同步改表）
/// 属性是「牌在牌桌上的状态」，不进 id 编码；一张牌可以同时有多个，列表为空 = 没有属性
/// </summary>
public enum E_CardAttributeEnum
{
    /// <summary>沉底：收入弃牌堆时按概率打上，下一次发出时以牌背插到该列堆底</summary>
    Sunk = 10001,

    /// <summary>毒：不能放入额外牌包</summary>
    Poison = 10002,
}

/// <summary>
/// 牌预设的类型 —— 数值就是配表 CardPreSetConfig.Type，用来分组抽牌（如商店按类型随机）
/// </summary>
public enum E_CardPreSetTypeEnum
{
    /// <summary>负面牌（不在商店售卖）</summary>
    Negative = 0,

    /// <summary>技能类（回收 / 复制等带落牌技能的牌）</summary>
    Skill = 1,

    /// <summary>蜘蛛类（无视点数，靠花色连接）</summary>
    Spider = 2,

    /// <summary>多重点数 / 花色类</summary>
    Multi = 3,
}

