using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 悬停表现 —— 鼠标移入放大 / 描边、移出还原，通用组件（纸牌格 / 按钮 / 任意 UI 都能用）
///
/// 用法：
///   按钮之类自己就能收到指针事件的：挂上本组件，autoTrigger 打勾，零代码。
///   纸牌格这种"事件来自子物体按钮"的：autoTrigger 不勾，由宿主脚本
///       拿到本组件后调 SetHover(true / false)
///   缩放目标用 target 指定（留空 = 自己）。**务必指向一个「轴心在正中、尺寸铺满 item」的节点**，
///   这样才是从中心放大；指向根节点会因为根节点轴心在左上角而朝右下长。
///
/// 描边（可选，hoverOutline）：
///   勾上 = 悬停时打开 Outline、移出关掉。编辑器里勾选时目标上没挂 Outline 会自动补一个，
///   取消勾选把**自动挂的**删掉（手动挂的 Outline 不动，可能另有用途）。
///   颜色 / 宽度以本组件的字段为准（会同步写入 Outline），不用去 Outline 组件上调。
///   Outline 只能作用在 Graphic（也就是 Image）上：
///     outlineTarget 留空 → 先找自己身上的 Graphic，再退化到子节点第一个 Graphic；
///     牌格这种根节点没有 Graphic 的，请把 outlineTarget 显式指到牌面那张 Image。
///   原理是把原图网格往四个方向各复制一份，所以矩形牌面看起来是"往外扩一圈的边框"。
///   和缩放共用同一个悬停状态：宿主 SetHover 驱动的纸牌格不用改代码，描边自动跟着走。
///
/// 层级 / 裁切：
///   只缩放 + 悬停时把自己提到同级最后一位（同级里靠后的画在上层，所以不会被相邻的牌盖住）：
///     - 不改父子关系、不改锚点位置，所以父级是 LayoutGroup 还是 ScrollList 手动定位都不受影响
///       （布局只看 sizeDelta 和 anchoredPosition，不看 localScale）；
///     - 放大后超出 ScrollRect 视口的部分会被 Mask 裁掉 —— 预期行为，不做任何脱离 Mask 的操作；
///     - 直接父级挂着 LayoutGroup 时不提层（改同级顺序会让布局重排）。
///
/// 池化：OnDisable 会立刻（不播动画）复位缩放与层级、并收起描边，回收 / 面板关闭都不会留残留状态。
/// </summary>
public class HoverScaleBehaviour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("缩放")]
    /// <summary>缩放目标（留空 = 自己）；应指向轴心居中的包裹节点</summary>
    [SerializeField] private RectTransform target;

    /// <summary>悬停时的放大倍数（每个预制体各调各的）</summary>
    [SerializeField] private float hoverScale = 1.12f;

    [Header("呼吸（提示可点）")]
    /// <summary>
    /// 是否允许呼吸（默认关）。没勾的预制体永远不会呼吸 —— 其它挂本组件的地方（牌格 / 商店格子等）不受影响，
    /// 需要的地方（目前只有关卡按钮）自己在预制体上勾上
    /// </summary>
    [SerializeField] private bool allowBreathing = false;

    /// <summary>空闲呼吸的最小倍数（相对基座缩放）</summary>
    [SerializeField] private float breatheMinScale = 0.9f;

    /// <summary>空闲呼吸的最大倍数（相对基座缩放）—— 别超过 hoverScale，否则分不清“呼吸”和“悬停”</summary>
    [SerializeField] private float breatheMaxScale = 1.2f;

    /// <summary>呼吸单程时长（秒）—— 一个来回是它的 2 倍</summary>
    [SerializeField] private float breatheDuration = 1f;

    /// <summary>缩放时长（秒）</summary>
    [SerializeField] private float duration = 0.12f;

    /// <summary>缓动</summary>
    [SerializeField] private Ease ease = Ease.OutQuad;

    [Header("触发 / 层级")]
    /// <summary>是否自己响应指针进出（按钮 = true；由宿主脚本驱动的纸牌格 = false）</summary>
    [SerializeField] private bool autoTrigger = false;

    /// <summary>悬停时提到同级最后一位（画在相邻 item 之上，不被盖住）</summary>
    [SerializeField] private bool raiseOnHover = true;

    /// <summary>
    /// 要提到同级最后一位的节点（留空 = 自己）。
    /// 缩放目标是子节点时（例如按钮上挂了宿主模组）要指外层容器，
    /// 否则只会动到子节点、盖不住外面的兄弟。
    /// </summary>
    [SerializeField] private Transform raiseTarget;

    /// <summary>用不受 timeScale 影响的时间走动画</summary>
    [SerializeField] private bool useUnscaledTime = true;

    [Header("描边（可选）")]
    /// <summary>悬停描边（勾选时自动补挂 Outline；取消勾选把自动挂的删掉）</summary>
    [SerializeField] private bool hoverOutline = false;

    /// <summary>描边颜色（以这里为准，会同步写入 Outline）</summary>
    [SerializeField] private Color outlineColor = new Color(1f, 0.84f, 0.3f, 1f);

    /// <summary>描边外扩距离（像素，四个方向各扩这么多）</summary>
    [SerializeField] private Vector2 outlineDistance = new Vector2(2f, 2f);

    /// <summary>要描边的 Graphic（留空 = 自己身上的 Graphic，没有再取子节点第一个）</summary>
    [SerializeField] private Graphic outlineTarget;

    /// <summary>Outline 是不是本组件自动挂的（取消勾选时只删自己挂的那个）</summary>
    [SerializeField, HideInInspector] private bool outlineOwned;

    // ==================== 对外 ====================

    /// <summary>放大倍数（提示框按"放大后的尺寸"算间距时要用）</summary>
    public float HoverScale => hoverScale;

    /// <summary>当前是否处于悬停放大状态</summary>
    public bool IsHovering { get; private set; }

    /// <summary>当前是否处于呼吸状态（宿主设过一次后一直保留，失活/复显后自动接上）</summary>
    public bool IsBreathing => _breathing;

    private bool _breathing;

    /// <summary>悬停状态变化（true = 进入 / false = 离开）—— 宿主用来做提示框之类的联动</summary>
    public Action<bool> OnHoverChanged;

    /// <summary>设置悬停状态（幂等；可由本组件自己触发，也可由宿主驱动）</summary>
    /// <param name="immediate">true = 不播动画直接到位（回收等打断场景用）</param>
    public void SetHover(bool on, bool immediate = false)
    {
        if (IsHovering == on) return;
        if (_rect == null) return;

        IsHovering = on;

        OnHoverChanged?.Invoke(on);

        if (on) Raise();

        ApplyOutline(on);

        KillTween();

        if (immediate)
        {
            _rect.localScale = on ? _baseScale * hoverScale : _baseScale;
            if (!on) RestoreSibling();
            return;
        }

        var to = on ? _baseScale * hoverScale : _baseScale;
        _scaleTween = AnimationHelper.ScaleTo(_rect, to, duration, ease).SetUpdate(useUnscaledTime);

        // 缩回去的过程里还要继续盖着邻居，所以等补间播完再还同级顺序；
        // 播完之后如果还处于呼吸模式，接着呼吸
        if (!on)
        {
            _scaleTween.OnComplete(() =>
            {
                RestoreSibling();
                if (_breathing) StartBreathe();
            });
        }
    }

    /// <summary>
    /// 空闲呼吸（在 [breatheMinScale, breatheMaxScale] 之间无限往返）—— 用来提示「这个还能点」。
    /// 悬停优先：悬停期间不呼吸，悬停结束后自动接上。走过 / 未解锁的关卡传 false 即可。
    /// </summary>
    public void SetBreathing(bool on)
    {
        if (!allowBreathing) on = false;   // 没打开开关的预制体一律不呼吸

        if (_breathing == on) return;
        _breathing = on;

        if (_rect == null) return;
        if (IsHovering) return;   // 悬停中：只记标志，等 SetHover(false) 播完再启动

        if (on) StartBreathe();
        else
        {
            KillTween();
            _scaleTween = AnimationHelper.ScaleTo(_rect, _baseScale, duration, ease).SetUpdate(useUnscaledTime);
        }
    }

    /// <summary>从当前缩放平滑走到峰值，然后开始在 [min, max] 之间无限往返</summary>
    private void StartBreathe()
    {
        if (!allowBreathing) return;

        KillTween();

        float maxFactor = Mathf.Max(breatheMinScale, breatheMaxScale);
        float minFactor = Mathf.Min(breatheMinScale, breatheMaxScale);

        Vector3 max = _baseScale * maxFactor;
        Vector3 min = _baseScale * minFactor;

        // 第一程：当前值 → 峰值（避免直接跳到峰值那一下的突亮）
        _scaleTween = AnimationHelper.ScaleTo(_rect, max, breatheDuration, Ease.InOutSine)
            .SetUpdate(useUnscaledTime)
            .OnComplete(() =>
            {
                if (!_breathing || IsHovering) return;

                // 峰值 ↔ 谷值，无限往返
                _scaleTween = _rect.DOScale(min, breatheDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(useUnscaledTime);
            });
    }

    // ==================== 指针 ====================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (autoTrigger) SetHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (autoTrigger) SetHover(false);
    }

    // ==================== 生命周期 ====================

    private void Awake()
    {
        _rect = target != null ? target : (RectTransform)transform;
        _baseScale = _rect.localScale;

        PrepareOutline();
    }

    /// <summary>重新显示（面板再打开 / 池化取出）后，如果还处于呼吸模式就接上</summary>
    private void OnEnable()
    {
        if (allowBreathing && _breathing && _rect != null && !IsHovering) StartBreathe();
    }

    /// <summary>被失活（池化回收 / 面板关闭）时立刻复位，不留缩放残留和层级残留</summary>
    private void OnDisable()
    {
        KillTween();

        if (_rect != null) _rect.localScale = _baseScale;

        if (_outline != null) _outline.enabled = false;

        // 这里只能复位缩放与状态：失活过程中改同级顺序会被 Unity 报错（见 RestoreSibling）
        _raised = false;
        _raisedNode = null;
        _originSibling = -1;

        if (IsHovering)
        {
            IsHovering = false;
            OnHoverChanged?.Invoke(false);   // 失活时补一次“离开”，宿主正好把提示收掉
        }
    }

    // ==================== 描边（可选） ====================

    /// <summary>描边初始化：确保 Outline 存在、按本组件字段配好、先藏起来等悬停</summary>
    private void PrepareOutline()
    {
        if (!hoverOutline) return;

        _outline = EnsureOutline();

        if (_outline == null)
        {
            Debug.LogWarning(
                "[HoverScaleBehaviour] 勾选了「悬停描边」，但目标上没有 Graphic（Image）——" +
                "outlineTarget 留空时会先找自己、再找子节点第一个 Graphic，牌格这类请显式指到牌面 Image。", this);
            return;
        }

        SyncOutlineStyle(_outline);
        _outline.enabled = false;
    }

    /// <summary>悬停开描边、移出关掉（没勾 hoverOutline 时不会开）</summary>
    private void ApplyOutline(bool on)
    {
        if (!on)
        {
            if (_outline != null) _outline.enabled = false;
            return;
        }

        if (!hoverOutline) return;

        if (_outline == null) _outline = EnsureOutline();
        if (_outline == null) return;

        SyncOutlineStyle(_outline);
        _outline.enabled = true;
    }

    /// <summary>描边目标 Graphic：outlineTarget → 自己身上的 Graphic → 子节点第一个 Graphic</summary>
    private Graphic ResolveOutlineGraphic()
    {
        if (outlineTarget != null) return outlineTarget;

        var graphic = GetComponent<Graphic>();
        if (graphic != null) return graphic;

        return GetComponentInChildren<Graphic>(true);
    }

    /// <summary>拿到目标上的 Outline，没有就补挂一个（补挂的会被记成"本组件的"，取消勾选时才会删）</summary>
    private Outline EnsureOutline()
    {
        var graphic = ResolveOutlineGraphic();
        if (graphic == null) return null;

        var outline = graphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = graphic.gameObject.AddComponent<Outline>();
            outlineOwned = true;
        }

        return outline;
    }

    /// <summary>删掉自动挂上的 Outline（手动挂的不动）</summary>
    private void RemoveOutline()
    {
        var graphic = ResolveOutlineGraphic();
        var outline = graphic != null ? graphic.GetComponent<Outline>() : null;

        if (outline != null)
        {
            outline.enabled = false;

            if (Application.isPlaying) Destroy(outline);
            else DestroyImmediate(outline);   // 编辑器里（取消勾选）要立刻消失，不能用 Destroy
        }

        outlineOwned = false;
        _outline = null;
    }

    /// <summary>颜色 / 宽度以本组件字段为准（Outline 上单独改的会被覆盖）</summary>
    private void SyncOutlineStyle(Outline outline)
    {
        outline.effectColor = outlineColor;
        outline.effectDistance = outlineDistance;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器里勾选 / 取消勾选时同步 Outline。
    /// 推到 delayCall 再改，避免在 OnValidate（反序列化期间）增删组件。
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= SyncOutlineInEditor;
        UnityEditor.EditorApplication.delayCall += SyncOutlineInEditor;
    }

    private void SyncOutlineInEditor()
    {
        if (this == null || Application.isPlaying) return;   // delayCall 里对象可能已经被删了

        if (!hoverOutline)
        {
            if (outlineOwned) RemoveOutline();
            return;
        }

        var outline = EnsureOutline();
        if (outline == null) return;

        SyncOutlineStyle(outline);
        outline.enabled = false;   // 编辑时不显示，等悬停再开
    }
