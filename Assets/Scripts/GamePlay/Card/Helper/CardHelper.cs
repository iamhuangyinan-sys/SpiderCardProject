using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 牌预设（配表 CardPreSetConfig）→ 牌定义（CardDef）的转换与取用
///
/// 配表里一行 = 花色列表 + 点数列表 + 落牌技能 + 自带属性 + 价格。
/// 运行时牌数据只有 CardDef（没有 id），所以 Id 只在配表内部当主键 / 分组依据。
///
/// 用法：
///   CardHelper.TryGetDef("110001", out var def, out int price);
///   foreach (var cfg in CardHelper.PickRandom(E_CardPreSetTypeEnum.Spider, 2)) { ... }
/// </summary>
public static class CardHelper
{
    public const string RandomNormalPresetId = "100005";

    /// <summary>仅用于预设配置；构建牌定义时解析为实际点数或花色。</summary>
    private const int RandomValue = -9;

    private static readonly System.Random _random = new System.Random();

    /// <summary>整张预设表（按配表顺序）</summary>
    public static List<CardPreSetConfig> GetAll() => ConfigHelper.GetAll<CardPreSetConfig>();

    /// <summary>按 Id 取一行（取不到返回 null）</summary>
    public static CardPreSetConfig Get(string id)
    {
        return string.IsNullOrEmpty(id) ? null : ConfigHelper.Get<CardPreSetConfig>(id);
    }

    /// <summary>某个类型的全部预设（按配表顺序；表里没有这个类型时返回空表）</summary>
    public static List<CardPreSetConfig> GetByType(E_CardPreSetTypeEnum type)
    {
        var result = new List<CardPreSetConfig>();

        foreach (var cfg in GetAll())
        {
            if (cfg != null && cfg.Type == (int)type) result.Add(cfg);
        }

        return result;
    }

    public static List<(CardDef def, int price)> BuildShopPresetPool()
    {
        var pool = new List<(CardDef def, int price)>();
        foreach (var cfg in GetAll())
        {
            if (cfg == null || cfg.Type == (int)E_CardPreSetTypeEnum.Negative
                || cfg.Type == (int)E_CardPreSetTypeEnum.Normal) continue;

            if (TryToDef(cfg, out var def, out _))
            {
                pool.Add((def, cfg.Price));
            }
        }

        return pool;
    }

    /// <summary>
    /// 从某个类型里随机抽 count 条 —— **允许重复**：每次独立随机，不做出放回处理
    /// </summary>
    public static List<CardPreSetConfig> PickRandom(E_CardPreSetTypeEnum type, int count)
    {
        var result = new List<CardPreSetConfig>();
        if (count <= 0) return result;

        var pool = GetByType(type);
        if (pool.Count == 0)
        {
            Debug.LogWarning($"[CardHelper] 配表里没有 Type={(int)type}（{type}）的预设");
            return result;
        }

        for (int i = 0; i < count; i++)
        {
            result.Add(pool[_random.Next(pool.Count)]);
        }

        return result;
    }

    /// <summary>按 Id 取一条并转成牌定义（同时取出价格）；失败会打警告</summary>
    public static bool TryGetDef(string id, out CardDef def, out int price)
    {
        def = default;
        price = 0;

        var cfg = Get(id);
        if (cfg == null)
        {
            Debug.LogWarning($"[CardHelper] 配表里没有 Id={id} 的预设");
            return false;
        }

        if (!TryToDef(cfg, out def, out string error))
        {
            Debug.LogWarning($"[CardHelper] 预设 {id} 无效：{error}");
            return false;
        }

        price = cfg.Price;
        return true;
    }

    /// <summary>
    /// 配表行 → 牌定义。属性 / 落牌技能必须是已定义的枚举值；
    /// 先解析 -9 随机槽，再由 CardDef 排序去重；不修改原始配置
    /// </summary>
    public static bool TryToDef(CardPreSetConfig cfg, out CardDef def, out string error)
    {
        def = default;

        if (cfg == null)
        {
            error = "预设为空";
            return false;
        }

        var attributes = new List<E_CardAttributeEnum>();

        if (cfg.Attribute != null)
        {
            for (int i = 0; i < cfg.Attribute.Count; i++)
            {
                int value = cfg.Attribute[i];

                if (!Enum.IsDefined(typeof(E_CardAttributeEnum), value))
                {
                    error = $"未知属性 {value}";
                    return false;
                }

                attributes.Add((E_CardAttributeEnum)value);
            }
        }

        if (!Enum.IsDefined(typeof(E_PlaceSkillEnum), cfg.PlaceSkill))
        {
            error = $"未知落牌技能 {cfg.PlaceSkill}";
            return false;
        }

        if (!Enum.IsDefined(typeof(E_FlipSkillEnum), cfg.FlipSkill))
        {
            error = $"未知翻牌技能 {cfg.FlipSkill}";
            return false;
        }

        if (!TryResolveRanks(cfg.Rank, out var ranks, out error)) return false;
        if (!TryResolveSuits(cfg.Suit, out var suits, out error)) return false;

        return CardDef.TryCreate(suits, ranks, (E_PlaceSkillEnum)cfg.PlaceSkill, attributes,
            out def, out error, (E_FlipSkillEnum)cfg.FlipSkill);
    }

    /// <summary>解析随机点数：-9 从 A-K 无放回抽取，避开列表中所有固定点数。</summary>
    public static bool TryResolveRanks(IReadOnlyList<int> input, out List<int> ranks, out string error)
    {
        return TryResolveValues(input, 1, 13, CardRankConst.MaxCount, true, out ranks, out error);
    }

    /// <summary>解析随机花色：-9 从四花色无放回抽取，避开列表中所有固定花色。</summary>
    public static bool TryResolveSuits(IReadOnlyList<int> input, out List<int> suits, out string error)
    {
        return TryResolveValues(input, (int)E_CardSuitEnum.Hearts, (int)E_CardSuitEnum.Clubs,
            4, false, out suits, out error);
    }

    private static bool TryResolveValues(IReadOnlyList<int> input, int min, int max, int maxCount,
        bool isRank, out List<int> result, out string error)
    {
        result = null;
        error = null;
        var values = new List<int>();
        int randomCount = 0;
        string label = isRank ? "点数" : "花色";

        if (input != null)
        {
            // 必须先收集全部固定值，随机槽的位置不能影响排除规则。
            for (int i = 0; i < input.Count; i++)
            {
                int value = input[i];
                if (value == RandomValue)
                {
                    randomCount++;
                    continue;
                }

                bool valid = isRank ? CardDef.RankToChar(value) != '\0' : value >= min && value <= max;
                if (!valid)
                {
                    error = $"非法{label} {value}";
                    return false;
                }
                if (!values.Contains(value)) values.Add(value);
            }
        }

        if (values.Count + randomCount > maxCount)
        {
            error = $"{label}最多 {maxCount} 个，固定值去重后加随机槽共 {values.Count + randomCount} 个";
            return false;
        }

        var candidates = new List<int>();
        for (int value = min; value <= max; value++)
            if (!values.Contains(value)) candidates.Add(value);

        if (randomCount > candidates.Count)
        {
            error = $"随机{label}需要 {randomCount} 个，剩余候选只有 {candidates.Count} 个";
            return false;
        }

        for (int i = 0; i < randomCount; i++)
        {
            int index = _random.Next(candidates.Count);
            values.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        result = values;
        return true;
    }
}
