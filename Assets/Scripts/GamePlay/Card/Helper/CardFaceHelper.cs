using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一张牌正面「显示什么」的计算 —— 3D 牌（CardView / SpriteRenderer）和
/// 2D 牌（CardModule / uGUI Image）共用同一套规则，两边只负责把结果画出来。
///
/// 规则：
///   图案   ：优先显示主花色和主点数对应的图案，缺图时尝试 PlaceSkillConfig.Image
///   花色   ：花色列表第一个（排序最前），其余进图标行；没有花色 → Suit/none 占位
///   点数   ：点数列表第一个「可显示」的点数（跳过 -1），其余进图标行；按主花色着色；
///            一个可显示点数都没有 → 有 -1 画 Rank/M，否则画 Rank/none
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

    /// <summary>主点数和额外点数的颜色，白色保留素材原色</summary>
    public Color RankColor = Color.white;

    /// <summary>
    /// 数据上是不是「多重花色」（花色列表超过 1 个）—— 跟素材有没有无关。
    /// 用来决定图案要不要下沉给花色图标行让位
    /// </summary>
    public bool HasMultiSuit;
}

public static class CardFaceHelper
{
    private static readonly Color SpadesRankColor = new Color32(28, 28, 28, 255);
    private static readonly Color HeartsRankColor = new Color32(201, 63, 63, 255);
    private static readonly Color ClubsRankColor = new Color32(12, 43, 95, 255);
    private static readonly Color DiamondsRankColor = new Color32(191, 77, 42, 255);

    /// <summary>该属性是否在牌桌和局内牌堆中显示</summary>
    public static bool IsAttributeVisibleInGame(E_CardAttributeEnum attribute)
    {
        return ConfigHelper.Get<CardAttributeConfig>(((int)attribute).ToString())?.IsShowInGame == true;
    }

    /// <summary>填充属性栏图标，落牌技能排在属性前面。</summary>
    public static void FillAttributeSprites(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes,
        List<Sprite> sprites, bool useInGameAttributes, int maxIcons = int.MaxValue)
    {
        sprites.Clear();

        var res = CardResManager.Instance;
        var skillIcon = def.IsValid ? res.GetPlaceSkillIcon(def.placeSkill) : null;
        if (skillIcon != null) sprites.Add(skillIcon);

        var flipIcon = def.IsValid ? res.GetFlipSkillIcon(def.flipSkill) : null;
        if (flipIcon != null && sprites.Count < maxIcons) sprites.Add(flipIcon);

        if (attributes == null) return;

        for (int i = 0; i < attributes.Count && sprites.Count < maxIcons; i++)
        {
            var attribute = attributes[i];
            if (useInGameAttributes && !IsAttributeVisibleInGame(attribute)) continue;

            var sprite = res.GetAttributeSprite((int)attribute);
            if (sprite != null) sprites.Add(sprite);
        }
    }

    /// <summary>按牌定义算（花色 / 点数取定义里的）</summary>
    public static CardFace Build(CardDef def)
    {
        if (!def.IsValid) return new CardFace();

        return Build(def, def.suits, def.ranks);
    }

    /// <summary>按显式花色 / 点数列表算（列对齐后的显示用列表也走这里）</summary>
    public static CardFace Build(CardDef def, IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var face = new CardFace();
        var res = CardResManager.Instance;

        face.Face = res.GetFaceSprite(def, suits, ranks);

        int suitCount = suits?.Count ?? 0;
        int rankCount = ranks?.Count ?? 0;

        // 花色：第一个当主花色，其余进图标行
        if (suitCount > 0)
        {
            face.MainSuit = res.GetSuitSprite(suits[0]);

            for (int i = 1; i < suitCount; i++)
            {
                var sprite = res.GetSuitSprite(suits[i]);
                if (sprite != null) face.ExtraSuits.Add(sprite);   // 素材缺失就当没这个花色
            }
        }
        else if (rankCount > 0)
        {
            // 无花色 → 画 Suit/none 占位。
            // 但「无花色 + 无点数」的牌花色位留空：只在点数位画一个 none，避免两个 none 并排
            face.MainSuit = res.GetNoSuitSprite();
        }

        face.HasMultiSuit = suitCount > 1;

        // 点数：跳过 -1，第一个当主点数，其余进图标行
        bool hasMainRank = false;

        for (int i = 0; i < rankCount; i++)
        {
            int rank = ranks[i];
            if (rank == CardRankConst.Wild) continue;

            var sprite = res.GetRankSprite(rank);
            if (sprite == null) continue;   // 素材缺失就当没这个点数

            if (!hasMainRank)
            {
                face.MainRank = sprite;
                face.RankColor = GetRankColor(suits);
                hasMainRank = true;
            }
            else
            {
                face.ExtraRanks.Add(sprite);
            }
        }

        // 一个可显示的点数都没有：牌面画「万能 M」或「无点数 none」占位
        if (!hasMainRank)
        {
            face.MainRank = HasRank(ranks, CardRankConst.Wild)
                ? res.GetWildRankSprite()
                : res.GetNoRankSprite();
        }

        return face;
    }

    /// <summary>点数列表里有没有这个值</summary>
    private static bool HasRank(IReadOnlyList<int> ranks, int value)
    {
        if (ranks == null) return false;

        for (int i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] == value) return true;
        }

        return false;
    }

    /// <summary>取主花色的点数颜色。无花色时保留白图原色。</summary>
    public static Color GetRankColor(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0) return Color.white;

        return (E_CardSuitEnum)suits[0] switch
        {
            E_CardSuitEnum.Spades => SpadesRankColor,
            E_CardSuitEnum.Hearts => HeartsRankColor,
            E_CardSuitEnum.Clubs => ClubsRankColor,
            E_CardSuitEnum.Diamonds => DiamondsRankColor,
            _ => Color.white
        };
    }
}
