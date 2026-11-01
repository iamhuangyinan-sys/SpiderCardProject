using System.Collections.Generic;
using Framework.Store;

public class LittleShopStore : StoreBase<LittleShopStore>
{
    public bool pending;
    public readonly List<ShopGoods> goods = new();
    public void Clear() { pending = false; goods.Clear(); }
    protected override void OnInit() => Clear();
    protected override void OnDispose() => Clear();
}
