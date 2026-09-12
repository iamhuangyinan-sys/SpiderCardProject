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
    private SpriteRenderer _glowRenderer;
    private SpriteRenderer _shadowRenderer;
    private GameObject _glowGo;
    private GameObject _shadowGo;
    private Vector3 _shadowInitLocalPos;

    // ===== 供 CardAnimationHelper 访问的引用 =====
    public SpriteRenderer RootRenderer => _rootRenderer;
    public SpriteRenderer GlowRenderer => _glowRenderer;
    public SpriteRenderer ShadowRenderer => _shadowRenderer;
    public GameObject GlowGo => _glowGo;
    public GameObject ShadowGo => _shadowGo;
    public Vector3 ShadowInitLocalPos => _shadowInitLocalPos;

    private void Awake()
    {
        _rootRenderer = GetComponent<SpriteRenderer>();

        var glow = transform.Find("Glow");
        if (glow != null)
        {
            _glowGo = glow.gameObject;
            _glowRenderer = glow.GetComponent<SpriteRenderer>();
        }

        var shadow = transform.Find("Shadow");
        if (shadow != null)
        {
            _shadowGo = shadow.gameObject;
            _shadowRenderer = shadow.GetComponent<SpriteRenderer>();
            _shadowInitLocalPos = shadow.localPosition;   // 记录初始位置（pivot 在顶部时为 (0, 0.95, 0)）
        }

        // 默认隐藏发光与投影（alpha 归零，便于淡入）
        SetRendererAlpha(_glowRenderer, 0f);
        SetRendererAlpha(_shadowRenderer, 0f);
        if (_glowGo != null) _glowGo.SetActive(false);
        if (_shadowGo != null) _shadowGo.SetActive(false);
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null) return;
        var c = renderer.color;
        c.a = alpha;
        renderer.color = c;
    }

    /// <summary>绑定数据并刷新表现</summary>
    public void Bind(CardData cardData)
    {
        data = cardData;
        transform.rotation = Quaternion.identity;   // 重置旋转，避免上次翻转被打断留下的斜角

        // 清理残留动画 + 恢复发光投影状态（动画统一由 CardAnimationHelper 管理）
        CardAnimationHelper.Reset(this);

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

    /// <summary>设置渲染排序（牌本体 order，发光 +1，投影 -1）</summary>
    public void SetSortingOrder(int order)
    {
        if (_rootRenderer != null) _rootRenderer.sortingOrder = order;
        if (_glowRenderer != null) _glowRenderer.sortingOrder = order + 1;
        if (_shadowRenderer != null) _shadowRenderer.sortingOrder = order - 1;
    }

    /// <summary>当前渲染排序</summary>
    public int CurrentSortingOrder => _rootRenderer != null ? _rootRenderer.sortingOrder : 0;

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

    /// <summary>直接显示牌背（洗回动画的临时牌用，无需绑定数据）</summary>
    public void ShowBack()
    {
        if (_rootRenderer != null)
        {
            _rootRenderer.sprite = CardResManager.Instance.GetBackSprite();
        }
    }

    /// <summary>直接显示指定牌的正面（洗入发牌堆等临时牌用，无需绑定数据）</summary>
    public void ShowFace(string cardId)
    {
        if (_rootRenderer == null || string.IsNullOrEmpty(cardId)) return;

        var cfg = CardResManager.Instance.GetCardConfig(cardId);
        _rootRenderer.sprite = cfg != null
            ? CardResManager.Instance.GetCardImage(cfg.Image)
            : null;
    }
}
