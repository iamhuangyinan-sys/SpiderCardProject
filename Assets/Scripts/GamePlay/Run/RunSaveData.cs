using System.Collections.Generic;

/// <summary>
/// 一大局游戏的存档数据 —— 整局的金币、牌组、关卡进度与商店状态
///
/// 只记录「已完成关卡」与「进行中的关卡 id」：
///   进行中的那一关只存关卡 id，牌局本身不存，重进后从头开始这一关。
/// </summary>
[System.Serializable]
public class RunSaveData
{
    /// <summary>本局金币</summary>
    public int coin;

    /// <summary>本局牌组（牌 id 列表，同一 id 出现多次即多份）</summary>
    public List<string> deck = new();

    /// <summary>最后完成的关卡 id（空 = 还没通关过任何关卡）—— 用于重建解锁状态与当前层</summary>
    public string lastLevelId;

    /// <summary>进行中的关卡 id（空 = 停在选关界面）—— 重进后直接重开这一关</summary>
    public string selectedLevelId;

    /// <summary>进行中的是商店关时的商品（含 sold，防止退出重进刷商品）</summary>
    public List<ShopGoods> shopGoods = new();
}
