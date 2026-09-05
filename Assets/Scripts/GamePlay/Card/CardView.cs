using UnityEngine;

/// <summary>
/// 单张卡牌的表现层（挂载在卡牌预制体上）
/// 只负责"把数据画出来"，不修改数据；数据绑定是单向的（View → Data）
/// </summary>
public class CardView : MonoBehaviour
{
    /// <summary>绑定的卡牌数据（单向引用）</summary>
    public CardData data { get; private set; }

    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
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
    }

    /// <summary>设置世界坐标</summary>
    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }

    /// <summary>设置渲染排序（同列内越靠上的牌排序值越大，渲染越靠前）</summary>
    public void SetSortingOrder(int order)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingOrder = order;
        }
    }

    /// <summary>根据数据刷新表现：从 CardResManager 取正面/背面贴图</summary>
    public void Refresh()
    {
        if (_spriteRenderer == null || data == null) return;

        _spriteRenderer.sprite = data.isFaceUp
            ? CardResManager.Instance.GetFaceSprite(data.suit, data.rank)
            : CardResManager.Instance.GetBackSprite();
    }
}
