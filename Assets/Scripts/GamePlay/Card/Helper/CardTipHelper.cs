using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡牌悬停提示的组装 —— 由牌的「数据属性」推出该显示哪些条 tip。
///
/// 文案在配表里（PlaceSkillConfig / CardAttributeConfig / CardTipConfig），这里只做三件事：
///   判断属性是否成立 → 按顺序查表 → 包成 HoverTipData
/// 表里查不到的条目直接跳过（删一行 = 关掉那条 tip）。
///
/// 顺序（与配表 Id 分段一致）：
///   点数（1xx）→ 花色（2xx）→ 落牌技能 → 属性
/// 其中「任意放下 / 蜘蛛 / 无点数 / 无花色」四条带图标，图标直接复用牌面上画的那个
/// （都走 CardResManager，不另外配图）
/// </summary>
public static class CardTipHelper
{
    /// <summary>推导一张牌的所有 tip；没有可显示的属性时返回空列表</summary>
    /// <param name="def">牌定义（花色 / 点数 / 落牌技能）</param>
    /// <param name="attributes">牌的属性（null / 空 = 没有属性）</param>
    public static List<HoverTipData> Build(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes)
    {
        var tips = new List<HoverTipData>();
        if (!def.IsValid) return tips;

        var suits = def.suits;
        var ranks = def.ranks;

        var res = CardResManager.Instance;
        bool isRed = CardFaceHelper.IsRedSuit(suits);

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
            AddTip(tips, E_CardTipEnum.RankIgnore, res.GetRankSprite(CardRankConst.Ignore, isRed));
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
        if (def.HasSkill) AddSkillTip(tips, (int)def.placeSkill);

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
            tips.Add(new HoverTipData(cfg.Name, cfg.Description, icon));
        }
    }

    /// <summary>按 tip 类型查属性描述表（Id = 枚举值）；查不到就跳过。icon 可空（只有四条牌面 tip 带图标）</summary>
    private static void AddTip(List<HoverTipData> tips, E_CardTipEnum type, Sprite icon = null)
    {
        var cfg = ConfigHelper.Get<CardTipConfig>(((int)type).ToString());
        if (cfg != null) tips.Add(new HoverTipData(cfg.Name, cfg.Description, icon));
    }

    /// <summary>按落牌技能查技能描述表（Id = 枚举值）；查不到就跳过</summary>
    private static void AddSkillTip(List<HoverTipData> tips, int placeSkill)
    {
        var cfg = ConfigHelper.Get<PlaceSkillConfig>(placeSkill.ToString());
        if (cfg != null) tips.Add(new HoverTipData(cfg.Name, cfg.Description));
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
