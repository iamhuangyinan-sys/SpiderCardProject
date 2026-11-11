using System.Collections.Generic;
using System.Globalization;

/// <summary>难度的显示顺序和文案。</summary>
public static class DifficultyHelper
{
    public static List<DifficultyConfig> GetAll()
    {
        // TODO: 接入难度解锁筛选，目前全部可选。
        var result = new List<DifficultyConfig>(ConfigHelper.GetAll<DifficultyConfig>());
        result.Sort((a, b) => int.Parse(a.Id, CultureInfo.InvariantCulture)
            .CompareTo(int.Parse(b.Id, CultureInfo.InvariantCulture)));
        return result;
    }

    public static string GetNumber(string id) => int.Parse(id, CultureInfo.InvariantCulture)
        .ToString(CultureInfo.InvariantCulture);

    public static string GetName(string id) => LanguageHelper.Get("difficulty_name", GetNumber(id));

    public static string GetDescription(DifficultyConfig config) =>
        string.IsNullOrWhiteSpace(config.Description) ? string.Empty : LanguageHelper.Get(config.Description);

    public static HoverTipData GetTip(string id)
    {
        var all = GetAll();
        int index = all.FindIndex(config => config.Id == id);
        if (index < 0) return default;
        string name = GetName(id);
        if (index == 0) return new HoverTipData(null, name);
        var descriptions = new List<string>();
        for (int i = 0; i <= index; i++)
        {
            string description = GetDescription(all[i]);
            if (!string.IsNullOrWhiteSpace(description)) descriptions.Add(description);
        }
        return new HoverTipData(name, string.Join("\n", descriptions));
    }
}
