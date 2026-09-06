using UnityEngine;

/// <summary>
/// 单张卡牌的表现层（挂载在卡牌预制体上）
/// 根物体 SpriteRenderer 显示整张牌：正面 = 配表 Image，反面 = cardBack
/// 只负责"把数据画出来"，不修改数据；数据绑定是单向的（View → Data）
/// </summary>
public class CardView : MonoBehaviour
{
    /// <summary>绑定的卡牌数据（单向引用）</summary>
    public CardData data { get; private set; }

    /// <summary>所在列索引（-1 = 未分配）</summary>
    public int columnIndex { get; set; } = -1;

    private SpriteRenderer _rootRenderer;

    private void Awake()
    {
        _rootRenderer = GetComponent<SpriteRenderer>();
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

    /// <summary>设置渲染排序</summary>
    public void SetSortingOrder(int order)
    {
        if (_rootRenderer != null) _rootRenderer.sortingOrder = order;
    }

    /// <summary>根据数据刷新表现：正面整图 / 反面牌背</summary>
    public void Refresh()
    {
        if (_rootRenderer == null || data == null) return;

        if (data.isFaceUp)
        {
            var cfg = CardResManager.Instance.GetCardConfig(data.id);
            _rootRenderer.sprite = cfg != null
                ? CardResManager.Instance.GetCardImage(cfg.Image)
                : null;
        }
        else
        {
            _rootRenderer.sprite = CardResManager.Instance.GetBackSprite();
        }
    }
}
