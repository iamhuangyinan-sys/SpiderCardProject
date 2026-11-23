using System.Collections.Generic;
using Framework.Mgr;
using UnityEngine;

/// <summary>
/// 卡牌资源管理器 —— 加载牌底 / 图案 / 花色 / 点数等素材
/// 素材都放在 Resources/GamePlay/Card/ 下：
///   Bg/cardBack · Bg/cardEmpty         牌背 / 空牌底
///   Image/{配表 Image}                  图案
///   Suit/{hearts|clubs|spades|diamonds}  花色小图
///   Rank/{A..K|S}                       共用白色点数小图
///   Attribute/{配表 Icon}                属性小图
///   PlaceSkill/{配表 Icon}               落牌技能小图
/// </summary>
public class CardResManager : ManagerBase<CardResManager>
{
    private const string CARD_DIR = "GamePlay/Card/";
    private const string BG_DIR = CARD_DIR + "Bg/";
    private const string SUIT_DIR = CARD_DIR + "Suit/";
    private const string RANK_DIR = CARD_DIR + "Rank/";
    private const string ATTRIBUTE_DIR = CARD_DIR + "Attribute/";
    private const string PLACE_SKILL_DIR = CARD_DIR + "PlaceSkill/";

    /// <summary>sprite 缓存：完整资源路径 → sprite</summary>
    private readonly Dictionary<string, Sprite> _spriteCache = new();

    private Sprite _backSprite;
    private Sprite _emptySprite;

    protected override void OnInit()
    {
        Preload();
    }

    /// <summary>预加载两种牌底</summary>
    public void Preload()
    {
        _backSprite = LoadSprite(BG_DIR + "cardBack");
        _emptySprite = LoadSprite(BG_DIR + "cardEmpty");
    }

    /// <summary>牌底：true = 牌背，false = 正面用的空牌底</summary>
    public Sprite GetBgSprite(bool back) => back ? _backSprite : _emptySprite;

