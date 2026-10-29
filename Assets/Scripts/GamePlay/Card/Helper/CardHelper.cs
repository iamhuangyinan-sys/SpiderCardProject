using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 纸牌通用工具 —— 随机生成之类零散的牌工具放这里（牌面 / tip / 动画各有自己的 Helper，别混）
/// </summary>
public static class CardHelper
{
    /// <summary>点数的随机范围：A ~ K（1-13）</summary>
    private const int MinRank = 1;
    private const int MaxRank = 13;

    private static readonly System.Random _random = new System.Random();

    /// <summary>
    /// 随机一张牌：花色在 <paramref name="suits"/> 里随机、点数 A-K 随机。
    /// 每次独立随机，不保证连续调用之间不重复
    /// </summary>
    /// <param name="suits">可选花色列表（0-3）；null 或空 = 四花色随机</param>
    public static CardDef RandomDef(IReadOnlyList<int> suits = null)
    {
        return CardDef.Create(PickSuit(suits), _random.Next(MinRank, MaxRank + 1));
    }

    /// <summary>花色：给了列表就在列表里随机，没给就在 0-3 里随机</summary>
    private static int PickSuit(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0)
        {
            return _random.Next((int)E_CardSuitEnum.Hearts, (int)E_CardSuitEnum.Clubs + 1);
        }

        return suits[_random.Next(suits.Count)];
    }
}
