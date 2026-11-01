using System.Collections.Generic;

/// <summary>初始牌组配方。展示和正式开局必须共用此入口，Image 仅用于 UI。</summary>
public static class BeginBuildHelper
{
    public static bool TryBuild(string id, out List<CardDef> deck)
    {
        deck = new List<CardDef>();
        if (string.IsNullOrEmpty(id) || ConfigHelper.Get<BeginBuildConfig>(id) == null) return false;

        switch (id)
        {
            case "101":
                if (!CardPreSetHelper.TryGetDef("210001", out var spider, out _)) return false;
                AddSuit(deck, E_CardSuitEnum.Hearts, 2);
                AddSuit(deck, E_CardSuitEnum.Spades, 2);
                deck.Add(spider.Clone());
                return true;
            case "102":
                AddSuit(deck, E_CardSuitEnum.Diamonds, 2);
                AddSuit(deck, E_CardSuitEnum.Spades, 2);
                return ReplaceWithPreset(deck, E_CardSuitEnum.Diamonds, 5, "220032")
                    && ReplaceWithPreset(deck, E_CardSuitEnum.Diamonds, 8, "220042")
                    && ReplaceWithPreset(deck, E_CardSuitEnum.Diamonds, 11, "220012");
            default:
                UnityEngine.Debug.LogError($"[BeginBuildHelper] 初始牌组 {id} 尚未配置代码配方");
                return false;
        }
    }

    /// <summary>只替换一份普通牌，保持牌组总张数不变。</summary>
    private static bool ReplaceWithPreset(List<CardDef> deck, E_CardSuitEnum suit, int rank, string presetId)
    {
        if (!CardPreSetHelper.TryGetDef(presetId, out var replacement, out _)) return false;
        int index = deck.IndexOf(CardDef.Create((int)suit, rank));
        if (index < 0) return false;
        deck[index] = replacement.Clone();
        return true;
    }

    private static void AddSuit(List<CardDef> deck, E_CardSuitEnum suit, int copies)
    {
        for (int rank = 1; rank <= 13; rank++)
            for (int i = 0; i < copies; i++)
                deck.Add(CardDef.Create((int)suit, rank));
    }
}
