using Framework.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 悬停提示框（通用触发式提示）—— 标题 + 正文，内容自适应尺寸、方向自适应摆放
///
/// 用法：业务层不要直接 Show，统一走 HoverTipHelper.Show(...) / Hide(owner)
///
/// 显示后会**每帧跟随目标**重新摆放（列表滚动、目标移动/缩放都不会跑偏）。
/// 目标所在层级不归它管，所以提示框能盖在所有列表/面板之上，也不会被 ScrollRect 的 Mask 裁。
///
/// 预制体约定：
///   img_Bg      框体（VerticalLayoutGroup + ContentSizeFitter 双向 PreferredSize）
///   txt_Title   标题（单行，宽自适应）
///   txt_Content 正文（ContentSizeFitter = 水平 Unconstrained + 垂直 PreferredSize，宽度由本类写入）
/// </summary>
public partial class HoverTipPanel : BasePanel
{
    /// <summary>正文单行最大宽度（内容区宽度，不含 img_Bg 的内边距）</summary>
    [SerializeField] private float maxContentWidth = 420f;

    /// <summary>提示框与目标之间的基础间距（屏幕像素；每张牌还可通过 extraGap 再让开一点）</summary>
    [SerializeField] private float gap = 6f;

    /// <summary>正文的 RectTransform（限宽用：只改宽度，高度由它自己的 ContentSizeFitter 按换行自动撑）</summary>
    private RectTransform _contentRect;

    /// <summary>img_Bg 的 RectTransform（框体尺寸与摆放都以它为准）</summary>
    private RectTransform _bgRect;

    /// <summary>面板所在的 Canvas（缩放换算用，打开时缓存）</summary>
    private Canvas _canvas;

    // ---- 跟随目标（列表滚动 / 目标移动时提示框不能留在原地） ----

    /// <summary>当前跟随的目标（null = 没显示提示）</summary>
    private RectTransform _target;

    /// <summary>跟随用的摆放参数（显示时记下，之后每帧复用）</summary>
    private E_TipDirection _followDirection = E_TipDirection.Down;
    private float _followScale = 1f;
    private float _followExtraGap;

