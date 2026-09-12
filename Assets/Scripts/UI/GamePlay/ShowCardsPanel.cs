using System.Collections.Generic;
using Framework.UI;

/// <summary>
/// 展示牌面板 —— 用一批牌 id 在列表里铺开显示
/// 牌组查看、奖励预览等需要展示若干张牌的地方都用它
/// </summary>
public partial class ShowCardsPanel : FullScreenPanel
{
    private IReadOnlyList<CardData> _cards;

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
            if (cell != null) cell.Bind(_cards[index].id, _cards[index].isSunk);
        };

        comps.listCard.ItemCount = _cards != null ? _cards.Count : 0;
    }

    private void OnClickClose()
    {
        CloseSelf();
    }
}
