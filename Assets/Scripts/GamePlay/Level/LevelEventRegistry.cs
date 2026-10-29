using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 开局事件注册表 —— 配表 LevelEventConfig 的 Id → 代码里的行为流程
///
/// 表里只放 Id + Description（文案给选关界面看），**行为与参数全在这里**：
/// 事件不一定是「洗入牌」，改列数 / 扣金币 / 播演出都可以写。
///
/// 约定：
///   1. handler 只做「发牌之前」的额外流程，**不要自己调 Deal()** —— 发牌由 CardsManager 统一收尾
///   2. 新增事件：在这里写一个 handler + Register(事件 Id, handler)，并在配表加一行
///   3. 事件 Id = 0 表示没有开局事件（不需要注册）
/// </summary>
public static class LevelEventRegistry
{
    /// <summary>开局事件的额外流程（发牌前）</summary>
    public delegate IEnumerator LevelEventRoutine();

    // ==================== 事件 Id（对应配表 LevelEventConfig.Id） ====================

    /// <summary>开始时往发牌堆中洗入一张万能蜘蛛牌</summary>
    private const int EventAddSpider = 11001;

    /// <summary>开始时往发牌堆中洗入 3 张随机花色点数牌 + 一张空白牌</summary>
    private const int EventAddRandom = 11002;

    // ==================== 注册 / 取用 ====================

    private static readonly Dictionary<int, LevelEventRoutine> _routines = new();

    private static bool _registered;

    /// <summary>取某个事件的流程；0 / 未注册都返回 null（= 直接发牌）</summary>
    public static LevelEventRoutine Get(int eventId)
    {
        EnsureRegistered();

        if (eventId == 0) return null;
        if (_routines.TryGetValue(eventId, out var routine)) return routine;

        Debug.LogWarning($"[LevelEventRegistry] 事件 {eventId} 没有注册处理逻辑（或配表没这一行），按无事件处理");
        return null;
    }

    private static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;

        Register(EventAddSpider, AddSpider);
        Register(EventAddRandom, AddRandom);
    }

    private static void Register(int eventId, LevelEventRoutine routine)
    {
        _routines[eventId] = routine;
    }

    // ==================== 各事件的具体实现（参数也在这） ====================

    /// <summary>洗入一张万能蜘蛛（4 花色 + 无视点数）</summary>
    private static IEnumerator AddSpider()
    {
        var defs = new List<CardDef>
        {
            CardDef.Create(new[] { 0, 1, 2, 3 }, new[] { CardRankConst.Ignore }),
        };

        yield return CardsManager.Instance.WashInAndWait(defs);
    }

    /// <summary>洗入 3 张随机花色点数牌 + 一张空白牌（无花色无点数）</summary>
    private static IEnumerator AddRandom()
    {
        var defs = new List<CardDef>
        {
            CardHelper.RandomDef(),
            CardHelper.RandomDef(),
            CardHelper.RandomDef(),
            CardDef.Create(null, null),
        };

        yield return CardsManager.Instance.WashInAndWait(defs);
    }
}
