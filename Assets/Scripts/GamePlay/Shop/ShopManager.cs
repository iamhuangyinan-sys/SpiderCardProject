using System.Collections.Generic;
using Framework.Event;
using Framework.Mgr;

/// <summary>
/// 商店业务层 —— 刷新商品、购买、继续进入下一层
///
/// 商品构成：常驻两张技能牌（复制 / 回收），价格先写死（TODO：价格规则后续再做）
/// </summary>
public class ShopManager : ManagerBase<ShopManager>
{
    /// <summary>
    /// 常驻技能牌（顺序即商品顺序）：牌定义 + 价格。
    /// TODO: 价格目前写死，后续接价格表
    /// </summary>
    private static readonly (CardDef def, int price)[] SpecialGoods =
    {
        (CardDef.Create(null, new[] { CardRankConst.Wild }, E_PlaceSkillEnum.Copy), 150),      // 复制
        (CardDef.Create(null, new[] { CardRankConst.Wild }, E_PlaceSkillEnum.Recycle), 80),    // 回收
    };

    /// <summary>本次商店的删牌上限（暂写死；后续可扩展成 LevelConfig 的字段）</summary>
    private const int DefaultDeleteLimit = 5;

    /// <summary>删牌单次费用（暂写死）</summary>
    private const int DefaultDeletePrice = 20;

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

        // 删牌次数不用在这里重置：一次商店结束（ContinueNextLevel → ShopStore.Clear）就已经归零了，
        // 关卡不能回头，所以「新进商店」时它本来就是 0；只有续档进来的那一次要保持存档里的值

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
    /// 刷新商品：常驻技能牌（价格先写死，后续接价格表）
    /// </summary>
    private void RandomizeGoods()
    {
        var store = ShopStore.Instance;
        store.goods.Clear();

        foreach (var (def, price) in SpecialGoods)
        {
            if (!def.IsValid) continue;

            store.goods.Add(new ShopGoods
            {
                def = def,
                price = price,
            });
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
        RunManager.Instance.AddCardToDeck(goods.def);

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

    // ==================== 删牌 ====================

    /// <summary>本次商店的删牌上限</summary>
    public int DeleteLimit => DefaultDeleteLimit;

    /// <summary>删牌单次费用</summary>
    public int DeletePrice => DefaultDeletePrice;

    /// <summary>本次商店已用的删牌次数</summary>
    public int DeleteCount => ShopStore.Instance.deleteCount;

    /// <summary>次数已到上限</summary>
    public bool IsDeleteLimitReached => DeleteCount >= DeleteLimit;

    /// <summary>金币够不够删一次</summary>
    public bool IsDeleteAffordable => RunManager.Instance.IsEnough(DeletePrice);

    /// <summary>能不能删牌（次数没到上限 + 金币够）</summary>
    public bool CanDelete => !IsDeleteLimitReached && IsDeleteAffordable;

    /// <summary>
    /// 从牌组里删掉一张牌：扣一次次数 + 扣费用
    /// 牌不在牌组里就整个不动（不扣钱也不扣次数）
    /// </summary>
    public bool TryDeleteCard(CardDef def)
    {
        if (!def.IsValid) return false;
        if (!CanDelete) return false;

        // 先移除再扣钱：移除失败就什么都不动
        if (!RunManager.Instance.RemoveCardFromDeck(def)) return false;

        RunManager.Instance.TrySpend(DeletePrice);
        ShopStore.Instance.deleteCount++;

        ShopStore.Instance.Refresh();
        EventManager.Instance.Dispatch(E_EventEnum.OnShopChanged);

        // 删了不能反悔：立即落盘
        CardGameModule.Instance.SaveRun();
        return true;
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

        if (!IsInShop)
        {
            // 不在商店里就不写，避免把上一次商店的残留带进存档
            return;
        }

        foreach (var g in store.goods)
        {
            data.shopGoods.Add(new ShopGoods { def = g.def, price = g.price, sold = g.sold });
        }

        // 删牌次数也要落盘：退出重进同一个商店要保持一样（次数不能洗回来）
        data.shopDeleteCount = store.deleteCount;
    }

    /// <summary>从存档恢复商店商品（含已售出标记）与已用删牌次数</summary>
    public void ImportFrom(RunSaveData data)
    {
        var store = ShopStore.Instance;
        store.Clear();
        _goodsPrepared = false;

        if (data.shopGoods == null || data.shopGoods.Count == 0) return;

        foreach (var g in data.shopGoods)
        {
            store.goods.Add(new ShopGoods { def = g.def, price = g.price, sold = g.sold });
        }

        _goodsPrepared = true;   // 已有商品 → 不再重新随机

        // 续档进来：把已用的删牌次数也恢复（存档里那一次商店还没结束）
        store.deleteCount = data.shopDeleteCount;
    }
}
