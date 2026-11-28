using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡牌悬停提示的组装 —— 由牌的「数据属性」推出该显示哪些条 tip。
///
/// 文案在配表里（PlaceSkillConfig / CardAttributeConfig / CardTipConfig），这里只做三件事：
///   判断属性是否成立 → 按顺序查表 → 包成 HoverTipData
/// 注意：表的 Name / Description 存的是 LanguageConfig 的 Id，取文案要过 LanguageHelper.Get。
/// 表里查不到的条目直接跳过（删一行 = 关掉那条 tip）。
///
/// 顺序（与配表 Id 分段一致）：
///   点数（1xx）→ 花色（2xx）→ 落牌技能 → 属性
/// 其中「任意放下 / 蜘蛛 / 无点数 / 无花色」四条带图标，图标直接复用牌面上画的那个
/// （都走 CardResManager，不另外配图）
/// 落牌技能和属性也复用属性栏的图标。
/// </summary>
public static class CardTipHelper
{
    /// <summary>推导一张牌的所有 tip；没有可显示的属性时返回空列表</summary>
    /// <param name="def">牌定义（花色 / 点数 / 落牌技能）</param>
    /// <param name="attributes">牌的属性（null / 空 = 没有属性）</param>
    /// <param name="relatedCardIds">按技能和配表顺序收集的关联牌预设 ID</param>
    public static List<HoverTipData> Build(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes,
        out List<string> relatedCardIds)
    {
        relatedCardIds = null;
        var tips = new List<HoverTipData>();
        if (!def.IsValid) return tips;

        var suits = def.suits;
        var ranks = def.ranks;

        var res = CardResManager.Instance;

        // ===== 1. 点数（配表 1xx）=====

        // 多重点数（-1 不是可显示点数，去掉后还有多个才算）
        if (CountVisibleRanks(ranks) > 1) AddTip(tips, E_CardTipEnum.MultiRank);

        // -1：万能放下
        if (HasRank(ranks, CardRankConst.Wild))
        {
            AddTip(tips, E_CardTipEnum.RankWild, res.GetWildRankSprite());
        }

        // -2：无视点数（蜘蛛），连接时只看花色
        if (HasRank(ranks, CardRankConst.Ignore))
        {
            AddTip(tips, E_CardTipEnum.RankIgnore, res.GetRankSprite(CardRankConst.Ignore),
                CardFaceHelper.GetRankColor(suits));
        }

        // 没有点数
        if (ranks == null || ranks.Count == 0)
        {
            AddTip(tips, E_CardTipEnum.NoRank, res.GetNoRankSprite());
        }

        // ===== 2. 花色（配表 2xx）=====

        // 多重花色
        if (suits != null && suits.Count > 1) AddTip(tips, E_CardTipEnum.MultiSuit);

        // 没有花色（跟谁都不是同花，不参与花色判断）
        if (suits == null || suits.Count == 0)
        {
            AddTip(tips, E_CardTipEnum.NoSuit, res.GetNoSuitSprite());
        }

        // ===== 3. 落牌技能（带技能的牌 = 以前的特殊牌，文案读 PlaceSkillConfig）=====
        // 技能表里的 RelatedBuff：把关联 Buff 的描述紧跟在技能 tip 之后（repeatBuffIds 用来跨技能去重）
        var repeatedBuffIds = new HashSet<string>();
        if (def.HasSkill)
        {
            relatedCardIds = AddSkillTip(tips, def.placeSkill, repeatedBuffIds);
        }
        if (def.flipSkill != E_FlipSkillEnum.None)
        {
            var cfg = ConfigHelper.Get<FlipSkillConfig>(((int)def.flipSkill).ToString());
            if (cfg != null)
            {
                tips.Add(new HoverTipData(LanguageHelper.Get(cfg.Name), LanguageHelper.Get(cfg.Description),
                    res.GetFlipSkillIcon(def.flipSkill)));
                AddRelatedBuffTips(tips, cfg.RelatedBuff, repeatedBuffIds);
                if (cfg.RelatedCard != null && cfg.RelatedCard.Count > 0)
                {
                    relatedCardIds ??= new List<string>();
                    relatedCardIds.AddRange(cfg.RelatedCard);
                }
            }
        }

        // ===== 4. 牌状态（沉底 / 毒等，文案读 CardAttributeConfig）=====
        AddAttributeTips(tips, attributes);

        return tips;
    }

