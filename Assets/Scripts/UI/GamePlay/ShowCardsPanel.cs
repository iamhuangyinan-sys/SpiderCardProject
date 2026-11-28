using System;
using System.Collections.Generic;
using Framework.Event;
using Framework.UI;

/// <summary>
/// 展示牌面板 —— 用一批牌定义在列表里铺开显示
/// 牌组查看、奖励预览等需要展示若干张牌的地方都用它
/// 也可进「选牌模式」：回调成功后关闭面板（商店删牌等）
/// 黑底由 PanelBackdrop 独立保存，打开单牌详情时留给详情面板复用
/// 遮挡场景输入（实现 ISceneInputBlockPanel）
/// </summary>
public partial class ShowCardsPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;

    /// <summary>黑底交给单牌详情复用时，本面板退场不收黑底</summary>
    private bool _keepBackground;

    private PanelBackdrop Backdrop => GetComponent<PanelBackdrop>();

    // 排序栏顶部 20 + 高度 60 + 下方间距 40，扣除列表原有的 50 留白。
    private const float SortHeaderSpace = 70f;

    private enum SortMethod { Acquisition, Suit, Rank, Special }

    /// <summary>列表里一格的内容（牌定义 + 要显示的属性）</summary>
    private struct CardShow
    {
        public CardDef def;
        public IReadOnlyList<E_CardAttributeEnum> attributes;
        public int originalIndex;
    }

    private readonly List<CardShow> _cards = new();
    private bool _useInGameAttributes;

    /// <summary>选牌模式下的回调（null = 纯展示）</summary>
    private Func<CardDef, bool> _onPick;

    /// <summary>展示本局牌组并启用排序</summary>
    public static void ShowDeck(IReadOnlyList<CardDef> defs, string info)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel == null) return;
        panel._onPick = null;
        panel.SetInfo(info);
        panel.SetDefs(defs, showSort: true);
    }

    /// <summary>打开面板并展示指定牌定义及其属性</summary>
    public static void ShowCards(IReadOnlyList<CardDef> defs, string info)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel == null) return;
        panel._onPick = null;
        panel.SetInfo(info);
        panel.SetDefs(defs);
    }

    /// <summary>打开面板并展示指定牌（发牌堆 / 弃牌堆等带属性的场合）</summary>
    public static void ShowCards(IReadOnlyList<CardData> cards, string info)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel == null) return;
        panel._onPick = null;
        panel.SetInfo(info);
        panel.SetCards(cards);
    }

    /// <summary>
    /// 选牌模式：回调返回 true 时关闭面板
    /// </summary>
    public static void PickCard(IReadOnlyList<CardDef> defs, string info, Func<CardDef, bool> onPick)
    {
        if (onPick == null) return;

        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel == null) return;

        panel._onPick = onPick;      // 先设回调，SetDefs 里的刷新才会挂上点击
        panel.SetInfo(info);
        panel.SetDefs(defs);
    }

    private void SetInfo(string info)
    {
        if (comps.txtInfo != null) comps.txtInfo.text = info;
    }

    protected override void OnOpen()
    {
        comps.btnClose.onClick.AddListener(OnClickClose);
        RefreshSortOptions();
        comps.drpSortMethod.onValueChanged.AddListener(OnSortChanged);
        EventManager.Instance.AddListener(E_EventEnum.OnLanguageChanged, RefreshSortOptions);
        _keepBackground = false;
        Backdrop.Show(true);      // 黑底独立淡入（内容走面板淡入）
        RefreshList();
    }

    protected override void OnShow()
    {
        _keepBackground = false;
        Backdrop.Show(true);      // 从详情返回时黑底已经在，内部会跳过重复淡入
        RefreshList();
    }

    protected override void OnHide() { }

    protected override bool OnPanelClosing()
    {
        if (comps.drpSortMethod.IsExpanded) comps.drpSortMethod.Hide();
        // 交给详情复用时保留黑底，否则和内容一起淡出
        if (!_keepBackground) Backdrop.Hide(true);
        return base.OnPanelClosing();
    }

    protected override void OnClose()
    {
        comps.btnClose.onClick.RemoveListener(OnClickClose);
        comps.drpSortMethod.onValueChanged.RemoveListener(OnSortChanged);
        EventManager.Instance.RemoveListener(E_EventEnum.OnLanguageChanged, RefreshSortOptions);
        _cards.Clear();
        _onPick = null;
        _keepBackground = false;
    }

    /// <summary>打开单牌详情前调用：本面板隐藏时保留黑底给详情面板用</summary>
    public void KeepBackground() => _keepBackground = true;

    /// <summary>设置要展示的场上牌（带属性）</summary>
    public void SetCards(IReadOnlyList<CardData> cards)
    {
        SetSortVisible(false);
        _useInGameAttributes = true;
        _cards.Clear();

        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                _cards.Add(new CardShow { def = cards[i].def, attributes = cards[i].attributes });
            }
        }

        RefreshList();
    }

    /// <summary>设置要展示的牌定义及其属性</summary>
    public void SetDefs(IReadOnlyList<CardDef> defs) => SetDefs(defs, showSort: false);

    private void SetDefs(IReadOnlyList<CardDef> defs, bool showSort)
    {
        SetSortVisible(showSort);
        _useInGameAttributes = false;
        _cards.Clear();

        if (defs != null)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                _cards.Add(new CardShow { def = defs[i], attributes = defs[i].attributes, originalIndex = i });
            }
        }

        RefreshList();
    }

    private void SetSortVisible(bool visible)
    {
        var header = comps.imgSort.gameObject;
        bool wasVisible = header.activeSelf;
        header.SetActive(visible);
        comps.listCard.SetHeaderSpace(visible ? SortHeaderSpace : 0f);
        if (visible) comps.drpSortMethod.SetValueWithoutNotify((int)SortMethod.Acquisition);
        if (visible || wasVisible) comps.listCard.ScrollToTop();
    }

    private void RefreshSortOptions()
    {
        var dropdown = comps.drpSortMethod;
        int selection = dropdown.value;
        if (dropdown.IsExpanded) dropdown.Hide();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>
        {
            LanguageHelper.Get("ui_deck_sort_acquisition"),
            LanguageHelper.Get("ui_deck_sort_suit"),
            LanguageHelper.Get("ui_deck_sort_rank"),
            LanguageHelper.Get("ui_deck_sort_special")
        });
        dropdown.SetValueWithoutNotify(selection);
    }

    private void OnSortChanged(int index)
    {
        if (!IsOpen || !comps.imgSort.gameObject.activeSelf) return;
        var method = (SortMethod)index;
        _cards.Sort((a, b) => CompareCards(a, b, method));
        comps.listCard.ScrollToTop();
        RefreshList();
    }

    private static int CompareCards(CardShow a, CardShow b, SortMethod method)
    {
        int order = 0;
        switch (method)
        {
            case SortMethod.Suit:
                order = GetSortSuit(a.def).CompareTo(GetSortSuit(b.def));
                if (order == 0) order = GetSortRank(a.def).CompareTo(GetSortRank(b.def));
                break;
            case SortMethod.Rank:
                order = GetSortRank(a.def).CompareTo(GetSortRank(b.def));
                if (order == 0) order = GetSortSuit(a.def).CompareTo(GetSortSuit(b.def));
                break;
            case SortMethod.Special:
                order = IsSpecial(b.def).CompareTo(IsSpecial(a.def));
                break;
        }

        return order != 0 ? order : a.originalIndex.CompareTo(b.originalIndex);
    }

    private static int GetSortSuit(CardDef def) => def.suits?.Count > 0 ? def.suits[0] : int.MaxValue;

    private static int GetSortRank(CardDef def)
    {
        int? rank = CardResManager.GetMainRank(def.ranks);
        // 普通点数 1–13，其后依次是蜘蛛、仅万能、无点数。
        if (rank == CardRankConst.Ignore) return 14;
        if (rank.HasValue) return rank.Value;
        return def.ranks?.Contains(CardRankConst.Wild) == true ? 15 : 16;
    }

    private static bool IsSpecial(CardDef def) =>
        def.attributes?.Count > 0 || def.HasSkill || def.flipSkill != E_FlipSkillEnum.None
        || def.ranks?.Contains(CardRankConst.Ignore) == true || def.ranks?.Contains(CardRankConst.Wild) == true;

    /// <summary>详情面板翻页用：当前可展示的牌数量</summary>
    public int CardCount => _cards.Count;

    /// <summary>详情面板翻页用：列表是否按「局内属性」口径展示</summary>
    public bool UseInGameAttributes => _useInGameAttributes;

    /// <summary>详情面板翻页用：取第 index 张牌的定义与属性</summary>
    public bool TryGetCard(int index, out CardDef def, out IReadOnlyList<E_CardAttributeEnum> attributes)
    {
        def = default;
        attributes = null;

        if (index < 0 || index >= _cards.Count) return false;

        def = _cards[index].def;
        attributes = _cards[index].attributes;
        return true;
    }

    /// <summary>刷新列表</summary>
    private void RefreshList()
    {
        comps.listCard.OnItemRender = (index, item) =>
        {
            var cell = item as ShowCardItem;
            if (cell == null) return;

            var card = _cards[index];

            // 选牌模式：点一下就算选中；纯展示模式：点一下打开单张详情
            if (_onPick != null)
            {
                cell.Bind(card.def, card.attributes, _useInGameAttributes, () => OnPickCard(index));
            }
            else
            {
                cell.Bind(card.def, card.attributes, _useInGameAttributes,
                    () => ShowOneCardPanel.OpenFrom(this, index));
            }
        };

        comps.listCard.ItemCount = _cards.Count;
    }

    /// <summary>选中一张牌，操作成功后关闭面板</summary>
    private void OnPickCard(int index)
    {
        if (_onPick == null) return;
        if (index < 0 || index >= _cards.Count) return;

        var callback = _onPick;
        var def = _cards[index].def;

        _onPick = null;   // 防止回调执行期间重复触发
        if (callback(def)) CloseSelf();
        else _onPick = callback;
    }

    private void OnClickClose()
    {
        CloseSelf();
    }
}
