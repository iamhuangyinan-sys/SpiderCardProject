using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 单张卡牌的表现层（挂载在卡牌预制体上）
///
/// 正面完全由「花色 / 点数数据」拼出来，不再是整张牌图：
///   根节点 SpriteRenderer   牌底：正面 cardEmpty，反面 cardBack（反面只显示它）
///   Image                   图案：配表 Image（唯一读配表的素材；留空则不显示）
///   Suit                    主花色 sprite（没有花色则隐藏）
///   Rank                    主点数 sprite（红 / 黑两套；-1 与无点数隐藏）
///   MutipleSuit / MutipleRank   多重花色 / 多重点数：显示「其余」花色点数（文本）
///   Shadow / Glow               投影与发光（与花色点数无关）
///
/// 只负责"把数据画出来"，不修改数据；数据绑定是单向的（View → Data）
/// </summary>
public class CardView : MonoBehaviour
{
    /// <summary>绑定的卡牌数据（单向引用）</summary>
    public CardData data { get; private set; }

    /// <summary>所在列索引（-1 = 未分配）</summary>
    public int columnIndex { get; set; } = -1;

    // ===== 牌面 =====
    private SpriteRenderer _bgRenderer;
    private SpriteRenderer _imageRenderer;
    private SpriteRenderer _suitRenderer;
    private SpriteRenderer _rankRenderer;
    private Color _bgBaseColor = Color.white;

    private GameObject _multiSuitGo;
    private GameObject _multiRankGo;
    private SpriteRenderer _multiSuitPlate;
    private SpriteRenderer _multiRankPlate;
    private TMP_Text _multiSuitText;
    private TMP_Text _multiRankText;

    // ===== 发光 / 投影 =====
    private SpriteRenderer _glowRenderer;
    private SpriteRenderer _shadowRenderer;
    private GameObject _glowGo;
    private GameObject _shadowGo;
    private Vector3 _shadowInitLocalPos;
    private Vector2 _shadowBaseSize;

    /// <summary>
    /// 要跟随卡牌渲染序一起平移的所有渲染器（牌面 + 发光投影 + 多重文本），
    /// offset = Awake 时记录的「相对 Bg 的初始渲染序」—— 相对层级完全交给预制体决定
    /// </summary>
    private readonly List<(Renderer renderer, int offset)> _sortingTargets = new();

    // ===== 供 CardAnimationHelper 访问的引用 =====
    /// <summary>牌底渲染器（"不可拖"变灰提示只改它）</summary>
    public SpriteRenderer BgRenderer => _bgRenderer;

    /// <summary>Bg 的初始颜色（变灰提示恢复时用它，不要硬编码白色）</summary>
    public Color BgBaseColor => _bgBaseColor;

    public SpriteRenderer GlowRenderer => _glowRenderer;
    public SpriteRenderer ShadowRenderer => _shadowRenderer;
    public GameObject GlowGo => _glowGo;
    public GameObject ShadowGo => _shadowGo;
    public Vector3 ShadowInitLocalPos => _shadowInitLocalPos;

    /// <summary>投影未被拉伸时的尺寸（世界单位）—— 拉牌串投影时以它为基准加长</summary>
    public Vector2 ShadowBaseSize => _shadowBaseSize;

    // ==================== 初始化 ====================

