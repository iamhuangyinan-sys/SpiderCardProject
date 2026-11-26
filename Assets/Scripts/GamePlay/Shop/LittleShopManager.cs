using System;
using System.Collections.Generic;
using Framework.Mgr;

public class LittleShopManager : ManagerBase<LittleShopManager>
{
    private const string RandomDoubleRankPresetId = "220055";

    private readonly Random random = new();
    public bool IsPending => LittleShopStore.Instance.pending;
    public IReadOnlyList<ShopGoods> Goods => LittleShopStore.Instance.goods;
    protected override void OnInit() { }
    protected override void OnDispose() { }
    public void ResetForNewRun() => LittleShopStore.Instance.Clear();

    // 通关确认时就生成，确保收牌动画途中退出也不会丢失奖励选择。
    public void Prepare(string levelId)
    {
        if (IsPending || string.IsNullOrEmpty(levelId)
            || !LevelStore.Instance.allLevels.TryGetValue(levelId, out var level)
            || level.LevelType == (int)E_LevelTypeEnum.Boss) return;
        var pool = CardHelper.BuildShopPresetPool();
        var store = LittleShopStore.Instance;
        store.goods.Clear();
        for (int i = 0; i < 3; i++)
        {
            var goods = RollGoods(pool);
            goods.RollPoison(random);
            store.goods.Add(goods);
        }
        store.pending = true;
    }

    private ShopGoods RollGoods(List<(CardDef def, int price)> pool)
    {
        int roll = random.Next(100);
        if (roll >= 80 && pool.Count > 0)
        {
            var preset = pool[random.Next(pool.Count)];
            return new ShopGoods { def = preset.def.Clone(), price = RollPrice(preset.price, 0.8) };
        }

        if (roll >= 40 && roll < 80
            && CardHelper.TryGetDef(RandomDoubleRankPresetId, out var doubleRankDef, out int doubleRankPrice))
        {
            return new ShopGoods { def = doubleRankDef, price = RollPrice(doubleRankPrice, 0.9) };
        }

        if (CardHelper.TryGetDef(CardHelper.RandomNormalPresetId, out var normalDef, out int normalPrice))
        {
            return new ShopGoods { def = normalDef, price = normalPrice };
        }

        // 配置缺失时仍填满三张候选，保证续档能恢复小商店。
        return new ShopGoods { def = CardDef.Create(random.Next(4), random.Next(1, 14)), price = 0 };
    }

    private int RollPrice(int basePrice, double minFactor)
    {
        double factor = minFactor + random.NextDouble() * 0.2;
        return Math.Max(0, (int)Math.Round(basePrice * factor, MidpointRounding.AwayFromZero));
    }

    public bool TryBuy(int index)
    {
        if (!IsPending || index < 0 || index >= Goods.Count) return false;
        var goods = Goods[index];
        if (goods.sold || !RunManager.Instance.TrySpend(goods.FinalPrice)) return false;
        RunManager.Instance.AddCardToDeck(goods.def);
        goods.sold = true;
        LittleShopStore.Instance.pending = false;
        CardGameModule.Instance.SaveRun();
        return true;
    }

    public void Skip()
    {
        if (!IsPending) return;
        LittleShopStore.Instance.pending = false;
        CardGameModule.Instance.SaveRun();
    }
    public void ExportTo(RunSaveData data)
    {
        data.littleShopPending = IsPending;
        if (IsPending) data.littleShopGoods.AddRange(Goods);
    }
    public void ImportFrom(RunSaveData data)
    {
        ResetForNewRun();
        if (!data.littleShopPending || data.littleShopGoods == null || data.littleShopGoods.Count != 3) return;
        LittleShopStore.Instance.goods.AddRange(data.littleShopGoods);
        LittleShopStore.Instance.pending = true;
    }
}
