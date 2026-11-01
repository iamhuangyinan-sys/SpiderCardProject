using System;
using System.Collections.Generic;
using Framework.Mgr;

public class LittleShopManager : ManagerBase<LittleShopManager>
{
    private readonly Random random = new();
    public bool IsPending => LittleShopStore.Instance.pending;
    public IReadOnlyList<ShopGoods> Goods => LittleShopStore.Instance.goods;
    protected override void OnInit() { }
    protected override void OnDispose() { }
    public void ResetForNewRun() => LittleShopStore.Instance.Clear();

    // 通关确认时就生成，确保收牌动画途中退出也不会丢失奖励选择。
    public void Prepare(string levelId)
    {
        if (IsPending || LevelManager.Instance.GetNextIds(levelId).Count == 0) return;
        var pool = new List<ShopGoods>();
        foreach (var cfg in CardPreSetHelper.GetAll())
            if (cfg != null && cfg.Type != (int)E_CardPreSetTypeEnum.Negative
                && CardPreSetHelper.TryToDef(cfg, out var def, out _))
                pool.Add(new ShopGoods { def = def, price = cfg.Price });
        var store = LittleShopStore.Instance;
        store.goods.Clear();
        for (int i = 0; i < 3; i++)
        {
            int roll = random.Next(100);
            int suit = random.Next(4), rank = random.Next(1, 14);
            ShopGoods goods;
            if (roll < 40 || (roll >= 80 && pool.Count == 0))
                goods = new ShopGoods { def = CardDef.Create(suit, rank), price = 0 };
            else if (roll < 80)
            {
                int second = random.Next(1, 13);
                if (second >= rank) second++;
                goods = new ShopGoods { def = CardDef.Create(new[] { suit }, new[] { rank, second }), price = random.Next(37, 44) };
            }
            else
            {
                var preset = pool[random.Next(pool.Count)];
                goods = new ShopGoods { def = preset.def.Clone(), price = Math.Max(0,
                    (int)Math.Round(preset.price * (0.8 + random.NextDouble() * 0.2), MidpointRounding.AwayFromZero)) };
            }
            store.goods.Add(goods);
        }
        store.pending = true;
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
