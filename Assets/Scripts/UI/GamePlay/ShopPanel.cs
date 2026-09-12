using Framework.Event;
using Framework.UI;

/// <summary>
/// 商店面板 —— 展示随机刷新的商品，购买后点继续进入下一层
/// </summary>
public partial class ShopPanel : NormalPanel
{
    protected override void OnOpen()
    {
        comps.btnContinue.onClick.AddListener(OnClickContinue);
        EventManager.Instance.AddListener(E_EventEnum.OnShopChanged, RefreshList);

        // 打开即随机刷新商品（内部会派发 OnShopChanged → RefreshList）
        ShopManager.Instance.OpenShop();
    }

    protected override void OnShow() { }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnContinue.onClick.RemoveListener(OnClickContinue);
        EventManager.Instance.RemoveListener(E_EventEnum.OnShopChanged, RefreshList);
    }

    /// <summary>刷新商品列表</summary>
    private void RefreshList()
    {
        comps.listShopCard.OnItemRender = (index, item) =>
        {
            var cell = item as ShopCardItem;
            if (cell != null) cell.Bind(index, ShopStore.Instance.Get(index));
        };

        comps.listShopCard.ItemCount = ShopStore.Instance.goods.Count;
    }

    /// <summary>继续：完成商店关，回选关</summary>
    private void OnClickContinue()
    {
        ShopManager.Instance.ContinueNextLevel();
    }
}
