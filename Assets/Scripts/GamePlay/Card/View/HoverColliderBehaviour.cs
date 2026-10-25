using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 世界空间悬停表现 —— 射线命中自己（或子物体）的 Collider 时放大 / 显示 Glow，移出还原。
///
/// 用法（发牌堆 / 牌堆信息图标这类场景物体）：
///   物体上要有 Collider；挂上本组件即可，两个效果都可以单独关掉：
///     缩放：Hover Scale = 1 就是不缩放（默认）
///     Glow：Glow Renderer 留空时自动找子节点里名字等于 Glow Node Name 的 SpriteRenderer
///
/// 判定规则（防止“指着前面的小图标、后面的大图也跟着亮”）：
///   只取**沿射线最近的**那个 Collider，然后从它往上找第一个 HoverColliderBehaviour，
///   找到的是自己才算悬停。所以：
///     指着自己 / 自己没挂本组件的装饰子物体 → 算悬停
///     子物体自己也挂了本组件且挡在前面 → 只算那个子物体悬停，自己不变
///
/// 触发时机：
///   只在 `CardGameModule.CanCardInput`（局内 && 非结算 && 非动画 && 无全屏面板）时响应；
///   鼠标没移动时不重复打射线（静止零开销），输入被锁时立刻视为未悬停。
/// </summary>
public class HoverColliderBehaviour : MonoBehaviour
{
    [Header("缩放（可选）")]
    /// <summary>缩放目标（留空 = 自己）</summary>
    [SerializeField] private Transform scaleTarget;

    /// <summary>悬停放大倍数（1 = 不缩放）</summary>
    [SerializeField] private float hoverScale = 1f;

    /// <summary>缩放时长（秒）</summary>
    [SerializeField] private float scaleDuration = 0.12f;

    /// <summary>缩放缓动</summary>
    [SerializeField] private Ease scaleEase = Ease.OutQuad;

    [Header("Glow（可选）")]
    /// <summary>Glow 的 SpriteRenderer（留空 = 按名字找子节点）</summary>
    [SerializeField] private SpriteRenderer glowRenderer;

    /// <summary>按名字找 Glow 节点用的名字</summary>
    [SerializeField] private string glowNodeName = "Glow";

    /// <summary>Glow 淡入 / 淡出时长（秒）</summary>
    [SerializeField] private float glowFadeDuration = 0.12f;

    /// <summary>当前是否悬停中（其它系统要联动可以读这个）</summary>
    public bool IsHovering => _hovering;

    private Camera _camera;
    private Transform _scaleNode;
    private Vector3 _baseScale;

    private Color _glowColor;
    private Tween _scaleTween;

    private bool _hovering;
    private bool _hasMousePos;
    private Vector2 _lastMousePos;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        _camera = Camera.main;

        _scaleNode = scaleTarget != null ? scaleTarget : transform;
        _baseScale = _scaleNode.localScale;

        if (glowRenderer == null) glowRenderer = FindGlowRenderer();
        if (glowRenderer != null)
        {
            _glowColor = glowRenderer.color;
            glowRenderer.gameObject.SetActive(false);   // 默认不显示，等悬停
        }

        if (GetComponentInChildren<Collider>(true) == null)
        {
            Debug.LogWarning($"[HoverColliderBehaviour] {name} 及其子物体上都没有 Collider，悬停永远触发不了", this);
        }
    }

    /// <summary>失活 / 回收时立刻复位，不留缩放与 Glow 残留</summary>
    private void OnDisable()
    {
        _scaleTween?.Kill();
        _scaleTween = null;

        if (_scaleNode != null) _scaleNode.localScale = _baseScale;

        if (glowRenderer != null)
        {
            glowRenderer.DOKill();
            glowRenderer.color = _glowColor;

            if (glowRenderer.gameObject != null) glowRenderer.gameObject.SetActive(false);
        }

        _hovering = false;
        _hasMousePos = false;
    }

    // ==================== 悬停检测 ====================

    private void Update()
    {
        // 输入被锁（面板遮挡 / 收牌结算 / 发牌动画）→ 一律当作未悬停
        bool canHover = CardGameModule.Instance != null && CardGameModule.Instance.CanCardInput;
        if (!canHover)
        {
            if (_hovering) SetHovering(false);

            _hasMousePos = false;   // 解锁后重新算一次
            return;
        }

        if (Mouse.current == null || _camera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 鼠标没动 → 悬停结果不会变，省掉射线
        if (_hasMousePos && mousePos == _lastMousePos) return;

        _lastMousePos = mousePos;
        _hasMousePos = true;

        bool hovering = IsPointerOverSelf(mousePos);
        if (hovering != _hovering) SetHovering(hovering);
    }

    /// <summary>
    /// 只认「最前面那个」命中的 Collider：从它往上找最近的 HoverColliderBehaviour，
    /// 找到的是自己才算悬停 —— 指着 i 图标（自己也挂了本组件、且 z 更靠前）时
    /// 不会再连下面的牌堆一起亮；指着没挂本组件的装饰子物体则仍算悬停自己。
    /// </summary>
    private bool IsPointerOverSelf(Vector2 screenPos)
    {
        var ray = _camera.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out var hit, 200f)) return false;
        if (hit.collider == null) return false;

        var owner = hit.collider.GetComponentInParent<HoverColliderBehaviour>();
        return owner == this;
    }

    private void SetHovering(bool on)
    {
        _hovering = on;

        ApplyScale(on);
        ApplyGlow(on);
    }

    // ==================== 表现 ====================

    private void ApplyScale(bool on)
    {
        if (_scaleNode == null) return;

        _scaleTween?.Kill();
        _scaleTween = null;

        Vector3 to = on ? _baseScale * hoverScale : _baseScale;
        if ((_scaleNode.localScale - to).sqrMagnitude < 0.000001f) return;

        _scaleTween = _scaleNode.DOScale(to, scaleDuration).SetEase(scaleEase).SetUpdate(true);
    }

    private void ApplyGlow(bool on)
    {
        if (glowRenderer == null) return;

        glowRenderer.DOKill();

        if (on)
        {
            glowRenderer.gameObject.SetActive(true);

            var color = _glowColor;
            color.a = 0f;
            glowRenderer.color = color;

            glowRenderer.DOFade(_glowColor.a, glowFadeDuration).SetUpdate(true);
            return;
        }

        glowRenderer.DOFade(0f, glowFadeDuration).SetUpdate(true).OnComplete(() =>
        {
            if (!_hovering && glowRenderer != null && glowRenderer.gameObject != null)
            {
                glowRenderer.gameObject.SetActive(false);
            }
        });
    }

    /// <summary>按名字找 Glow 的 SpriteRenderer（含失活子物体）</summary>
    private SpriteRenderer FindGlowRenderer()
    {
        if (string.IsNullOrEmpty(glowNodeName)) return null;

        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.gameObject.name == glowNodeName) return renderer;
        }

        return null;
    }
}
