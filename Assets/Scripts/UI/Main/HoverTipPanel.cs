using System.Collections.Generic;
using Framework.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 悬停提示框（通用触发式提示）—— 由若干条 HoverTipItemModule（每条一个框）纵向堆叠，
/// 内容自适应尺寸、方向自适应摆放
///
/// 用法：业务层不要直接 Show，统一走 HoverTipHelper.Show(...) / Hide(owner)
///
/// 显示后会**每帧跟随目标**重新摆放（列表滚动、目标移动/缩放都不会跑偏）。
/// 目标所在层级不归它管，所以提示框能盖在所有列表/面板之上，也不会被 ScrollRect 的 Mask 裁。
///
/// 预制体约定：
///   img_Root   条目容器（VerticalLayoutGroup + ContentSizeFitter 双向 PreferredSize）
///   img_CardRoot   关联牌容器，使用同样的纵向布局，独立摆放
/// </summary>
public partial class HoverTipPanel : BasePanel
{
    private const float ScreenMarginPixels = 5f;

    /// <summary>单条描述的预制体（Inspector 里拖入 HoverTipItemModule.prefab）</summary>
    [SerializeField] private HoverTipItemModule itemPrefab;
    [SerializeField] private HoverTipCardItemModule cardItemPrefab;

    /// <summary>提示框与目标之间的基础间距（Canvas 单位；每个宿主还可通过 extraGap 再让开一点）</summary>
    [SerializeField] private float gap = 12f;

    /// <summary>img_Root 的 RectTransform（框体尺寸与摆放都以它为准）</summary>
    private RectTransform _rootRect;
    private RectTransform _cardRootRect;

    /// <summary>面板所在的 Canvas（缩放换算用，打开时缓存）</summary>
    private Canvas _canvas;

    /// <summary>条目实例缓存（同一个面板反复显示，不反复 Instantiate）</summary>
    private readonly List<HoverTipItemModule> _items = new();
    private readonly List<HoverTipCardItemModule> _cardItems = new();
    private readonly HashSet<string> _relatedCardIds = new();

    /// <summary>单条接口的复用数组（避免每次调 ShowTip 都 new 一个列表）</summary>
    private readonly HoverTipData[] _single = new HoverTipData[1];

    // ---- 跟随目标（列表滚动 / 目标移动时提示框不能留在原地） ----

    /// <summary>当前跟随的目标（null = 没显示提示）</summary>
    private RectTransform _target;

    /// <summary>跟随用的摆放参数（显示时记下，之后每帧复用）</summary>
    private E_TipDirection _followDirection = E_TipDirection.Down;
    private float _followExtraGap;

    /// <summary>两列的布局尺寸（Canvas 单位）。内容变更时刷新，跟随时只换算为屏幕尺寸。</summary>
    private Vector2 _tipSize;
    private Vector2 _cardSize;
    private Rect _cardShadowOutsets;

    /// <summary>取世界角点用的复用数组（每帧都要用，避免反复 new）</summary>
    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>候选方向：首选 → 其余（固定顺序）</summary>
    private static readonly E_TipDirection[] AllDirections =
    {
        E_TipDirection.Down, E_TipDirection.Up, E_TipDirection.Right, E_TipDirection.Left,
    };

    // ==================== 生命周期 ====================

    protected override void OnOpen()
    {
        _rootRect = comps.imgRoot != null ? comps.imgRoot.rectTransform : null;
        _cardRootRect = comps.imgCardRoot != null ? comps.imgCardRoot.rectTransform : null;
        _canvas = GetComponentInParent<Canvas>();
    }

    protected override void OnShow() { }

    protected override void OnHide()
    {
        _target = null;   // 关掉就不再跟随
        if (_cardRootRect != null) _cardRootRect.gameObject.SetActive(false);
        _cardSize = Vector2.zero;
    }

