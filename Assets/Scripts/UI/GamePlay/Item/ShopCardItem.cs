using DG.Tweening;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 商店商品格 —— 显示牌图与价格，点击购买；鼠标悬停整个格子放大
/// </summary>
public partial class ShopCardItem : BaseListItem
{
    /// <summary>买牌飞行时长</summary>
    private const float BuyFlyDuration = 0.4f;

    /// <summary>买牌飞行结束时的缩放比例</summary>
    private const float BuyFlyEndScale = 0.3f;

    private int _index = -1;

    /// <summary>悬停缩放（挂在根节点上，缩放目标是内部那个轴心居中的 layout；没有就退化成不缩放）</summary>
    private HoverScaleBehaviour _hoverScale;

    public override void OnActivate()
    {
        if (_hoverScale == null) _hoverScale = GetComponent<HoverScaleBehaviour>();

        comps.modCard.OnHoverChanged = OnCardHover;
    }

    public override void OnRecycle()
    {
        comps.modCard.OnHoverChanged = null;
        _hoverScale?.SetHover(false, true);   // 回收：立即复位（组件 OnDisable 也会兜底）
        comps.modCard.Clear();
        _index = -1;
    }

    /// <summary>牌面悬停变化 → 整个格子缩放（连价格一起）</summary>
    private void OnCardHover(bool hovering) => _hoverScale?.SetHover(hovering);

    /// <summary>绑定商品并刷新显示（已售出的商品不进列表，这里不用管）</summary>
    public void Bind(int index, ShopGoods goods)
    {
        _index = index;

        if (goods == null)
        {
            comps.modCard.Clear();
            comps.txtCoin.text = string.Empty;
            return;
        }

        comps.modCard.Bind(goods.def);
        comps.modCard.SetClickable(OnClickCard);
        comps.txtCoin.text = goods.price.ToString();
    }

    private void OnClickCard()
    {
        if (!ShopManager.Instance.TryBuy(_index)) return;

        PlayBuyFly();
    }

    /// <summary>
    /// 买到手：藏掉价格，本体一边缩小一边飞向顶栏的牌组按钮，飞完销毁。
    /// 列表不重建 —— 只把这一格从列表里摘掉，其它格子位置不动；
    /// 这格留空，下次打开商店（列表重排）时已售出的商品就不会再出现了。
    /// </summary>
    private void PlayBuyFly()
    {
        _hoverScale?.SetHover(false, true);
        comps.modCard.HideTip();

        // 不碰 btnCard：SetClickable(null) 会把按钮关掉，按钮进 Disabled 态后牌面立刻变灰，很难看。
        // 飞行期间由 FlyAndVanish 加的 CanvasGroup（blocksRaycasts = false）挡住点击；
        // 万一漏进来一次，TryBuy 也会因为 goods.sold 直接返回 false。
        // 价格整块一起藏（img_Price 下面挂着金币底图和数字），只让牌面飞
        if (comps.imgPrice != null) comps.imgPrice.gameObject.SetActive(false);

        // 先从列表摘掉自己：否则滚动 / 刷新时这一格会被当成空闲格子回收，飞行被打断
        GetComponentInParent<ScrollList>()?.ReleaseItem(this);

        // 落点：顶栏牌组按钮（拿不到就原地消失）
        var anchor = UIManager.Instance.GetPanel<MainTopPanel>()?.CardBuildAnchor;
        if (anchor == null)
        {
            Destroy(gameObject);
            return;
        }

        // 挂到画布根节点：列表视口的遮罩会把飞出去的牌裁掉
        var canvas = GetComponentInParent<Canvas>();
        var tween = AnimationHelper.FlyAndVanish(RectTransform, anchor.position,
            canvas != null ? canvas.rootCanvas.transform : null, BuyFlyDuration, BuyFlyEndScale);

        if (tween == null)
        {
            Destroy(gameObject);
            return;
        }

        tween.OnComplete(() => Destroy(gameObject));
    }
}
