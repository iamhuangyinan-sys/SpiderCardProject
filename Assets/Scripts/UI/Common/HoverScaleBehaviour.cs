using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 悬停缩放 —— 鼠标移入放大、移出还原，通用组件（纸牌格 / 按钮 / 任意 UI 都能用）
///
/// 用法：
///   按钮之类自己就能收到指针事件的：挂上本组件，autoTrigger 打勾，零代码。
///   纸牌格这种"事件来自子物体按钮"的：autoTrigger 不勾，由宿主脚本
///       拿到本组件后调 SetHover(true / false)
///   缩放目标用 target 指定（留空 = 自己）。**务必指向一个「轴心在正中、尺寸铺满 item」的节点**，
///   这样才是从中心放大；指向根节点会因为根节点轴心在左上角而朝右下长。
///
/// 层级 / 裁切：
///   只缩放 + 悬停时把自己提到同级最后一位（同级里靠后的画在上层，所以不会被相邻的牌盖住）：
///     - 不改父子关系、不改锚点位置，所以父级是 LayoutGroup 还是 ScrollList 手动定位都不受影响
///       （布局只看 sizeDelta 和 anchoredPosition，不看 localScale）；
///     - 放大后超出 ScrollRect 视口的部分会被 Mask 裁掉 —— 预期行为，不做任何脱离 Mask 的操作；
///     - 直接父级挂着 LayoutGroup 时不提层（改同级顺序会让布局重排）。
///
/// 池化：OnDisable 会立刻（不播动画）复位缩放与层级，回收 / 面板关闭都不会留残留状态。
/// </summary>
public class HoverScaleBehaviour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("缩放")]
    /// <summary>缩放目标（留空 = 自己）；应指向轴心居中的包裹节点</summary>
    [SerializeField] private RectTransform target;

    /// <summary>悬停时的放大倍数（每个预制体各调各的）</summary>
    [SerializeField] private float hoverScale = 1.12f;

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

    // ==================== 对外 ====================

    /// <summary>放大倍数（提示框按"放大后的尺寸"算间距时要用）</summary>
    public float HoverScale => hoverScale;

    /// <summary>当前是否处于悬停放大状态</summary>
    public bool IsHovering { get; private set; }

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

        KillTween();

        if (immediate)
        {
            _rect.localScale = on ? _baseScale * hoverScale : _baseScale;
            if (!on) RestoreSibling();
            return;
        }

        var to = on ? _baseScale * hoverScale : _baseScale;
        _scaleTween = AnimationHelper.ScaleTo(_rect, to, duration, ease).SetUpdate(useUnscaledTime);

        // 缩回去的过程里还要继续盖着邻居，所以等补间播完再还同级顺序
        if (!on) _scaleTween.OnComplete(RestoreSibling);
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
    }

    /// <summary>被失活（池化回收 / 面板关闭）时立刻复位，不留缩放残留和层级残留</summary>
    private void OnDisable()
    {
        KillTween();

        if (_rect != null) _rect.localScale = _baseScale;

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

    // ==================== 内部 ====================

    private RectTransform _rect;

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
