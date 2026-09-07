using DG.Tweening;
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

    /// <summary>发光目标透明度</summary>
    private const float GlowAlpha = 0.9f;

    /// <summary>投影目标透明度</summary>
    private const float ShadowAlpha = 0.8f;

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

        // 池复用残留：确保发光投影隐藏、投影缩放复位
        if (_glowRenderer != null) _glowRenderer.DOKill();
        if (_shadowRenderer != null) _shadowRenderer.DOKill();
        if (_glowGo != null) _glowGo.SetActive(false);
        if (_shadowGo != null)
        {
            _shadowGo.transform.localScale = Vector3.one;
            _shadowGo.SetActive(false);
        }

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

    /// <summary>拖起时显示发光（淡入）</summary>
    public void ShowGlow()
    {
        if (_glowRenderer == null) return;
        _glowGo.SetActive(true);
        _glowRenderer.DOFade(GlowAlpha, 0.15f);
    }

    /// <summary>拖起时显示投影（淡入）</summary>
    public void ShowShadow()
    {
        if (_shadowRenderer == null) return;
        _shadowGo.SetActive(true);
        _shadowRenderer.DOFade(ShadowAlpha, 0.15f);
    }

    /// <summary>拖起时同时显示发光与投影（淡入），投影按牌串实际高度拉伸</summary>
    public void ShowGlowShadow(int cardCount)
    {
        ShowGlow();
        ShowShadow();

        // 投影拉伸：覆盖整串牌的高度（牌高 + (N-1) × 正面露出间距）
        float cardHeight = _rootRenderer != null ? _rootRenderer.bounds.size.y : 1f;
        float stretch = 1f + (cardCount - 1) * CardViewManager.FaceUpSpacing / cardHeight;
        if (_shadowGo != null)
        {
            _shadowGo.transform.localScale = new Vector3(1f, stretch, 1f);
        }
    }

    /// <summary>放下时隐藏发光与投影（淡出），淡出完成后恢复投影缩放</summary>
    public void HideGlowShadow()
    {
        if (_glowRenderer != null)
        {
            _glowRenderer.DOFade(0f, 0.1f).OnComplete(() => _glowGo.SetActive(false));
        }
        if (_shadowRenderer != null)
        {
            _shadowRenderer.DOFade(0f, 0.1f).OnComplete(() =>
            {
                _shadowGo.SetActive(false);
                _shadowGo.transform.localScale = Vector3.one;  // 淡出完成后恢复原始大小
            });
        }
    }
}