    protected override void OnClose()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null) Destroy(_items[i].gameObject);
        }
        _items.Clear();
        for (int i = 0; i < _cardItems.Count; i++)
        {
            if (_cardItems[i] != null) Destroy(_cardItems[i].gameObject);
        }
        _cardItems.Clear();

        _rootRect = null;
        _cardRootRect = null;
        _canvas = null;
        _target = null;
    }

    /// <summary>
    /// 每帧跟着目标重新摆一次。
    /// 目标会被滚动 / 拖动 / 缩放（列表滚动、悬停放大补间），面板只摆一次就会变成
    /// “牌走了、提示框留在原地”。这里只重算位置，不动内容也不重算尺寸，开销很小。
    /// 放在 LateUpdate：ScrollRect 滚完、悬停缩放补间跑完后再摆，跟得最紧。
    /// </summary>
    private void LateUpdate()
    {
        if (_target == null) return;

        Place(_target, _followDirection, _followExtraGap);
    }

    protected override void OnPanelOpened()
    {
        // 提示框只负责展示：整框对指针透明
        // （否则鼠标移进提示框会触发 host 的 OnPointerExit → 隐藏 → 又 Enter → 疯狂闪烁）
        // CanvasGroup 是继承的，之后才实例化的条目也一并生效
        SetInteractable(false);
    }

    // ==================== 对外 ====================

    /// <summary>
    /// 显示一组描述（每条一个框）：铺条目 → 算出框体尺寸 → 按目标位置与方向摆放
    /// </summary>
    /// <param name="tips">每条描述（空条目会被跳过）</param>
    /// <param name="target">提示要对齐的目标（一般传触发悬停的 item）</param>
    /// <param name="preferred">首选方向（放不下会自动换方向）</param>
    /// <param name="extraGap">额外让出的距离（Canvas 单位）</param>
    /// <param name="relatedCardIds">关联的纸牌预设 ID</param>
    public void ShowTips(IReadOnlyList<HoverTipData> tips, RectTransform target, E_TipDirection preferred,
        float extraGap = 0f, IReadOnlyList<string> relatedCardIds = null)
    {
        if (!ApplyTips(tips))
        {
            HideSelf();   // 没有可显示的内容 / 没配条目预制体：别留个空框
            return;
        }

        // 同帧把框体尺寸算准，摆放依赖它（否则会按上一帧尺寸摆：位置偏、字两边也会没留白）
        //
        // 条目已在 ApplyTips 中完成内部布局并同步根节点尺寸，这里只需排列一次。
        // 必须从带布局组件的 img_Root 开始重建，面板根节点没有布局控制器。
        _tipSize = RebuildColumn(_rootRect);
        ApplyRelatedCards(relatedCardIds);

        // 记下摆放参数：之后每帧都要按同一套参数重新摆（目标会滚动 / 移动 / 缩放）
        _target = target;
        _followDirection = preferred;
        _followExtraGap = extraGap;

        Place(target, preferred, extraGap);
    }

    /// <summary>单条描述的旧接口（关卡按钮等不需要分条的提示用）</summary>
    public void ShowTip(string title, string content, RectTransform target, E_TipDirection preferred,
        float extraGap = 0f, IReadOnlyList<string> relatedCardIds = null)
    {
        _single[0] = new HoverTipData(title, content);
        ShowTips(_single, target, preferred, extraGap, relatedCardIds);
    }

    // ==================== 内部 ====================

    /// <summary>
    /// 把条目铺到 img_Root 下：按需复用/创建 → 逐条填内容 → 统一正文宽度（所有框等宽）。
    /// 返回是否有内容真的显示出来了
    /// </summary>
    private bool ApplyTips(IReadOnlyList<HoverTipData> tips)
    {
        int count = tips?.Count ?? 0;
        if (count == 0 || _rootRect == null) return false;

        // 1. 条目实例不够才创建（多了的后面 SetActive(false)）
        while (_items.Count < count)
        {
            if (itemPrefab == null)
            {
                Debug.LogWarning("[HoverTipPanel] 没有配置 itemPrefab，无法显示提示");
                return false;
            }

            _items.Add(Instantiate(itemPrefab, _rootRect, false));
        }

        // 2. 逐条填内容，记下最大的正文宽度（空条目跳过）
        float maxWidth = 0f;
        int shown = 0;

        for (int i = 0; i < _items.Count; i++)
        {
            bool used = i < count && tips[i].HasAny;

            _items[i].gameObject.SetActive(used);
            if (!used) continue;

            var tip = tips[i];
            float width = _items[i].SetTip(tip.Title, tip.Content, tip.Icon, tip.IconColor);
            if (width > maxWidth) maxWidth = width;

            shown++;
        }

        if (shown == 0) return false;

        // 3. 统一正文宽度，再完成各条目的内部布局，供外层按实际框体尺寸排列。
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].gameObject.activeSelf) continue;

            _items[i].SetContentWidth(maxWidth);
            _items[i].RebuildLayout();
        }

        return true;
    }

    private void ApplyRelatedCards(IReadOnlyList<string> ids)
    {
        _cardSize = Vector2.zero;
        if (_cardRootRect == null) return;

        int count = ids?.Count ?? 0;
        int shown = 0;
        _relatedCardIds.Clear();
        for (int i = 0; i < count && cardItemPrefab != null; i++)
        {
            string id = ids[i];
            if (string.IsNullOrEmpty(id) || !_relatedCardIds.Add(id)
                || !CardHelper.TryGetDef(id, out var def, out _)) continue;

            // 先激活父节点和实例，确保新实例的 UIBinder 已在 Awake 完成绑定。
            if (shown == 0)
            {
                _cardRootRect.localScale = Vector3.one;
                _cardRootRect.gameObject.SetActive(true);
            }
            if (shown == _cardItems.Count)
                _cardItems.Add(Instantiate(cardItemPrefab, _cardRootRect, false));

            var item = _cardItems[shown++];
            item.gameObject.SetActive(true);
            item.SetCard(def);
        }

        for (int i = shown; i < _cardItems.Count; i++)
            _cardItems[i].gameObject.SetActive(false);

        _cardRootRect.gameObject.SetActive(shown > 0);
        if (shown > 0)
        {
            _cardSize = RebuildColumn(_cardRootRect);
            _cardShadowOutsets = _cardItems[0].GetShadowOutsets();
        }
    }

    private static Vector2 RebuildColumn(RectTransform rect)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        return new Vector2(LayoutUtility.GetPreferredSize(rect, 0), LayoutUtility.GetPreferredSize(rect, 1));
    }

    /// <summary>
    /// 按目标位置摆放：依次尝试候选方向，都放不下就按首选方向定位后夹回屏幕内
    ///
    /// 关键设计：**贴边用「轴心 + 定位点」而不是「中心点 + 框体尺寸」** ——
    /// 把 img_Root 的 pivot 设成贴着目标的那条边（向下显示 = 顶边），
    /// 定位点只由「目标边界 + gap」决定，与提示框自身尺寸无关。
    /// 这样即使框体尺寸晚一帧才被布局系统纠正，也只是朝远离目标的方向伸展，
    /// 与目标之间的间距永远不变（不会出现"距离乱跳"）。
    /// </summary>
    private void Place(RectTransform target, E_TipDirection preferred, float extraGap)
    {
        if (target == null || _rootRect == null) return;

        var canvas = _canvas != null ? _canvas : GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 世界角点包含父级缩放和当前悬停动画，直接取屏幕边界，避免漏算或重复乘缩放。
        GetTargetBounds(target, out var targetCenter, out var targetSize);
        float canvasScale = canvas.scaleFactor;
        float useGap = (gap + Mathf.Max(0f, extraGap)) * canvas.rootCanvas.scaleFactor;
        float columnGap = gap * canvasScale;
        var screen = Rect.MinMaxRect(ScreenMarginPixels, ScreenMarginPixels,
            Screen.width - ScreenMarginPixels, Screen.height - ScreenMarginPixels);
        var tipSize = _tipSize * canvasScale;
        var cardSize = _cardSize * canvasScale;
        bool hasCards = cardSize.x > 0f && cardSize.y > 0f;
        var bestDirection = preferred;
        var bestTipAnchor = Vector2.zero;
        var bestCardAnchor = Vector2.zero;
        var bestCardPivot = Vector2.zero;
        float bestCardScale = 0f;

        // 先试首选方向，再试其余（固定顺序）；完整落在屏幕内才采用
        // （这里用下标循环而不是迭代器，避免每帧产生一个枚举器对象）
        for (int i = -1; i < AllDirections.Length; i++)
        {
            var dir = i < 0 ? preferred : AllDirections[i];
            if (i >= 0 && dir == preferred) continue;

            Vector2 anchor = CalcAnchor(dir, targetCenter, targetSize, useGap);
            Vector2 tipPivot = ToPivot(dir);
            Rect tipBox = BoxRect(tipPivot, anchor, tipSize);
            Rect tipBounds = itemPrefab.IncludeShadow(tipBox, canvasScale);
            if (!hasCards)
            {
                if (!IsInside(screen, tipBounds)) continue;
                ApplyPlacement(_rootRect, tipPivot, anchor, canvas);
                return;
            }

            bool alignWithTip = dir == E_TipDirection.Up || dir == E_TipDirection.Down;
            E_TipDirection cardDir = GetCardDirection(dir);
            Vector2 cardPivot = ToPivot(cardDir);
            Vector2 cardAnchor;
            if (alignWithTip)
            {
                // 文字列顶部就是第一条可见 tip 的顶边。牌列固定在文字列右侧并从这一高度往下排。
                cardPivot.y = 1f;
                cardAnchor = new Vector2(tipBox.xMax + columnGap, tipBox.yMax);
            }
            else
            {
                cardAnchor = CalcAnchor(cardDir, targetCenter, targetSize, useGap);
            }

            // 列的轴心按牌面定位，阴影只扩张用于安全区检查的矩形。
            Rect cardBounds = BoxRect(cardPivot, Vector2.zero, cardSize);
            cardBounds.min += _cardShadowOutsets.min * canvasScale;
            cardBounds.max += _cardShadowOutsets.max * canvasScale;
            Rect placedCardBounds = cardBounds;
            placedCardBounds.position += cardAnchor;
            if (IsInside(screen, tipBounds) && IsInside(screen, placedCardBounds))
            {
                ApplyPlacement(_rootRect, tipPivot, anchor, canvas);
                PlaceCards(cardPivot, cardAnchor, 1f, canvas);
                return;
            }

            // 记录兜底方案：沿贴边的轴平移文字，保留能放下的最大牌列。
            Vector2 shift = FitTipToScreen(tipBounds, screen);
            if (alignWithTip) shift.y = 0f;
            else shift.x = 0f;
            tipBounds.position += shift;
            if (!IsInside(screen, tipBounds)) continue;

            if (alignWithTip) cardAnchor += shift;
            float cardScale = FitCards(cardBounds, alignWithTip, ref cardAnchor, screen);

            if (cardScale <= bestCardScale) continue;
            bestDirection = dir;
            bestTipAnchor = anchor + shift;
            bestCardAnchor = cardAnchor;
            bestCardPivot = cardPivot;
            bestCardScale = cardScale;
        }

        if (bestCardScale > 0f)
        {
            ApplyPlacement(_rootRect, ToPivot(bestDirection), bestTipAnchor, canvas);
            PlaceCards(bestCardPivot, bestCardAnchor, bestCardScale, canvas);
            return;
        }

        // 画面太窄时优先保证文字可读。保持牌列布局，下次跟随时可以直接恢复显示。
        if (hasCards) _cardRootRect.localScale = new Vector3(0f, 0f, 1f);
        Vector2 fallback = CalcAnchor(preferred, targetCenter, targetSize, useGap);
        Vector2 fallbackPivot = ToPivot(preferred);
        Rect fallbackBounds = itemPrefab.IncludeShadow(BoxRect(fallbackPivot, fallback, tipSize), canvasScale);
        ApplyPlacement(_rootRect, fallbackPivot, fallback + FitTipToScreen(fallbackBounds, screen), canvas);
    }

    private static E_TipDirection GetCardDirection(E_TipDirection tipDirection)
    {
        return tipDirection == E_TipDirection.Right ? E_TipDirection.Left : E_TipDirection.Right;
    }

    private static Vector2 FitTipToScreen(Rect bounds, Rect screen)
    {
        Vector2 minShift = screen.min - bounds.min;
        Vector2 maxShift = screen.max - bounds.max;

        // 内容超过可用尺寸时，保留尽可能大的可见区域。
        return new Vector2(
            Mathf.Clamp(0f, Mathf.Min(minShift.x, maxShift.x), Mathf.Max(minShift.x, maxShift.x)),
            Mathf.Clamp(0f, Mathf.Min(minShift.y, maxShift.y), Mathf.Max(minShift.y, maxShift.y)));
    }

    private static float FitCards(Rect bounds, bool alignWithTip, ref Vector2 anchor, Rect screen)
    {
        if (anchor.x < screen.xMin || anchor.x > screen.xMax) return 0f;

        float scale = Mathf.Min(ScaleToEdge(anchor.x - screen.xMin, -bounds.xMin),
            ScaleToEdge(screen.xMax - anchor.x, bounds.xMax));
        if (alignWithTip)
        {
            if (anchor.y < screen.yMin || anchor.y > screen.yMax) return 0f;
            float verticalScale = Mathf.Min(ScaleToEdge(anchor.y - screen.yMin, -bounds.yMin),
                ScaleToEdge(screen.yMax - anchor.y, bounds.yMax));
            return Mathf.Min(scale, verticalScale);
        }

        scale = Mathf.Min(scale, screen.height / bounds.height);
        anchor.y = Mathf.Clamp(anchor.y, screen.yMin - bounds.yMin * scale, screen.yMax - bounds.yMax * scale);
        return scale;
    }

    private static float ScaleToEdge(float space, float extent)
    {
        return extent > space ? space / extent : 1f;
    }

    private void PlaceCards(Vector2 pivot, Vector2 anchor, float scale, Canvas canvas)
    {
        _cardRootRect.localScale = new Vector3(scale, scale, 1f);
        ApplyPlacement(_cardRootRect, pivot, anchor, canvas);
    }

    /// <summary>定位点 = 「提示框贴着目标的那条边的中点」应落在的屏幕坐标（只看目标边界 + gap）</summary>
    private static Vector2 CalcAnchor(E_TipDirection dir, Vector2 targetCenter, Vector2 targetSize, float useGap)
    {
        switch (dir)
        {
            case E_TipDirection.Up: return targetCenter + new Vector2(0f, targetSize.y * 0.5f + useGap);
            case E_TipDirection.Right: return targetCenter + new Vector2(targetSize.x * 0.5f + useGap, 0f);
            case E_TipDirection.Left: return targetCenter - new Vector2(targetSize.x * 0.5f + useGap, 0f);
            default: return targetCenter - new Vector2(0f, targetSize.y * 0.5f + useGap);   // Down
        }
    }

    /// <summary>各方向的轴心，取「贴着目标的那条边」</summary>
    private static Vector2 ToPivot(E_TipDirection dir)
    {
        switch (dir)
        {
            case E_TipDirection.Up: return new Vector2(0.5f, 0f);    // 框在目标上方 → 定在框的底边
            case E_TipDirection.Right: return new Vector2(0f, 0.5f); // 框在目标右侧 → 定在框的左边
            case E_TipDirection.Left: return new Vector2(1f, 0.5f);  // 定在框的右边
            default: return new Vector2(0.5f, 1f);                   // Down → 定在框的顶边
        }
    }

    /// <summary>按轴心算出容器在屏幕上的矩形（用于出屏判断）</summary>
    private static Rect BoxRect(Vector2 pivot, Vector2 anchor, Vector2 size)
    {
        return new Rect(anchor - new Vector2(size.x * pivot.x, size.y * pivot.y), size);
    }

    /// <summary>inner 是否完整落在 outer 内（Rect 没有 Contains(Rect) 重载）</summary>
    private static bool IsInside(Rect outer, Rect inner)
    {
        return inner.xMin >= outer.xMin && inner.xMax <= outer.xMax
            && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;
    }

    /// <summary>设置列的轴心，并把定位点摆到指定的屏幕坐标。</summary>
    private static void ApplyPlacement(RectTransform rect, Vector2 pivot, Vector2 anchorScreen, Canvas canvas)
    {
        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;

        // 转回提示框所在画布，支持 Overlay 与 Camera 画布。
        var camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvas.rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, anchorScreen, camera, out var world))
            return;

        rect.pivot = pivot;
        rect.position = world;
    }

    /// <summary>取目标实际屏幕边界，包含各层缩放、旋转和悬停动画。</summary>
    private static void GetTargetBounds(RectTransform rect, out Vector2 center, out Vector2 size)
    {
        var targetCanvas = rect.GetComponentInParent<Canvas>();
        var camera = targetCanvas != null && targetCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.rootCanvas.worldCamera : null;
        rect.GetWorldCorners(Corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
        Vector2 max = min;
        for (int i = 1; i < Corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, Corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        center = (min + max) * 0.5f;
        size = max - min;
    }
}
