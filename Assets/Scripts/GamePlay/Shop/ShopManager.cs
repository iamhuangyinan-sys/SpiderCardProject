using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 商店业务层 —— 刷新商品、购买、继续进入下一层
///
/// 商品构成（共 8 件）：
///   前 5 件：常驻特殊牌，价格取配表 Price 并随机上下波动 ±10%
///   后 3 件：随机 01 开头普通牌，价格直接取配表 Price（不波动）
/// </summary>
public class ShopManager : ManagerBase<ShopManager>
{
    /// <summary>常驻特殊牌（顺序即商品顺序）</summary>
    private static readonly string[] SpecialCardIds =
    {
        "0200005", "0200001", "0200003", "0200006", "0200007",
    };

    /// <summary>随机普通牌（Id 以 01 开头）数量</summary>
    private const int NormalCount = 3;

    /// <summary>特殊牌价格波动幅度（±10%）</summary>
    private const float SpecialPriceFluctuation = 0.1f;

    private const string NormalPrefix = "01";

    private readonly System.Random _random = new System.Random();

    /// <summary>本关商店的商品是否已备好（防止退出重进刷商品）</summary>
    private bool _goodsPrepared;

    /// <summary>是否正停在商店里</summary>
    public bool IsInShop => !string.IsNullOrEmpty(ShopStore.Instance.shopLevelId);

    protected override void OnInit() { }

    protected override void OnDispose()
    {
        ShopStore.Instance.Clear();
        _goodsPrepared = false;
    }

    // ==================== 进入商店 ====================

    /// <summary>
    /// 进入商店关：首次进入才随机商品，之后（含退出重进）沿用同一批商品
    /// 商品落盘由调用方（SelectLevel）在之后统一 SaveRun
    /// </summary>
    public void EnterShop(string levelId)
    {
        var store = ShopStore.Instance;

        // 换了一个商店关 → 商品作废，重新随机
        if (!string.IsNullOrEmpty(store.shopLevelId) && store.shopLevelId != levelId)
        {
            _goodsPrepared = false;
        }

        store.shopLevelId = levelId;

        if (!_goodsPrepared)
        {
            RandomizeGoods();
            _goodsPrepared = true;
        }

        store.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnShopChanged);
    }

    // ==================== 刷新商品 ====================

    /// <summary>
    /// 刷新商品：前 5 张常驻特殊牌（价格读配表并 ±10% 波动），
    /// 后 3 张随机普通牌（价格直接用配表 Price）
    /// </summary>
    private void RandomizeGoods()
    {
        var store = ShopStore.Instance;
        store.goods.Clear();

        // 1. 常驻特殊牌：固定 5 张，顺序固定
        foreach (var cardId in SpecialCardIds)
        {
            var cfg = ConfigHelper.Get<CardConfig>(cardId);
            if (cfg == null) continue;   // 配表里没有这张牌：跳过

            store.goods.Add(new ShopGoods
            {
                cardId = cardId,
                price = FluctuatePrice(cfg.Price),
            });
        }

        // 2. 随机普通牌：01 开头，价格不波动
        AppendRandomNormalGoods(NormalCount);
    }

    /// <summary>价格在配表 Price 基础上随机上下波动 ±10%（四舍五入到整数）</summary>
    private int FluctuatePrice(int basePrice)
    {
        float factor = 1f + ((float)_random.NextDouble() * 2f - 1f) * SpecialPriceFluctuation;
        return (int)System.Math.Round(basePrice * factor);
    }

    /// <summary>按 Id 前缀随机挑 count 张普通牌加入商品列表（同批不重复；不足则有多少取多少）</summary>
    private void AppendRandomNormalGoods(int count)
    {
        var candidates = new List<CardConfig>();
        foreach (var cfg in ConfigHelper.GetAll<CardConfig>())
        {
            if (!string.IsNullOrEmpty(cfg.Id) && cfg.Id.StartsWith(NormalPrefix))
            {
                candidates.Add(cfg);
            }
        }

        Shuffle(candidates);

        int take = count < candidates.Count ? count : candidates.Count;
        for (int i = 0; i < take; i++)
        {
            ShopStore.Instance.goods.Add(new ShopGoods
            {
                cardId = candidates[i].Id,
                price = candidates[i].Price,   // 普通牌价格直接用配表值（当前是 0），不波动
            });
        }
    }

    /// <summary>Fisher-Yates 洗牌</summary>
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ==================== 购买 / 继续 ====================

    /// <summary>购买某件商品（牌组系统尚未接入，当前只做金币结算）</summary>
    public bool TryBuy(int index)
    {
        var goods = ShopStore.Instance.Get(index);
        if (goods == null || goods.sold) return false;

        // 金币不足 → 买不了
        if (!RunManager.Instance.IsEnough(goods.price)) return false;

        // 扣金币 + 把该牌加入本局牌组
        RunManager.Instance.TrySpend(goods.price);
        RunManager.Instance.AddCardToDeck(goods.cardId);

        goods.sold = true;

        ShopStore.Instance.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnShopChanged);

        // 已购买不能反悔：立即落盘
        CardGameModule.Instance.SaveRun();
        return true;
    }

    /// <summary>继续：完成当前商店关（解锁下一批关卡、层号推进），并通知 UI 回选关</summary>
    public void ContinueNextLevel()
    {
        var levelId = LevelStore.Instance.selectedLevelId;

        ShopStore.Instance.Clear();
        _goodsPrepared = false;

        LevelManager.Instance.CompleteLevel(levelId);
        LevelManager.Instance.SettleLevelReward();   // 发放本关奖励（与层号刷新同时机）

        CardGameModule.Instance.SaveRun();           // 商店结束，进度落盘

        EventManager.Instance.Dispatch(E_EventEnum.OnShopClosed);
    }

    // ==================== 存档 ====================

    /// <summary>
    /// 导出商店状态（存档用）
    /// 只有停在商店里时才导出商品；否则不写（避免把上一次商店的残留带进存档）
    /// </summary>
    public void ExportTo(RunSaveData data)
    {
        var store = ShopStore.Instance;

        data.shopGoods.Clear();
        if (!IsInShop) return;

        foreach (var g in store.goods)
        {
            data.shopGoods.Add(new ShopGoods { cardId = g.cardId, price = g.price, sold = g.sold });
        }
    }

    /// <summary>从存档恢复商店商品（含已售出标记）</summary>
    public void ImportFrom(RunSaveData data)
    {
        var store = ShopStore.Instance;
        store.Clear();
        _goodsPrepared = false;

        if (data.shopGoods == null || data.shopGoods.Count == 0) return;

        foreach (var g in data.shopGoods)
        {
            store.goods.Add(new ShopGoods { cardId = g.cardId, price = g.price, sold = g.sold });
        }

        _goodsPrepared = true;   // 已有商品 → 不再重新随机
    }
}