    /// <summary>按属性枚举查属性描述表（Id = 枚举值）；查不到就跳过。图标一起带上（提示框会显示）</summary>
    private static void AddAttributeTips(List<HoverTipData> tips, IReadOnlyList<E_CardAttributeEnum> attributes)
    {
        if (attributes == null) return;

        for (int i = 0; i < attributes.Count; i++)
        {
            int attribute = (int)attributes[i];

            var cfg = ConfigHelper.Get<CardAttributeConfig>(attribute.ToString());
            if (cfg == null) continue;

            // 图标读表和加载都在 CardResManager 里（有缓存），拿不到就显示空图标
            var icon = CardResManager.Instance.GetAttributeSprite(attribute);
            tips.Add(new HoverTipData(LanguageHelper.Get(cfg.Name), LanguageHelper.Get(cfg.Description), icon));
        }
    }

    /// <summary>按 tip 类型查属性描述表（Id = 枚举值）；查不到就跳过。icon 可空（只有四条牌面 tip 带图标）</summary>
    private static void AddTip(List<HoverTipData> tips, E_CardTipEnum type, Sprite icon = null, Color? iconColor = null)
    {
        var cfg = ConfigHelper.Get<CardTipConfig>(((int)type).ToString());
        if (cfg != null)
        {
            tips.Add(new HoverTipData(LanguageHelper.Get(cfg.Name), LanguageHelper.Get(cfg.Description), icon, iconColor));
        }
    }

    /// <summary>按落牌技能查技能描述表（Id = 枚举值）；查不到就跳过。技能 tip 后面紧跟它的关联 Buff 描述</summary>
    private static List<string> AddSkillTip(List<HoverTipData> tips, E_PlaceSkillEnum placeSkill,
        HashSet<string> repeatedBuffIds)
    {
        var cfg = ConfigHelper.Get<PlaceSkillConfig>(((int)placeSkill).ToString());
        if (cfg == null) return null;

        var icon = CardResManager.Instance.GetPlaceSkillIcon(placeSkill);
        tips.Add(new HoverTipData(LanguageHelper.Get(cfg.Name), LanguageHelper.Get(cfg.Description), icon));
        AddRelatedBuffTips(tips, cfg.RelatedBuff, repeatedBuffIds);
        return cfg.RelatedCard != null && cfg.RelatedCard.Count > 0 ? new List<string>(cfg.RelatedCard) : null;
    }

    /// <summary>
    /// 技能表 RelatedBuff 列里的 Buff 描述 —— 紧跟在技能 tip 之下作为「关联描述」。
    /// 空 id / 配表查不到 / 已在前面出现过的 Buff 直接跳过（落牌技能和翻牌技能可能引用同一个 Buff）。
    /// </summary>
    private static void AddRelatedBuffTips(List<HoverTipData> tips, List<string> relatedBuff,
        HashSet<string> repeatedBuffIds)
    {
        if (relatedBuff == null) return;

        for (int i = 0; i < relatedBuff.Count; i++)
        {
            string id = relatedBuff[i];
            if (string.IsNullOrEmpty(id) || !repeatedBuffIds.Add(id)) continue;

            var cfg = ConfigHelper.Get<BuffConfig>(id);
            if (cfg == null) continue;

            // 与 BuffItem 的 tip 保持一致：只显示名字 + 描述，不带图标
            tips.Add(new HoverTipData(LanguageHelper.Get(cfg.Name), LanguageHelper.Get(cfg.Description)));
        }
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

    /// <summary>「可显示」的点数数量（-1 万能不显示点数，不算在内）</summary>
    private static int CountVisibleRanks(IReadOnlyList<int> ranks)
    {
        if (ranks == null) return 0;

        int count = 0;
        for (int i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] != CardRankConst.Wild) count++;
        }

        return count;
    }
}
