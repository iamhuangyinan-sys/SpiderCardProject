using TMPro;
using UnityEngine;

/// <summary>
/// 单张卡牌的表现层（挂载在卡牌预制体上）
/// 根物体 SpriteRenderer 显示牌底/牌背；子物体：Rank（点数 TMP）、Suit（花色）、Card（中央图案）
/// 只负责"把数据画出来"，不修改数据；数据绑定是单向的（View → Data）
/// </summary>
public class CardView : MonoBehaviour
{
    /// <summary>绑定的卡牌数据（单向引用）</summary>
    public CardData data { get; private set; }

    /// <summary>所在列索引（-1 = 未分配）</summary>
    public int columnIndex { get; set; } = -1;

    private TMP_Text _rankText;
    private SpriteRenderer _suitRenderer;
    private SpriteRenderer _cardRenderer;

    /// <summary>根物体 SpriteRenderer（牌底 / 牌背）</summary>
    private SpriteRenderer _rootRenderer;

    private void Awake()
    {
        _rootRenderer = GetComponent<SpriteRenderer>();
        _rankText = GetChildComponent<TMP_Text>("Rank");
        _suitRenderer = GetChildComponent<SpriteRenderer>("Suit");
        _cardRenderer = GetChildComponent<SpriteRenderer>("Card");
    }

    /// <summary>绑定数据并刷新表现</summary>
    public void Bind(CardData cardData)
    {
        data = cardData;
        Refresh();
    }

    /// <summary>解绑数据（回收前调用，避免持有脏数据）</summary>
    public void Unbind()
    {
        data = null;
        columnIndex = -1;
    }

    /// <summary>设置世界坐标</summary>
    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }

    /// <summary>设置渲染排序：牌底最低、图案居中、点数文本最高</summary>
    public void SetSortingOrder(int order)
    {
        if (_rootRenderer != null) _rootRenderer.sortingOrder = order;
        if (_suitRenderer != null) _suitRenderer.sortingOrder = order + 1;
        if (_cardRenderer != null) _cardRenderer.sortingOrder = order + 1;

        if (_rankText != null)
        {
            // TextMeshPro（世界空间）的排序在 MeshRenderer 上
            var rankRenderer = _rankText.GetComponent<MeshRenderer>();
            if (rankRenderer != null)
            {
                rankRenderer.sortingOrder = order + 2;
            }
        }
    }

    /// <summary>根据数据刷新表现：翻面、点数、花色符号、中央图案</summary>
    public void Refresh()
    {
        if (data == null) return;

        bool faceUp = data.isFaceUp;

        // 根物体 sprite：正面空牌底 / 反面牌背
        if (_rootRenderer != null)
        {
            _rootRenderer.sprite = faceUp
                ? CardResManager.Instance.GetEmptySprite()
                : CardResManager.Instance.GetBackSprite();
        }

        // 正面：点数、花色、图案
        if (_rankText != null) _rankText.gameObject.SetActive(faceUp);
        if (_suitRenderer != null) _suitRenderer.gameObject.SetActive(faceUp);
        if (_cardRenderer != null) _cardRenderer.gameObject.SetActive(faceUp);

        if (faceUp)
        {
            if (_rankText != null)
            {
                _rankText.text = RankToString(data.rank);
                _rankText.color = IsRedSuit(data.suit) ? Color.red : Color.black;
            }

            var suitSprite = CardResManager.Instance.GetSuitSprite(data.suit);
            if (_suitRenderer != null) _suitRenderer.sprite = suitSprite;
            if (_cardRenderer != null) _cardRenderer.sprite = suitSprite; // 中央图案暂用花色符号
        }
    }

    /// <summary>点数转字符串：1→A，11→J，12→Q，13→K，其余为数字</summary>
    private static string RankToString(int rank)
    {
        switch (rank)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return rank.ToString();
        }
    }

    /// <summary>红桃/方块为红色，黑桃/梅花为黑色</summary>
    private static bool IsRedSuit(E_CardSuitEnum suit)
    {
        return suit == E_CardSuitEnum.Hearts || suit == E_CardSuitEnum.Diamonds;
    }

    /// <summary>获取子物体上的组件</summary>
    private T GetChildComponent<T>(string name) where T : Component
    {
        var child = transform.Find(name);
        return child != null ? child.GetComponent<T>() : null;
    }
}
