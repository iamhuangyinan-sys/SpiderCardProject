using System.Collections.Generic;
using System.Globalization;

/// <summary>构建初始牌组，选组时解析预设里的随机槽位。</summary>
public static class BeginBuildHelper
{
    public static bool TryBuildForDifficulty(IReadOnlyList<CardDef> baseDeck, string difficultyId, out List<CardDef> deck)
    {
        deck = null;
        if (!int.TryParse(difficultyId, NumberStyles.None, CultureInfo.InvariantCulture, out int difficulty)
            || difficulty < 1) return false;

        var cards = new List<CardDef>(baseDeck);
        if (difficulty >= 2 && !AddPreset(cards, "710003")) return false;
        deck = cards;
        return true;
    }

    public static bool TryBuildBaseDeck(BeginBuildConfig config, out List<CardDef> deck)
    {
        deck = new List<CardDef>();
        var firstSuit = (E_CardSuitEnum)config.Suit.x;
        var secondSuit = (E_CardSuitEnum)config.Suit.y;
        if (!System.Enum.IsDefined(typeof(E_CardSuitEnum), firstSuit)
            || !System.Enum.IsDefined(typeof(E_CardSuitEnum), secondSuit))
        {
            UnityEngine.Debug.LogError($"[BeginBuildHelper] 初始牌组 {config.Id} 的主要花色无效：{config.Suit}");
            return false;
        }

        AddSuit(deck, firstSuit, 2);
        AddSuit(deck, secondSuit, 2);
        switch (config.Id)
        {
            case "101":
                return AddPreset(deck, "210001");
            case "102":
                return ReplaceWithPreset(deck, E_CardSuitEnum.Hearts, 5, "220031")
                    && ReplaceWithPreset(deck, E_CardSuitEnum.Hearts, 8, "220041")
                    && AddPreset(deck, "220051");
            case "103":
                return ReplaceWithPreset(deck, E_CardSuitEnum.Hearts, 1, "230005")
                    && ReplaceWithPreset(deck, E_CardSuitEnum.Hearts, 13, "230006")
                    && AddPreset(deck, "230001");
            default:
                UnityEngine.Debug.LogError($"[BeginBuildHelper] 初始牌组 {config.Id} 尚未配置代码配方");
                return false;
        }
    }

    private static bool AddPreset(List<CardDef> deck, string presetId)
    {
        if (!CardHelper.TryGetDef(presetId, out var def, out _)) return false;
        deck.Add(def);
        return true;
    }

    /// <summary>只替换一份普通牌，保持牌组总张数不变。</summary>
    private static bool ReplaceWithPreset(List<CardDef> deck, E_CardSuitEnum suit, int rank, string presetId)
    {
        if (!CardHelper.TryGetDef(presetId, out var replacement, out _)) return false;
        int index = deck.IndexOf(CardDef.Create((int)suit, rank));
        if (index < 0) return false;
        deck[index] = replacement;
        return true;
    }

    internal static void AddSuit(List<CardDef> deck, E_CardSuitEnum suit, int copies)
    {
        for (int rank = 1; rank <= 13; rank++)
            for (int i = 0; i < copies; i++)
                deck.Add(CardDef.Create((int)suit, rank));
    }
}