    /// <summary>框体尺寸（屏幕像素）。内容固定，显示时算一次就够，每帧不用再问布局系统</summary>
    private Vector2 _tipSize;

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
        _contentRect = comps.txtContent != null ? comps.txtContent.rectTransform : null;
        _bgRect = comps.imgBg != null ? comps.imgBg.rectTransform : null;
        _canvas = GetComponentInParent<Canvas>();
    }

    protected override void OnShow() { }

    protected override void OnHide()
    {
        _target = null;   // 关掉就不再跟随
    }

    protected override void OnClose()
    {
        _contentRect = null;
        _bgRect = null;
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

        Place(_target, _followDirection, _followScale, _followExtraGap);
    }

    protected override void OnPanelOpened()
    {
        // 提示框只负责展示：整框对指针透明
        // （否则鼠标移进提示框会触发 host 的 OnPointerExit → 隐藏 → 又 Enter → 疯狂闪烁）
        SetInteractable(false);
    }

    // ==================== 对外 ====================

    /// <summary>
    /// 显示提示：设置内容 → 算出框体尺寸 → 按目标位置与方向摆放
    /// </summary>
    /// <param name="title">标题（可空）</param>
    /// <param name="content">正文（可空；为空则整块隐藏，框只包住标题）</param>
    /// <param name="target">提示要对齐的目标（一般传触发悬停的 item）</param>
    /// <param name="preferred">首选方向（放不下会自动换方向）</param>
    /// <param name="targetScale">目标最终会放大到多少倍（悬停放大时用；按放大后的尺寸定位，间距才不会被吃掉）</param>
    /// <param name="extraGap">额外让出的距离（像素）</param>
    public void ShowTip(string title, string content, RectTransform target, E_TipDirection preferred,
        float targetScale = 1f, float extraGap = 0f)
    {
        ApplyContent(title, content);

        // 同帧把框体尺寸算准，摆放依赖它（否则会按上一帧尺寸摆：位置偏、字两边也会没留白）
        //
        // 两个坑：
        //   1. 必须从 img_Bg 开始重建。uGUI 的 PerformLayoutControl 碰到“自己身上没有任何布局组件”
        //      的节点会整棵子树跳过，而面板根节点正好没有布局组件 —— 传根节点等于什么都没做。
        //   2. 要连做两次。img_Bg 的 ChildControl 是关的，所以框体尺寸要看子物体当前的 sizeDelta，
        //      而子物体的尺寸是它们自己的 ContentSizeFitter 在同一次布局里“稍后”才写进去的；
        //      第一遍读到的还是上一轮旧值，第二遍才是新值。
        if (_bgRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bgRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bgRect);
        }

        // 此刻框体尺寸是准的，缓存下来：跟随的时候只重算位置，不再每帧问布局系统
        // （直接问布局系统的话，LayoutUtility 内部会做 GetComponents，每帧几次没必要）
        RecalcTipSize();

        // 记下摆放参数：之后每帧都要按同一套参数重新摆（目标会滚动 / 移动 / 缩放）
        _target = target;
        _followDirection = preferred;
        _followScale = targetScale;
        _followExtraGap = extraGap;

        Place(target, preferred, targetScale, extraGap);
    }

    // ==================== 内部 ====================

    /// <summary>写入标题 / 正文，并给正文一个不超过 maxContentWidth 的宽度（换行的关键）</summary>
    private void ApplyContent(string title, string content)
    {
        // 标题可空（普通牌 / 普通关这种不需要标题，信息都写在描述里）：
        // 为空时要把整块隐藏，不能只把文字清空 —— TMP 空文本的 preferredHeight 仍是一行高，
        // 会在框顶留一条空白（布局组会跳过未激活的子物体，所以隐藏才是对的）
        bool hasTitle = !string.IsNullOrEmpty(title);
        comps.txtTitle.gameObject.SetActive(hasTitle);

        if (hasTitle)
        {
            comps.txtTitle.text = title;
            comps.txtTitle.ForceMeshUpdate();   // 立刻刷新标题的 preferredWidth（框体宽度依赖它）
        }

        bool hasContent = !string.IsNullOrEmpty(content);
        comps.txtContent.gameObject.SetActive(hasContent);

        if (!hasContent) return;

        comps.txtContent.text = content;

        // 1. 宽度取「按 maxContentWidth 排布后实际需要的宽度」：
        //      短文本 → 文字本身的宽度（框贴着字）
        //      超长   → 封顶在 maxContentWidth，此时 TMP 会自动换行
        float width = comps.txtContent.GetPreferredValues(content, maxContentWidth, 0f).x;
        _contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        // 2. 立刻让 TMP 按新宽度重排一次。
        //    RectTransform 尺寸变化要等 Unity 派发 OnRectTransformDimensionsChange 才失效，
        //    不强制刷新的话，紧接着读到的 preferredHeight 还是「上一个宽度」下的旧值，
        //    框体高度就会算错 —— 表现为「提示框边缘到牌的距离忽大忽小地跳」
        comps.txtContent.ForceMeshUpdate();
    }

    /// <summary>
    /// 按目标位置摆放：依次尝试候选方向，都放不下就按首选方向定位后夹回屏幕内
    ///
    /// 关键设计：**贴边用「轴心 + 定位点」而不是「中心点 + 框体尺寸」** ——
    /// 把 img_Bg 的 pivot 设成贴着目标的那条边（向下显示 = 顶边），
    /// 定位点只由「目标边界 + gap」决定，与提示框自身尺寸无关。
    /// 这样即使框体尺寸晚一帧才被布局系统纠正，也只是朝远离目标的方向伸展，
    /// 与目标之间的间距永远不变（不会出现"距离乱跳"）。
    /// </summary>
    private void Place(RectTransform target, E_TipDirection preferred, float targetScale, float extraGap)
    {
        if (target == null || _bgRect == null) return;

        var canvas = _canvas != null ? _canvas : GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 目标中心：用世界角点换算（不受锚点 / 轴心 / 缩放影响；放大是从中心长，中心基本不动）
        GetTargetCenter(target, out var targetCenter);

        // 目标尺寸：用「局部 rect 尺寸 × 画布缩放 × 悬停放大倍数」算。
        // ⚠ 不能用角点量出来的尺寸：那是「当前实际大小」，悬停放大补间跑完后它已经含着放大倍数了，
        //    再乘一次就是重复放大，间距会莫名变远（每帧跟随之后才会暴露这个问题）。
        Vector2 targetSize = target.rect.size * canvas.scaleFactor
                             * (targetScale > 0f ? targetScale : 1f);

        float useGap = gap + Mathf.Max(0f, extraGap);

        var screen = new Rect(0f, 0f, Screen.width, Screen.height);

        // 先试首选方向，再试其余（固定顺序）；完整落在屏幕内才采用
        // （这里用下标循环而不是迭代器，避免每帧产生一个枚举器对象）
        for (int i = -1; i < AllDirections.Length; i++)
        {
            var dir = i < 0 ? preferred : AllDirections[i];
            if (i >= 0 && dir == preferred) continue;

            Vector2 anchor = CalcAnchor(dir, targetCenter, targetSize, useGap);
            if (!IsInside(screen, BoxRect(dir, anchor, _tipSize))) continue;

            ApplyPlacement(dir, anchor, canvas);
            return;
        }

        // 四个方向都放不下：按首选方向定位，再把定位点夹回屏幕内
        Vector2 fallback = CalcAnchor(preferred, targetCenter, targetSize, useGap);
        ApplyPlacement(preferred, ClampAnchor(preferred, fallback, _tipSize), canvas);
    }

    /// <summary>按当前框体重新求一次尺寸（只在显示时调，跟随过程中不变）</summary>
    private void RecalcTipSize()
    {
        if (_bgRect == null)
        {
            _tipSize = Vector2.zero;
            return;
        }

        float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;

        // 直接问布局系统要，比读 RectTransform.rect 可靠
        // （布局刚重建时 rect 可能还没落到实处，而这里读的是布局计算值本身）
        _tipSize = new Vector2(
            LayoutUtility.GetPreferredSize(_bgRect, 0),
            LayoutUtility.GetPreferredSize(_bgRect, 1)) * scaleFactor;
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

    /// <summary>按方向与轴心算出提示框在屏幕上的矩形（用于出屏判断）</summary>
    private static Rect BoxRect(E_TipDirection dir, Vector2 anchor, Vector2 size)
    {
        var pivot = ToPivot(dir);
        return new Rect(anchor - new Vector2(size.x * pivot.x, size.y * pivot.y), size);
    }

    /// <summary>inner 是否完整落在 outer 内（Rect 没有 Contains(Rect) 重载）</summary>
    private static bool IsInside(Rect outer, Rect inner)
    {
        return inner.xMin >= outer.xMin && inner.xMax <= outer.xMax
            && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;
    }

    /// <summary>兜底：把定位点夹回屏幕内（按轴心换算左右 / 上下边界）</summary>
    private static Vector2 ClampAnchor(E_TipDirection dir, Vector2 anchor, Vector2 size)
    {
        var pivot = ToPivot(dir);

        return new Vector2(
            Mathf.Clamp(anchor.x, size.x * pivot.x, Screen.width - size.x * (1f - pivot.x)),
            Mathf.Clamp(anchor.y, size.y * pivot.y, Screen.height - size.y * (1f - pivot.y)));
    }

    /// <summary>落地：设轴心 + 把定位点摆到指定屏幕坐标（img_Bg 就是可见框体，直接摆它）</summary>
    private void ApplyPlacement(E_TipDirection dir, Vector2 anchorScreen, Canvas canvas)
    {
        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;

        // Overlay 画布：camera 传 null
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, anchorScreen, null, out var world))
            return;

        _bgRect.pivot = ToPivot(dir);
        _bgRect.position = world;
    }

    /// <summary>取目标中心点的屏幕坐标（用世界角点，不受锚点 / 轴心 / 缩放影响）</summary>
    private static void GetTargetCenter(RectTransform rect, out Vector2 center)
    {
        rect.GetWorldCorners(Corners);   // 0=左下 1=左上 2=右上 3=右下（复用静态数组，避免每帧分配）

        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);

        center = (min + max) * 0.5f;
    }
}
