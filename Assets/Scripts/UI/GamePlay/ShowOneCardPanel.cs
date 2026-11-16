using System;
using System.Collections.Generic;
using Framework;
using Framework.Res;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 单张牌详情面板 —— 把一张牌放大显示
/// 牌面复用 ShowCardItem（运行时生成到 img_CardContent 下，和列表同一套显示 / 悬停逻辑），
/// 悬停动效与 tip 照常；详情里的牌不可点击
///
/// 两个入口，淡入淡出方式不同：
///   展示牌面板点格子 → OpenFrom：复用来源的黑底，只淡入淡出内容，关掉后回到原面板
///   场上牌右键 → ShowOne：自带黑底，整个界面（含黑底）一起淡入淡出
///
/// 遮挡场景输入（实现 ISceneInputBlockPanel）
/// </summary>
public partial class ShowOneCardPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;

    private const string ItemPath = "Prefab/UI/GamePlay/Item/ShowCardItem";

    /// <summary>放大倍数（列表里的牌是 1 倍，这里 2.1 倍）</summary>
    private const float ItemScale = 2.1f;

    /// <summary>悬停倍率（列表里是 1.4，这里是 1.2，避免放大后叠加过大）</summary>
    private const float HoverScale = 1.2f;

    private GameObject _itemPrefab;
    private ShowCardItem _item;

    /// <summary>来源列表（null = 场上牌单张展示，没有左右切换）</summary>
    private ShowCardsPanel _source;

    /// <summary>来源列表里当前展示的下标</summary>
    private int _index;

    private CardDef _def;
    private IReadOnlyList<E_CardAttributeEnum> _attributes;
    private bool _useInGameAttributes;

    /// <summary>复用来源面板的黑底（此时自己的黑底不显示、不淡入）</summary>
    private bool _reuseSourceBackground;

    /// <summary>关闭后回到来源面板</summary>
    private Action _returnToSource;

    private PanelBackdrop Backdrop => GetComponent<PanelBackdrop>();

    /// <summary>放大显示一张牌（自己带黑底，整个界面淡入淡出；没有左右切换）—— 场上牌右键用</summary>
    public static void ShowOne(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes = null,
        bool useInGameAttributes = false)
    {
        UIManager.Instance.Show<ShowOneCardPanel>(p => p.SetupSingle(def, attributes, useInGameAttributes));
    }

    /// <summary>
    /// 从展示牌面板打开第 index 张：可左右翻看同一个列表的其他牌；
    /// 先淡出来源面板的内容（黑底留着），关闭时反过来并把来源面板重新显示出来
    /// </summary>
    public static void OpenFrom(ShowCardsPanel source, int index)
    {
        if (source == null || !source.IsOpen || source.IsTransitioning || SceneController.Instance.IsLoading
            || UIManager.Instance.IsOpen<ShowOneCardPanel>()) return;

        source.KeepBackground();   // 先置位： HideSelf 退场时才不会把黑底收掉
        source.HideSelf(() => OpenAfterSourceHidden(source, index));
    }

    private static void OpenAfterSourceHidden(ShowCardsPanel source, int index)
    {
        if (source == null || source.IsOpen || SceneController.Instance.IsLoading
            || UIManager.Instance.GetPanel<ShowCardsPanel>() != source) return;

        var panel = UIManager.Instance.Show<ShowOneCardPanel>(p => p.SetupFromSource(source, index));
        if (panel == null)
        {
            UIManager.Instance.Show<ShowCardsPanel>();
            return;
        }

        panel._returnToSource = () =>
        {
            if (source != null && !SceneController.Instance.IsLoading
                && UIManager.Instance.GetPanel<ShowCardsPanel>() == source)
                UIManager.Instance.Show<ShowCardsPanel>();
        };
    }

    protected override void OnOpen()
    {
        comps.btnClose.onClick.AddListener(OnClickClose);
        if (comps.btnLeft != null) comps.btnLeft.onClick.AddListener(OnClickLeft);
        if (comps.btnRight != null) comps.btnRight.onClick.AddListener(OnClickRight);

        // 自己带黑底时才淡入（复用来源黑底时它是关的）
        if (!_reuseSourceBackground) Backdrop.Show(true);

        _itemPrefab = ResManager.Instance.Load<GameObject>(ItemPath);
        CreateItem();
        RefreshCard();
    }

    protected override void OnShow()
    {
        if (!_reuseSourceBackground) Backdrop.Show(true);

        // 命中缓存复用时重新绑一次（列表那边可能换了牌）
        RefreshCard();
    }

    protected override void OnHide() { }

    protected override bool OnPanelClosing()
    {
        // 黑底自己去淡出（与内容同时长 0.2s），复用来源黑底时不动它
        if (!_reuseSourceBackground) Backdrop.Hide(true);
        return base.OnPanelClosing();
    }

    protected override void OnClose()
    {
        comps.btnClose.onClick.RemoveListener(OnClickClose);
        if (comps.btnLeft != null) comps.btnLeft.onClick.RemoveListener(OnClickLeft);
        if (comps.btnRight != null) comps.btnRight.onClick.RemoveListener(OnClickRight);

        DestroyItem();
        ResManager.Instance.UnloadAsset<GameObject>(ItemPath);
        _itemPrefab = null;

        _def = default;
        _attributes = null;
        _source = null;
        _index = 0;
        _reuseSourceBackground = false;
        _returnToSource = null;
    }

    /// <summary>场上牌单张展示：没有来源列表，左右按钮不显示</summary>
    private void SetupSingle(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes, bool useInGameAttributes)
    {
        _source = null;
        _index = 0;
        _def = def;
        _attributes = attributes;
        _useInGameAttributes = useInGameAttributes;
        _reuseSourceBackground = false;
    }

    /// <summary>从展示牌列表打开：之后左右切换只改下标，数据仍从来源列表取</summary>
    private void SetupFromSource(ShowCardsPanel source, int index)
    {
        _source = source;
        _index = index;
        _reuseSourceBackground = true;
    }

    /// <summary>生成放大的牌（只建一次，之后只改绑定）</summary>
    private void CreateItem()
    {
        if (_item != null || _itemPrefab == null) return;

        _item = Instantiate(_itemPrefab, comps.imgCardContent.transform).GetComponent<ShowCardItem>();
        if (_item == null) return;

        // 列表用的锚点在左上角，放进面板要改成居中，并按倍数放大
        var rect = (RectTransform)_item.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(ItemScale, ItemScale, 1f);

        // 悬停倍率只改这一份实例，列表里的牌仍然是预制体的 1.4
        var scaleEffect = _item.GetComponent<UIScaleEffect>();
        if (scaleEffect != null) scaleEffect.SetHoveredScale(HoverScale);

        _item.OnActivate();   // 不在 ScrollList 里，手动补一次激活（悬停反馈靠它接线）
    }

    private void DestroyItem()
    {
        if (_item == null) return;

        _item.OnRecycle();
        Destroy(_item.gameObject);
        _item = null;
    }

    /// <summary>绑定当前要显示的那张牌（有来源列表时按下标取，属性口径沿用列表）</summary>
    private void RefreshCard()
    {
        if (_item == null) return;

        if (_source != null)
        {
            // onClick 传 null：详情里的牌不可再点
            if (_source.TryGetCard(_index, out var def, out var attributes))
                _item.Bind(def, attributes, _source.UseInGameAttributes, null);
            else
                _item.comps.modCard.Clear();
        }
        else if (_def.IsValid)
        {
            _item.Bind(_def, _attributes, _useInGameAttributes, null);
        }
        else
        {
            _item.comps.modCard.Clear();
        }

        RefreshNavButtons();
    }

    /// <summary>左右切换按钮：只有来源列表才有；第一张没左、最后一张没右</summary>
    private void RefreshNavButtons()
    {
        int count = _source != null ? _source.CardCount : 0;

        if (comps.btnLeft != null)
            comps.btnLeft.gameObject.SetActive(_source != null && _index > 0);

        if (comps.btnRight != null)
            comps.btnRight.gameObject.SetActive(_source != null && _index < count - 1);
    }

    private void OnClickLeft() => SwitchCard(-1);

    private void OnClickRight() => SwitchCard(1);

    /// <summary>左右翻看同一列表里的上一张 / 下一张</summary>
    private void SwitchCard(int direction)
    {
        if (_source == null || !IsOpen || IsTransitioning) return;

        int index = _index + direction;
        if (index < 0 || index >= _source.CardCount) return;

        _index = index;

        // 换牌了，旧的悬停提示作废（鼠标没移出时不会自动刷新）
        _item?.comps.modCard.HideTip();
        RefreshCard();
    }

    private void OnClickClose()
    {
        if (!IsOpen || IsTransitioning) return;

        // 淡出播完再回到来源面板（从场上牌打开时没有来源，直接关）
        var returnAction = _returnToSource;
        _returnToSource = null;
        CloseSelf(returnAction);
    }
}
