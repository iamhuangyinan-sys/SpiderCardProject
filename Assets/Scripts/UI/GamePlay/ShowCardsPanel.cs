using System;
using System.Collections.Generic;
using Framework.UI;

/// <summary>
/// 展示牌面板 —— 用一批牌定义在列表里铺开显示
/// 牌组查看、奖励预览等需要展示若干张牌的地方都用它
/// 也可进「选牌模式」：点选一张牌后立即关面板并回调（商店删牌等）
/// 遮挡场景输入（实现 ISceneInputBlockPanel）
/// </summary>
public partial class ShowCardsPanel : NormalPanel, ISceneInputBlockPanel
{
    /// <summary>列表里一格的内容（牌定义 + 要显示的属性）</summary>
    private struct CardShow
    {
        public CardDef def;
        public IReadOnlyList<E_CardAttributeEnum> attributes;
    }

    private readonly List<CardShow> _cards = new();

    /// <summary>选牌模式下的回调（null = 纯展示）</summary>
    private Action<CardDef> _onPick;

    /// <summary>打开面板并展示指定牌（牌组查看 / 奖励预览等只有牌定义的场合，无属性）</summary>
    public static void ShowCards(IReadOnlyList<CardDef> defs)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel != null) panel.SetDefs(defs);
    }

    /// <summary>打开面板并展示指定牌（发牌堆 / 弃牌堆等带属性的场合）</summary>
    public static void ShowCards(IReadOnlyList<CardData> cards)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel != null) panel.SetCards(cards);
    }

    /// <summary>
    /// 选牌模式：点选一张牌后立刻关面板，并把牌定义回调出去（商店删牌等）
    /// </summary>
    public static void PickCard(IReadOnlyList<CardDef> defs, Action<CardDef> onPick)
    {
        if (onPick == null) return;

        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel == null) return;

        panel._onPick = onPick;      // 先设回调，SetDefs 里的刷新才会挂上点击
        panel.SetDefs(defs);
    }

    protected override void OnOpen()
    {
        comps.btnClose.onClick.AddListener(OnClickClose);
        RefreshList();
    }

    protected override void OnShow()
    {
        RefreshList();
    }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnClose.onClick.RemoveListener(OnClickClose);
        _cards.Clear();
        _onPick = null;
    }

    /// <summary>设置要展示的场上牌（带属性）</summary>
    public void SetCards(IReadOnlyList<CardData> cards)
    {
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

    /// <summary>设置要展示的牌定义（无属性）</summary>
    public void SetDefs(IReadOnlyList<CardDef> defs)
    {
        _cards.Clear();

        if (defs != null)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                _cards.Add(new CardShow { def = defs[i], attributes = null });
            }
        }

        RefreshList();
    }

    /// <summary>刷新列表</summary>
    private void RefreshList()
    {
        comps.listCard.OnItemRender = (index, item) =>
        {
            var cell = item as ShowCardItem;
            if (cell == null) return;

            cell.Bind(_cards[index].def, _cards[index].attributes);

            // 选牌模式：点一下就算选中；纯展示模式保持不可点
            if (_onPick != null) cell.SetClickable(() => OnPickCard(index));
            else cell.SetClickable(null);
        };

        comps.listCard.ItemCount = _cards.Count;
    }

    /// <summary>选中一张牌：先把回调取出来，关掉面板后再回调（避免拿着已关闭面板的引用）</summary>
    private void OnPickCard(int index)
    {
        if (_onPick == null) return;
        if (index < 0 || index >= _cards.Count) return;

        var callback = _onPick;
        var def = _cards[index].def;

        _onPick = null;   // 防止连点触发两次
        CloseSelf();

        callback(def);
    }

    private void OnClickClose()
    {
        CloseSelf();
    }
}
