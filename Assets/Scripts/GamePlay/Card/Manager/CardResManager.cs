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

    /// <summary>花色 → 符号（文本用）</summary>
    public static string GetSuitSymbol(int suit)
    {
        switch ((E_CardSuitEnum)suit)
        {
            case E_CardSuitEnum.Hearts: return "♥";
            case E_CardSuitEnum.Clubs: return "♣";
            case E_CardSuitEnum.Spades: return "♠";
            case E_CardSuitEnum.Diamonds: return "♦";
            default: return "?";
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