    private void Awake()
    {
        // 牌底就是根节点自己的 SpriteRenderer（旧的招取 / 层级逻辑都能继续用）
        _bgRenderer = GetComponent<SpriteRenderer>();

        _imageRenderer = FindRenderer("Image");
        _suitRenderer = FindRenderer("Suit");
        _rankRenderer = FindRenderer("Rank");

        _multiSuitGo = FindGo("MutipleSuit");
        _multiRankGo = FindGo("MutipleRank");
        _multiSuitPlate = _multiSuitGo != null ? _multiSuitGo.GetComponent<SpriteRenderer>() : null;
        _multiRankPlate = _multiRankGo != null ? _multiRankGo.GetComponent<SpriteRenderer>() : null;
        _multiSuitText = _multiSuitGo != null ? _multiSuitGo.GetComponentInChildren<TMP_Text>(true) : null;
        _multiRankText = _multiRankGo != null ? _multiRankGo.GetComponentInChildren<TMP_Text>(true) : null;

        _glowGo = FindGo("Glow");
        _glowRenderer = _glowGo != null ? _glowGo.GetComponent<SpriteRenderer>() : null;

        _shadowGo = FindGo("Shadow");
        _shadowRenderer = _shadowGo != null ? _shadowGo.GetComponent<SpriteRenderer>() : null;

        if (_shadowGo != null)
        {
            _shadowInitLocalPos = _shadowGo.transform.localPosition;   // 记录初始位置（pivot 在顶部时为 (0, 0.95, 0)）

            // 投影要按牌串长度拉伸：必须用 Sliced 绘制模式，九宫格的角才会保持原尺寸；
            // Simple 模式下 border 完全无效，整张图（含四角）会被一起拉变形。
            // 拉伸走 SpriteRenderer.size，不要动 localScale。
            if (_shadowRenderer != null)
            {
                _shadowRenderer.drawMode = SpriteDrawMode.Sliced;
                _shadowBaseSize = _shadowRenderer.size;

                // 兜底：预制体上 size 没设时用精灵自然尺寸
                if (_shadowBaseSize.y <= 0f && _shadowRenderer.sprite != null)
                {
                    _shadowBaseSize = _shadowRenderer.sprite.bounds.size;
                }
            }
        }

        if (_bgRenderer != null) _bgBaseColor = _bgRenderer.color;

        CacheSortingOffsets();

        // 默认隐藏发光与投影（alpha 归零，便于淡入）
        SetRendererAlpha(_glowRenderer, 0f);
        SetRendererAlpha(_shadowRenderer, 0f);
        if (_glowGo != null) _glowGo.SetActive(false);
        if (_shadowGo != null) _shadowGo.SetActive(false);
    }

    /// <summary>记录各渲染器「相对 Bg」的初始渲染序偏移，之后整张牌一起平移</summary>
    private void CacheSortingOffsets()
    {
        int baseOrder = _bgRenderer != null ? _bgRenderer.sortingOrder : 0;

        _sortingTargets.Clear();
        AddSortingTarget(_bgRenderer, baseOrder);
        AddSortingTarget(_imageRenderer, baseOrder);
        AddSortingTarget(_suitRenderer, baseOrder);
        AddSortingTarget(_rankRenderer, baseOrder);
        AddSortingTarget(_multiSuitPlate, baseOrder);
        AddSortingTarget(_multiRankPlate, baseOrder);
        AddSortingTarget(_glowRenderer, baseOrder);
        AddSortingTarget(_shadowRenderer, baseOrder);

        // 多重文本是世界空间 TMP，靠自己的 MeshRenderer 参与排序，也要跟着平移
        AddSortingTarget(_multiSuitText != null ? _multiSuitText.GetComponent<Renderer>() : null, baseOrder);
        AddSortingTarget(_multiRankText != null ? _multiRankText.GetComponent<Renderer>() : null, baseOrder);
    }

    private void AddSortingTarget(Renderer renderer, int baseOrder)
    {
        if (renderer == null) return;

        _sortingTargets.Add((renderer, renderer.sortingOrder - baseOrder));
    }

    /// <summary>按路径找节点；路径找不到时退化成按名字找（容忍预制体结构调整）</summary>
    private Transform FindDeep(string path)
    {
        var t = transform.Find(path);
        if (t != null) return t;

        int slash = path.LastIndexOf('/');
        return slash >= 0 ? transform.Find(path.Substring(slash + 1)) : null;
    }

    private GameObject FindGo(string path)
    {
        var t = FindDeep(path);
        return t != null ? t.gameObject : null;
    }

