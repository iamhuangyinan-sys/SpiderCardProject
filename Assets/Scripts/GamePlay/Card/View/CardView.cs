using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单张卡牌的表现层（挂载在卡牌预制体上）
///
/// 正面完全由「花色 / 点数数据」拼出来，不再是整张牌图：
///   根节点 SpriteRenderer   牌底：正面 cardEmpty，反面 cardBack（反面只显示它）
///   Image                   图案：特殊牌读配表 Image，普通牌按「主花色 + 主点数」自动推导
///   Suit                    主花色 sprite（没有花色则隐藏）
///   Rank                    主点数 sprite（红 / 黑两套；-1 与无点数隐藏）
///   MutipleSuit / MutipleRank   多重花色 / 多重点数：把「其余」花色点数排成图标行（SpriteRowLayout）
///   Shadow / Glow               投影与发光（与花色点数无关）
///
/// 「主花色 / 主点数」= 显示用列表的第一个。显示用列表由 CardViewManager 刷新列时
/// 从列顶往下解析一遍后喂进来（多重花色 / 多重点数跟上面那张牌的实际值对齐），
/// 没喂（null）就用 CardData 自己的列表 —— 图案、花色、点数三者永远一致
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

    /// <summary>有多重花色时图案的 y（往下沉一点，给花色图标行让位）；没多重花色时回到 0</summary>
    private const float MultiSuitImageY = -0.2f;

    private GameObject _multiSuitGo;
    private GameObject _multiRankGo;
    private SpriteRenderer _multiSuitPlate;
    private SpriteRenderer _multiRankPlate;
    private SpriteRowLayout _multiSuitLayout;
    private SpriteRowLayout _multiRankLayout;

    /// <summary>图标行要喂的 sprite 列表（两个行共用，避免每次刷新都 new List）</summary>
    private readonly List<Sprite> _iconBuffer = new();

    /// <summary>
    /// 显示用花色 / 点数（多重花色 / 多重点数跟上面那张牌对齐之后的结果）。
    /// 由 CardViewManager 刷新列时喂进来；null = 用 CardData 自己的列表。
    /// 只影响显示，不改 CardData
    /// </summary>
    private IReadOnlyList<int> _displaySuits;
    private IReadOnlyList<int> _displayRanks;

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
        _multiSuitLayout = _multiSuitGo != null ? _multiSuitGo.GetComponent<SpriteRowLayout>() : null;
        _multiRankLayout = _multiRankGo != null ? _multiRankGo.GetComponent<SpriteRowLayout>() : null;

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

        // 其余所有子渲染器（多重花色 / 点数的图标槽位、以后再往预制体里加的图层）也按
        // 「自身相对牌底的初始 order」一起平移 —— 递归收集，调整预制体结构不用改代码
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            AddSortingTarget(renderer, baseOrder);
        }
    }

    private void AddSortingTarget(Renderer renderer, int baseOrder)
    {
        if (renderer == null) return;

        // 上面显式登记的那几个会被递归收集再扫一遍，这里去重
        foreach (var (existing, _) in _sortingTargets)
        {
            if (existing == renderer) return;
        }

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
        _displaySuits = null;
        _displayRanks = null;
    }

    /// <summary>设置世界坐标</summary>
    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }

    /// <summary>
    /// 喂「显示用」花色 / 点数（多重花色 / 多重点数的对齐结果，由 CardViewManager 刷新列时从列顶往下解析给出）。
    /// 传 null 表示用 CardData 自己的列表（临时牌 / 牌包里的牌）。只影响显示，不改 CardData
    /// </summary>
    public void SetDisplay(IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        if (_displaySuits == suits && _displayRanks == ranks) return;

        _displaySuits = suits;
        _displayRanks = ranks;

        if (data != null && data.isFaceUp) Refresh();
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

        if (data.isFaceUp)
        {
            // 显示用列表优先（没喂过则用原始列表）
            ApplyFace(data.id, _displaySuits ?? data.suits, _displayRanks ?? data.ranks);
        }
        else ApplyBack();
    }

    /// <summary>直接显示牌背（洗回动画的临时牌用，无需绑定数据）</summary>
    public void ShowBack()
    {
        ApplyBack();
    }

    /// <summary>直接显示指定牌的正面（洗入发牌堆等临时牌用，无需绑定数据）</summary>
    public void ShowFace(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        CardIdCodec.TryParse(cardId, out var suits, out var ranks);

        _displaySuits = null;   // 临时牌没有列上下文，用原始列表
        _displayRanks = null;
        ApplyFace(cardId, suits, ranks);
    }

    /// <summary>
    /// 画正面。花色 / 点数一律用传进来的列表：传进来的已经是「显示用列表」（对齐过），
    /// 复制牌复制过之后与配表也已经不一致，所以都不查 data
    /// </summary>
    private void ApplyFace(string cardId, IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        var res = CardResManager.Instance;

        SetSprite(_bgRenderer, res.GetBgSprite(false));

        // 图案：特殊牌读配表 Image；普通牌按「主花色 + 主点数」推（多重也推得出来）。
        // 传的就是显示用列表 → 对齐时图案一起换（N02_12345 压在 ♥5 下 → cardHearts4）；
        // 对齐后的素材不存在（画得少）则退回原始列表，不至于整张图都消失
        var face = res.GetFaceSprite(cardId, suits, ranks);
        if (face == null && IsConverged(suits, ranks))
        {
            face = res.GetFaceSprite(cardId, data.suits, data.ranks);
        }
        SetSprite(_imageRenderer, face);

        // 多重花色：图案下沉一点，给花色图标行让位
        SetImageY(suits != null && suits.Count > 1 ? MultiSuitImageY : 0f);

        ApplySuit(suits);
        ApplyRank(ranks, suits);
    }

    /// <summary>显示用列表和原始列表不是同一个（= 对齐过）。临时牌没有 data，按没对齐算</summary>
    private bool IsConverged(IReadOnlyList<int> suits, IReadOnlyList<int> ranks)
    {
        return data != null && (suits != data.suits || ranks != data.ranks);
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

    /// <summary>花色：第一个是主花色（显示 Suit 那个 sprite），其余的排进 MutipleSuit 图标行</summary>
    private void ApplySuit(IReadOnlyList<int> suits)
    {
        int count = suits?.Count ?? 0;
        var res = CardResManager.Instance;

        // 主花色
        SetSprite(_suitRenderer, count > 0 ? res.GetSuitSprite(suits[0]) : null);

        if (_multiSuitLayout == null)
        {
            if (_multiSuitGo != null) _multiSuitGo.SetActive(false);
            return;
        }

        // 其余花色 → 图标行（槽位数量由预制体决定，多出来的自动忽略）
        _iconBuffer.Clear();
        for (int i = 1; i < count; i++) _iconBuffer.Add(res.GetSuitSprite(suits[i]));

        if (_multiSuitGo != null) _multiSuitGo.SetActive(_iconBuffer.Count > 0);
        _multiSuitLayout.SetItems(_iconBuffer);
    }

    /// <summary>
    /// 点数：第一个「可显示」的点数作主点数（显示 sprite），其余的排进 MutipleRank 图标行。
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
        bool isRed = IsRedSuit(suits);
        SetSprite(_rankRenderer, visible.Count > 0
            ? CardResManager.Instance.GetRankSprite(visible[0], isRed)
            : null);

        if (_multiRankLayout == null)
        {
            if (_multiRankGo != null) _multiRankGo.SetActive(false);
            return;
        }

        // 其余点数 → 图标行（颜色跟主点数一致；槽位数量由预制体决定，多出来的自动忽略）
        _iconBuffer.Clear();
        for (int i = 1; i < visible.Count; i++) _iconBuffer.Add(CardResManager.Instance.GetRankSprite(visible[i], isRed));

        if (_multiRankGo != null) _multiRankGo.SetActive(_iconBuffer.Count > 0);
        _multiRankLayout.SetItems(_iconBuffer);
    }

    /// <summary>点数图案用红套还是黑套：红心 / 方块是红，其余（含没有花色）是黑</summary>
    private static bool IsRedSuit(IReadOnlyList<int> suits)
    {
        if (suits == null || suits.Count == 0) return false;

        int suit = suits[0];
        return suit == (int)E_CardSuitEnum.Hearts || suit == (int)E_CardSuitEnum.Diamonds;
    }

    /// <summary>只改图案节点的 y（x / z 保持预制体上设的）</summary>
    private void SetImageY(float y)
    {
        if (_imageRenderer == null) return;

        var pos = _imageRenderer.transform.localPosition;
        if (Mathf.Approximately(pos.y, y)) return;

        pos.y = y;
        _imageRenderer.transform.localPosition = pos;
    }

    private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null) return;

        renderer.sprite = sprite;
        renderer.gameObject.SetActive(sprite != null);
    }
}
