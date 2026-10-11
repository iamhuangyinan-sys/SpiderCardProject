using System.Collections.Generic;
using Framework.Mgr;
using UnityEngine;

/// <summary>
/// 卡牌资源管理器 —— 加载牌底 / 图案 / 花色 / 点数等素材
/// 素材都放在 Resources/GamePlay/Card/ 下：
///   Bg/cardBack · Bg/cardEmpty         牌背 / 空牌底
///   Image/{配表 Image}                  图案（唯一读配表的素材）
///   Suit/{hearts|clubs|spades|diamonds}  花色小图
///   Rank/{Red|Black}/{A..K|S}            点数小图
/// </summary>
public class CardResManager : ManagerBase<CardResManager>
{
    private const string CARD_DIR = "GamePlay/Card/";
    private const string BG_DIR = CARD_DIR + "Bg/";
    private const string SUIT_DIR = CARD_DIR + "Suit/";
    private const string RANK_DIR = CARD_DIR + "Rank/";

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

    /// <summary>点数小图（Rank/{Red|Black}/{A..K|S}）</summary>
    public Sprite GetRankSprite(int rank, bool isRed)
    {
        return LoadSprite($"{RANK_DIR}{(isRed ? "Red" : "Black")}/{GetRankName(rank)}");
    }

    // ==================== 整张牌图案（自动推导） ====================

    /// <summary>
    /// 取一张牌正面的图案：
    ///   特殊牌 → 配表 Image
    ///   普通牌（配表没有 / Image 为空）→ 按「主花色 + 主点数」自动推导（cardHeartsA / cardHeartsSpider…）
    ///   推不出来（没有花色 / 没有可显示的点数）→ null（不显示图案）
    ///
    /// 传进来的列表就是「显示用」列表：多重花色 / 多重点数也推得出来（取排在最前的那一个当主花色主点数），
    /// 被收敛过的牌传收敛后的列表 → 图案跟着一起换
    /// </summary>
    public Sprite GetFaceSprite(string encodedId, IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var cfg = GetCardConfig(encodedId);
        string imageName = cfg != null ? cfg.Image : null;

        if (string.IsNullOrEmpty(imageName)) imageName = GetFaceImageName(suits, ranks);

        return string.IsNullOrEmpty(imageName) ? null : GetCardImage(imageName);
    }

    /// <summary>只给编码 id 时取图案（列表里的牌用）</summary>
    public Sprite GetFaceSprite(string encodedId)
    {
        CardIdCodec.TryParse(encodedId, out var suits, out var ranks);
        return GetFaceSprite(encodedId, suits, ranks);
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

    /// <summary>按 id 查配表</summary>
    public CardConfig GetCardConfig(string id)
    {
        return ConfigHelper.Get<CardConfig>(id);
    }

    private Sprite LoadSprite(string resPath)
    {
        if (_spriteCache.TryGetValue(resPath, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>(resPath);
        if (sprite != null) _spriteCache[resPath] = sprite;
        return sprite;
    }
}
