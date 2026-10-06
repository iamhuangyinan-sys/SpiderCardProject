using System.Collections.Generic;

/// <summary>
/// 一大局游戏的存档数据 —— 整局的金币、牌组、关卡进度与商店状态
///
/// 只记录「选择过的关卡顺序（走过的路线）」与「进行中的关卡 id」：
///   进行中的那一关只存关卡 id，牌局本身不存，重进后从头开始这一关。
///   通关信息不单独存 —— 由「选择顺序 + 是否还在关卡中」推导（见 LevelStore.LastClearedLevelId）
/// </summary>
[System.Serializable]
public class RunSaveData
{
    /// <summary>本局金币</summary>
    public int coin;

    /// <summary>本局牌组（牌 id 列表，同一 id 出现多次即多份）</summary>
    public List<string> deck = new();

    /// <summary>
    /// 玩家选择过的关卡 id（按选择顺序追加，含正在打的那一关）：
    ///   相邻两项 = 玩家实际走过的路线（选关界面 / 地图据此把连线置白）
    ///   最后一项 = 当前正在打的关；已通关则 = 最后通关的关
    /// </summary>
    public List<string> chosenLevelIds = new();

    /// <summary>进行中的关卡 id（空 = 停在选关界面）—— 重进后直接重开这一关</summary>
    public string selectedLevelId;

    /// <summary>进行中的是商店关时的商品（含 sold，防止退出重进刷商品）</summary>
    public List<ShopGoods> shopGoods = new();

    /// <summary>进行中的商店关已用的删牌次数（退出重进要保持一样，不能洗回来）</summary>
    public int shopDeleteCount;
}