    /// <summary>获取整张牌图（imagePath 为相对 GamePlay/Card/Image/ 的文件名，不含扩展名）</summary>
    public Sprite GetCardImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;
        return LoadSprite(CARD_DIR + "Image/" + imagePath);
    }

    /// <summary>花色小图（Suit/{hearts|clubs|spades|diamonds}）</summary>
    public Sprite GetSuitSprite(int suit)
    {
        string name = GetSuitName(suit);
        return string.IsNullOrEmpty(name) ? null : LoadSprite(SUIT_DIR + name);
    }

    /// <summary>无花色的占位图（Suit/none）—— 没有花色的牌画这个</summary>
    public Sprite GetNoSuitSprite() => LoadSprite(SUIT_DIR + "none");

    /// <summary>共用白色点数小图（Rank/{A..K|S}）</summary>
    public Sprite GetRankSprite(int rank)
    {
        return LoadSprite(RANK_DIR + GetRankName(rank));
    }

    /// <summary>万能放下（-1）的点数图（Rank/M）</summary>
    public Sprite GetWildRankSprite() => LoadSprite(RANK_DIR + "M");

    /// <summary>无点数的占位图（Rank/none）</summary>
    public Sprite GetNoRankSprite() => LoadSprite(RANK_DIR + "none");

    /// <summary>属性小图（读配表 CardAttributeConfig.Icon → Attribute/ 下的文件名，不含扩展名）</summary>
    public Sprite GetAttributeSprite(int attribute)
    {
        var cfg = ConfigHelper.Get<CardAttributeConfig>(attribute.ToString());
        if (cfg == null)
        {
            Debug.LogWarning($"[CardResManager] 属性 {attribute} 缺少 CardAttributeConfig 配表行");
            return null;
        }

        return string.IsNullOrEmpty(cfg.Icon) ? null : LoadSprite(ATTRIBUTE_DIR + cfg.Icon);
    }

    public Sprite GetPlaceSkillIcon(E_PlaceSkillEnum skill)
    {
        if (skill == E_PlaceSkillEnum.None) return null;

        var cfg = ConfigHelper.Get<PlaceSkillConfig>(((int)skill).ToString());
        return string.IsNullOrEmpty(cfg?.Icon) ? null : LoadSprite(PLACE_SKILL_DIR + cfg.Icon);
    }

    public Sprite GetFlipSkillIcon(E_FlipSkillEnum skill)
    {
        var cfg = GetFlipSkillConfig(skill);
        return string.IsNullOrEmpty(cfg?.Icon) ? null : LoadSprite(CARD_DIR + "FlipSkill/" + cfg.Icon);
    }

    private static FlipSkillConfig GetFlipSkillConfig(E_FlipSkillEnum skill)
    {
        return skill == E_FlipSkillEnum.None ? null : ConfigHelper.Get<FlipSkillConfig>(((int)skill).ToString());
    }

    // ==================== 整张牌图案（自动推导） ====================

    /// <summary>
    /// 取一张牌正面的图案：
    ///   优先加载「主花色 + 主点数」对应的图案（cardHeartsA / cardHeartsSpider…）
    ///   没有对应图案时依次尝试 PlaceSkillConfig.Image 和 FlipSkillConfig.Image
    ///   两者都没有素材时返回 null
    ///
    /// 传进来的列表就是「显示用」列表：多重花色 / 多重点数也推得出来（取排在最前的那一个当主花色主点数），
    /// 被收敛过的牌传收敛后的列表 → 图案跟着一起换
    /// </summary>
    public Sprite GetFaceSprite(CardDef def, IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var face = GetCardImage(GetFaceImageName(suits, ranks));
        if (face != null) return face;

        var placeImage = GetCardImage(GetSkillImage(def));
        if (placeImage != null) return placeImage;

        return def.IsValid ? GetCardImage(GetFlipSkillConfig(def.flipSkill)?.Image) : null;
    }

    /// <summary>只给牌定义时取图案（列表 / 预览用）</summary>
    public Sprite GetFaceSprite(CardDef def)
    {
        return def.IsValid ? GetFaceSprite(def, def.suits, def.ranks) : null;
    }

    /// <summary>带落牌技能的牌 → PlaceSkillConfig.Image（没技能 / 没配图返回 null）</summary>
    private static string GetSkillImage(CardDef def)
    {
        if (!def.IsValid || !def.HasSkill) return null;

        var cfg = ConfigHelper.Get<PlaceSkillConfig>(((int)def.placeSkill).ToString());
        return cfg != null ? cfg.Image : null;
    }

    /// <summary>
    /// 按「主花色 + 主点数」推导图案素材名：card{SuitPascal}{RankName}。
    /// 多重花色 / 多重点数推得出来 —— 取排在最前的花色、最前的可显示点数当主角
    /// （N0123_s → cardHeartsSpider，N0_123 → cardHeartsA）
    /// 没有花色、或没有可显示的点数（没有点数 / 只有 -1 万能放下）→ null
    /// </summary>
    public static string GetFaceImageName(IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        if (suits == null || suits.Count == 0) return null;

        string suitName = GetSuitPascal(suits[0]);
        if (string.IsNullOrEmpty(suitName)) return null;

        string rankSuffix = GetRankSuffix(GetMainRank(ranks));
        return rankSuffix == null ? null : "card" + suitName + rankSuffix;
    }

    /// <summary>
    /// 主点数：点数列里第一个「能显示成图案」的点数。
    /// -1（万能放下）不是点数、也没有图案，跳过；没有可显示的点数则返回 null
    /// </summary>
    public static int? GetMainRank(IReadOnlyList<int> ranks)
    {
        if (ranks == null) return null;

        foreach (int rank in ranks)
        {
            if (rank != CardRankConst.Wild) return rank;
        }

        return null;
    }

    /// <summary>点数 → 图案后缀：-2 → Spider、1-13 → A..K；其余（-1 / 越界）推不出图案，返回 null</summary>
    private static string GetRankSuffix(int? rank)
    {
        if (rank == null) return null;

        int value = rank.Value;
        if (value == CardRankConst.Ignore) return "Spider";          // -2 蜘蛛
        if (value >= 1 && value <= 13) return GetRankName(value);

        return null;
    }

    /// <summary>花色 → Pascal 素材名（Hearts / Clubs / Spades / Diamonds）</summary>
    public static string GetSuitPascal(int suit)
    {
        switch ((E_CardSuitEnum)suit)
        {
            case E_CardSuitEnum.Hearts: return "Hearts";
            case E_CardSuitEnum.Clubs: return "Clubs";
            case E_CardSuitEnum.Spades: return "Spades";
            case E_CardSuitEnum.Diamonds: return "Diamonds";
            default: return null;
        }
    }

    /// <summary>花色 → 素材名（未知花色返回 null）</summary>
    public static string GetSuitName(int suit)
    {
        switch ((E_CardSuitEnum)suit)
        {
            case E_CardSuitEnum.Hearts: return "hearts";
            case E_CardSuitEnum.Clubs: return "clubs";
            case E_CardSuitEnum.Spades: return "spades";
            case E_CardSuitEnum.Diamonds: return "diamonds";
            default: return null;
        }
    }

    /// <summary>点数 → 素材 / 文本名：1→A、11→J、12→Q、13→K、-2→S，其余用数字</summary>
    public static string GetRankName(int rank)
    {
        switch (rank)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            case CardRankConst.Ignore: return "S";
            default: return rank.ToString();
        }
    }

    private Sprite LoadSprite(string resPath)
    {
        if (_spriteCache.TryGetValue(resPath, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>(resPath);
        if (sprite != null) _spriteCache[resPath] = sprite;
        return sprite;
    }
}