    private SpriteRenderer FindRenderer(string path)
    {
        var t = FindDeep(path);
        return t != null ? t.GetComponent<SpriteRenderer>() : null;
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null) return;
        var c = renderer.color;
        c.a = alpha;
        renderer.color = c;
    }

    // ==================== 绑定 / 位置 / 排序 ====================

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

    /// <summary>设置渲染排序（整张牌所有层一起平移，相对层级由预制体决定）</summary>
    public void SetSortingOrder(int order)
    {
        foreach (var (renderer, offset) in _sortingTargets)
        {
            if (renderer != null) renderer.sortingOrder = order + offset;
        }
    }

    /// <summary>当前渲染排序（以 Bg 为基准）</summary>
    public int CurrentSortingOrder => _bgRenderer != null ? _bgRenderer.sortingOrder : 0;

    // ==================== 刷新表现 ====================

    /// <summary>根据数据刷新表现：正面 = 牌底 + 图案 + 花色 + 点数，反面 = 牌背</summary>
    public void Refresh()
    {
        if (data == null) return;

        if (data.isFaceUp) ApplyFace(data.id, data.suits, data.ranks);
        else ApplyBack();
    }

    /// <summary>直接显示牌背（洗回动画的临时牌用，无需绑定数据）</summary>
    public void ShowBack()
    {
        ApplyBack();
    }

    /// <summary>直接显示指定牌的正面（洗入发牌堆等临时牌用，无需绑定数据 —— 这种才需要查配表）</summary>
    public void ShowFace(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        var cfg = CardResManager.Instance.GetCardConfig(cardId);
        ApplyFace(cardId, cfg != null ? cfg.Suit : null, cfg != null ? cfg.Rank : null);
    }

    /// <summary>
    /// 画正面。花色 / 点数一律用传进来的列表：复制牌复制过之后与配表已经不一致，
    /// 所以只有「图案」按 id 查表
    /// </summary>
    private void ApplyFace(string cardId, IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var res = CardResManager.Instance;

        // 牌底 + 图案（图案留空就不显示）
        SetSprite(_bgRenderer, res.GetBgSprite(false));

        var cfg = string.IsNullOrEmpty(cardId) ? null : res.GetCardConfig(cardId);
        string imageName = cfg != null ? cfg.Image : null;
        SetSprite(_imageRenderer, string.IsNullOrEmpty(imageName) ? null : res.GetCardImage(imageName));

        ApplySuit(suits);
        ApplyRank(ranks, suits);
    }

    /// <summary>画反面：只显示牌背，其余全部隐藏</summary>
    private void ApplyBack()
    {
        SetSprite(_bgRenderer, CardResManager.Instance.GetBgSprite(true));

        SetSprite(_imageRenderer, null);
        SetSprite(_suitRenderer, null);
        SetSprite(_rankRenderer, null);
        if (_multiSuitGo != null) _multiSuitGo.SetActive(false);
        if (_multiRankGo != null) _multiRankGo.SetActive(false);
    }

    /// <summary>花色：第一个是主花色（显示 sprite），其余的写进 MutipleSuit 文本</summary>
    private void ApplySuit(IReadOnlyList<int> suits)
    {
        int count = suits?.Count ?? 0;

        // 主花色
        SetSprite(_suitRenderer, count > 0 ? CardResManager.Instance.GetSuitSprite(suits[0]) : null);

        // 其余花色（文本；颜色用预制体里定好的，代码不动）
        bool hasMore = count > 1;
        if (_multiSuitGo != null) _multiSuitGo.SetActive(hasMore);
        if (hasMore && _multiSuitText != null)
        {
            _multiSuitText.text = JoinFrom(suits, 1, CardResManager.GetSuitSymbol);
        }
    }

    /// <summary>
    /// 点数：第一个「可显示」的点数作主点数（显示 sprite），其余的写进 MutipleRank 文本。
    /// -1（万能放下）不显示，跟没有这个点数一样；-2 显示成 S
    /// </summary>
    private void ApplyRank(IReadOnlyList<int> ranks, IReadOnlyList<int> suits)
    {
        // 过滤掉 -1（它不是点数）
        var visible = new List<int>();
        if (ranks != null)
        {
            foreach (int r in ranks)
            {
                if (r != CardRankConst.Wild) visible.Add(r);
            }
        }

        // 主点数：黑桃 / 梅花用黑套，红心 / 方块用红套；没有花色按黑
        SetSprite(_rankRenderer, visible.Count > 0
            ? CardResManager.Instance.GetRankSprite(visible[0], IsRedSuit(suits))
            : null);

        // 其余点数（文本）
        bool hasMore = visible.Count > 1;
        if (_multiRankGo != null) _multiRankGo.SetActive(hasMore);
        if (hasMore && _multiRankText != null)
        {
            _multiRankText.text = JoinFrom(visible, 1, CardResManager.GetRankName);
        }
    }

    /// <summary>点数图案用红套还是黑套：红心 / 方块是红，其余（含没有花色）是黑</summary>
    private static bool IsRedSuit(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0) return false;

        int suit = suits[0];
        return suit == (int)E_CardSuitEnum.Hearts || suit == (int)E_CardSuitEnum.Diamonds;
    }

    /// <summary>把列表 start 之后的项用 " | " 拼起来（toText 负责单项的显示名）</summary>
    private static string JoinFrom<T>(IReadOnlyList<T> list, int start, Func<T, string> toText)
    {
        var sb = new StringBuilder();

        for (int i = start; i < list.Count; i++)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(toText(list[i]));
        }

        return sb.ToString();
    }

    private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null) return;

        renderer.sprite = sprite;
        renderer.gameObject.SetActive(sprite != null);
    }
}
