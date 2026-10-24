using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一张牌正面「显示什么」的计算 —— 3D 牌（CardView / SpriteRenderer）和
/// 2D 牌（CardModule / uGUI Image）共用同一套规则，两边只负责把结果画出来。
///
/// 规则：
///   图案   ：特殊牌读配表 Image，普通牌按「主花色 + 主点数」自动推导
///   主花色 ：花色列表第一个（排序最前），其余进图标行
///   主点数 ：点数列表第一个「可显示」的点数（跳过 -1），其余进图标行；红黑跟主花色
///
/// 列对齐（多重牌压在别的牌上时切换主值）不在这里做 —— 那是 CardViewManager 的事，
/// 对齐后的显示用列表照样传进来算就行。
/// </summary>
public class CardFace
{
    /// <summary>图案（推不出来 / 素材不存在 = null）</summary>
    public Sprite Face;

    /// <summary>主花色（没有花色 = null）</summary>
    public Sprite MainSuit;

    /// <summary>其余花色 → 图标行（素材不存在的已跳过）</summary>
    public readonly List<Sprite> ExtraSuits = new();

    /// <summary>主点数（没有可显示的点数 = null）</summary>
    public Sprite MainRank;

    /// <summary>其余点数 → 图标行（素材不存在的已跳过）</summary>
    public readonly List<Sprite> ExtraRanks = new();

    /// <summary>
    /// 数据上是不是「多重花色」（花色列表超过 1 个）—— 跟素材有没有无关。
    /// 用来决定图案要不要下沉给花色图标行让位
    /// </summary>
    public bool HasMultiSuit;
}

public static class CardFaceHelper
{
    /// <summary>按牌 id 算（花色点数从 id 解析）</summary>
    public static CardFace Build(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return new CardFace();

        CardIdCodec.TryParse(cardId, out var suits, out var ranks);
        return Build(cardId, suits, ranks);
    }

    /// <summary>按显式花色 / 点数列表算（列对齐后的显示用列表也走这里）</summary>
    public static CardFace Build(string cardId, IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var face = new CardFace();
        var res = CardResManager.Instance;

        face.Face = res.GetFaceSprite(cardId, suits, ranks);

        // 花色：第一个当主花色，其余进图标行
        int suitCount = suits?.Count ?? 0;
        if (suitCount > 0)
        {
            face.MainSuit = res.GetSuitSprite(suits[0]);

            for (int i = 1; i < suitCount; i++)
            {
                var sprite = res.GetSuitSprite(suits[i]);
                if (sprite != null) face.ExtraSuits.Add(sprite);   // 素材缺失就当没这个花色
            }
        }

        face.HasMultiSuit = suitCount > 1;

        // 点数：跳过 -1（它不是点数），第一个当主点数，其余进图标行；红黑跟主花色
        bool isRed = IsRedSuit(suits);
        bool hasMainRank = false;

        int rankCount = ranks?.Count ?? 0;
        for (int i = 0; i < rankCount; i++)
        {
            int rank = ranks[i];
            if (rank == CardRankConst.Wild) continue;

            var sprite = res.GetRankSprite(rank, isRed);
            if (sprite == null) continue;   // 素材缺失就当没这个点数

            if (!hasMainRank)
            {
                face.MainRank = sprite;
                hasMainRank = true;
            }
            else
            {
                face.ExtraRanks.Add(sprite);
            }
        }

        return face;
    }

    /// <summary>点数用红套还是黑套：红心 / 方块是红，其余（含没有花色）是黑</summary>
    private static bool IsRedSuit(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0) return false;

        int suit = suits[0];
        return suit == (int)E_CardSuitEnum.Hearts || suit == (int)E_CardSuitEnum.Diamonds;
    }
}
