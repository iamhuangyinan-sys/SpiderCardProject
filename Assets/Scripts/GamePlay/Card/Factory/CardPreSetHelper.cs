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
///   CardPreSetHelper.TryGetDef("110001", out var def, out int price);
///   foreach (var cfg in CardPreSetHelper.PickRandom(E_CardPreSetTypeEnum.Spider, 2)) { ... }
/// </summary>
public static class CardPreSetHelper
{
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
            Debug.LogWarning($"[CardPreSetHelper] 配表里没有 Type={(int)type}（{type}）的预设");
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
            Debug.LogWarning($"[CardPreSetHelper] 配表里没有 Id={id} 的预设");
            return false;
        }

        if (!TryToDef(cfg, out def, out string error))
        {
            Debug.LogWarning($"[CardPreSetHelper] 预设 {id} 无效：{error}");
            return false;
        }

        price = cfg.Price;
        return true;
    }

    /// <summary>
    /// 配表行 → 牌定义。属性 / 落牌技能必须是已定义的枚举值；
    /// 花色 / 点数的规范化（排序去重、点数上限）交给 CardDef
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

        return CardDef.TryCreate(cfg.Suit, cfg.Rank, (E_PlaceSkillEnum)cfg.PlaceSkill, attributes,
            out def, out error);
    }
}