#endif

    // ==================== 内部 ====================

    private RectTransform _rect;

    /// <summary>当前描边组件（悬停开关用；没勾 hoverOutline 时恒为 null）</summary>
    private Outline _outline;

    /// <summary>未放大时的缩放（还原用；不写死 (1,1,1)）</summary>
    private Vector3 _baseScale = Vector3.one;

    private Tween _scaleTween;

    private bool KillTween()
    {
        if (_scaleTween == null) return false;

        _scaleTween.Kill();
        _scaleTween = null;
        return true;
    }

    // ---------- 同级提层（不被相邻牌盖住） ----------

    /// <summary>提层前的同级顺序（还原用）</summary>
    private int _originSibling = -1;

    /// <summary>实际被提层的节点（还原用）</summary>
    private Transform _raisedNode;

    /// <summary>是否已提到同级最后一位</summary>
    private bool _raised;

    /// <summary>
    /// 提到同级最后一位 —— 同级里靠后的画在上层，放大后就不会被相邻的牌盖住。
    ///
    /// 只改同级顺序：不动父子关系、不动 Mask，所以该被视口裁掉的部分依旧会被裁（预期行为），
    /// 也不会干扰 ScrollList（它按自己的 slot 列表摆位置，不看子节点顺序）。
    /// 直接父级挂着 LayoutGroup 时不提（改顺序会让布局重排），这种情况才会被相邻项盖住。
    /// </summary>
    private void Raise()
    {
        if (!raiseOnHover || _raised) return;

        var node = raiseTarget != null ? raiseTarget : transform;
        var parent = node.parent;
        if (parent == null) return;
        if (parent.GetComponent<LayoutGroup>() != null) return;

        _originSibling = node.GetSiblingIndex();
        _raisedNode = node;
        node.SetAsLastSibling();
        _raised = true;
    }

    /// <summary>还原同级顺序（幂等）
    ///
    /// 注意：整个面板在批量 SetActive(false) 时，Unity 禁止在这个过程里改同级顺序，
    /// 会报 “Cannot change the sibling position ... while activating or deactivating”。
    /// 所以失活期间只把提层状态丢掉、不写回顺序 —— 我们的父级都是手动定位的容器
    /// （ScrollList.Content / 面板根节点），同级顺序不影响显示，下次悬停会重新提层。
    /// </summary>
    private void RestoreSibling()
    {
        if (!_raised) return;

        _raised = false;

        var node = _raisedNode;
        _raisedNode = null;

        if (node == null || _originSibling < 0) return;
        if (!node.gameObject.activeInHierarchy) return;   // 正在失活（或已失活）→ 这次不写

        node.SetSiblingIndex(_originSibling);
    }
}
