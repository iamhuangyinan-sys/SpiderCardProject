using System;
using System.Collections.Generic;
using Framework.UI;

/// <summary>
/// 展示牌面板 —— 用一批牌 id 在列表里铺开显示
/// 牌组查看、奖励预览等需要展示若干张牌的地方都用它
/// 也可进「选牌模式」：点选一张牌后立即关面板并回调（商店删牌等）
/// 遮挡场景输入（实现 ISceneInputBlockPanel）
/// </summary>
public partial class ShowCardsPanel : NormalPanel, ISceneInputBlockPanel
{
    private IReadOnlyList<CardData> _cards;

    /// <summary>选牌模式下的回调（null = 纯展示）</summary>
    private Action<string> _onPick;

    /// <summary>打开面板并展示指定牌（牌组查看等只有牌 id 的场合，无沉底标记）</summary>
    public static void ShowCards(IReadOnlyList<string> cardIds)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel != null) panel.SetCardIds(cardIds);
    }

    /// <summary>打开面板并展示指定牌（发牌堆 / 弃牌堆等带沉底标记的场合）</summary>
    public static void ShowCards(IReadOnlyList<CardData> cards)
    {
        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel != null) panel.SetCards(cards);
    }

    /// <summary>
    /// 选牌模式：点选一张牌后立刻关面板，并把牌 id 回调出去（商店删牌等）
    /// </summary>
    public static void PickCard(IReadOnlyList<string> cardIds, Action<string> onPick)
    {
        if (onPick == null) return;

        var panel = UIManager.Instance.Show<ShowCardsPanel>();
        if (panel == null) return;

        panel._onPick = onPick;      // 先设回调，SetCardIds 里的刷新才会挂上点击
        panel.SetCardIds(cardIds);
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
        _cards = null;
        _onPick = null;
    }

    /// <summary>设置要展示的牌并刷新（带沉底标记）</summary>
    public void SetCards(IReadOnlyList<CardData> cards)
    {
        _cards = cards;
        RefreshList();
    }

    /// <summary>设置要展示的牌并刷新（只有牌 id，无沉底标记）</summary>
    public void SetCardIds(IReadOnlyList<string> cardIds)
    {
        var list = new List<CardData>(cardIds != null ? cardIds.Count : 0);
        if (cardIds != null)
        {
            foreach (var id in cardIds)
            {
                list.Add(new CardData { id = id });
            }
        }

        SetCards(list);
    }

    /// <summary>刷新列表</summary>
    private void RefreshList()
    {
        comps.listCard.OnItemRender = (index, item) =>
        {
            var cell = item as ShowCardItem;
            if (cell == null) return;

            cell.Bind(_cards[index].id, _cards[index].isSunk);

            // 选牌模式：点一下就算选中；纯展示模式保持不可点
            if (_onPick != null) cell.SetClickable(() => OnPickCard(index));
            else cell.SetClickable(null);
        };

        comps.listCard.ItemCount = _cards != null ? _cards.Count : 0;
    }

    /// <summary>选中一张牌：先把回调取出来，关掉面板后再回调（避免拿着已关闭面板的引用）</summary>
    private void OnPickCard(int index)
    {
        if (_onPick == null || _cards == null) return;
        if (index < 0 || index >= _cards.Count) return;

        var callback = _onPick;
        string cardId = _cards[index].id;

        _onPick = null;   // 防止连点触发两次
        CloseSelf();

        callback(cardId);
    }

    private void OnClickClose()
    {
        CloseSelf();
    }
}
