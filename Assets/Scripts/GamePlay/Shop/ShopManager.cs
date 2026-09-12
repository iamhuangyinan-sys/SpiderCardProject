using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 商店业务层 —— 随机刷新商品、购买、继续进入下一层
///
/// 商品构成：
///   前 2 件：Id 以 "02" 开头的特殊牌，单价 100
///   后 4 件：Id 以 "01" 开头的普通牌，单价 0
/// </summary>
public class ShopManager : ManagerBase<ShopManager>
{
    /// <summary>特殊牌（Id 以 02 开头）数量</summary>
    public const int SpecialCount = 2;

    /// <summary>特殊牌价格</summary>
    public const int SpecialPrice = 100;

    /// <summary>普通牌（Id 以 01 开头）数量</summary>
    public const int NormalCount = 4;

    /// <summary>普通牌价格</summary>
    public const int NormalPrice = 0;

    private const string SpecialPrefix = "02";
    private const string NormalPrefix = "01";

    private readonly System.Random _random = new System.Random();

    protected override void OnInit() { }

    protected override void OnDispose()
    {
        ShopStore.Instance.Clear();
    }

    // ==================== 刷新商品 ====================

    /// <summary>打开商店：随机刷新商品（前 2 张特殊牌，后 4 张普通牌）</summary>
    public void OpenShop()
    {
        ShopStore.Instance.goods.Clear();

        var all = ConfigHelper.GetAll<CardConfig>();
        AppendRandomGoods(all, SpecialPrefix, SpecialCount, SpecialPrice);
        AppendRandomGoods(all, NormalPrefix, NormalCount, NormalPrice);

        ShopStore.Instance.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnShopChanged);
    }

    /// <summary>按 id 前缀随机挑 count 张牌加入商品列表（同批不重复；不足则有多少取多少）</summary>
    private void AppendRandomGoods(List<CardConfig> all, string idPrefix, int count, int price)
    {
        var candidates = new List<string>();
        foreach (var cfg in all)
        {
            if (!string.IsNullOrEmpty(cfg.Id) && cfg.Id.StartsWith(idPrefix))
            {
                candidates.Add(cfg.Id);
            }
        }

        Shuffle(candidates);

        int take = count < candidates.Count ? count : candidates.Count;
        for (int i = 0; i < take; i++)
        {
            ShopStore.Instance.goods.Add(new ShopGoods
            {
                cardId = candidates[i],
                price = price,
            });
        }
    }

    /// <summary>Fisher-Yates 洗牌</summary>
    private void Shuffle(List<string> list)
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

        // TODO: 把该牌加入玩家牌组（失败应回滚金币）

        // 扣金币
        RunManager.Instance.TrySpend(goods.price);

        goods.sold = true;

        ShopStore.Instance.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnShopChanged);
        return true;
    }

    /// <summary>继续：完成当前商店关（解锁下一批关卡、层号推进），并通知 UI 回选关</summary>
    public void ContinueNextLevel()
    {
        var levelId = LevelStore.Instance.selectedLevelId;

        ShopStore.Instance.Clear();

        LevelManager.Instance.CompleteLevel(levelId);
        LevelManager.Instance.SettleLevelReward();   // 发放本关奖励（与层号刷新同时机）

        EventManager.Instance.Dispatch(E_EventEnum.OnShopClosed);
    }
}
