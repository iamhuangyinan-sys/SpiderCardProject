using System;
using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 纸牌展示模组 —— 牌面渲染 + 牌 id 记录 + 悬停提示
///
/// 用法：任何需要显示一张牌的地方塞这个模组即可
///   可点击的（商店格）：Bind(id) 后调 SetClickable(回调)
///   不可点击的（展示牌）：只调 Bind(id)，按钮组件会被关掉
/// 显示走模组内按钮的 Image（btn_Card.image），不额外挂图
/// 悬停提示的方向在各宿主预制体上配（商店格 Down、展示牌 Right 之类）
/// </summary>
public partial class CardModule : BaseModule, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>沉底牌的显示颜色（黄色）</summary>
    private static readonly Color SunkColor = new Color(1f, 0.85f, 0.25f, 1f);

    /// <summary>悬停提示的首选方向（放不下会自动换方向）</summary>
    [SerializeField] private E_TipDirection tipDirection = E_TipDirection.Down;

    /// <summary>提示框额外让出的距离（像素，默认 0；个别牌想再远一点就在预制体上调）</summary>
    [SerializeField] private float tipExtraGap = 0f;

    /// <summary>当前绑定的牌 id（空 = 未绑定）</summary>
    public string CardId { get; private set; }

    /// <summary>悬停状态变化（true = 进入 / false = 离开）—— 宿主用来做整体缩放等表现</summary>
    public Action<bool> OnHoverChanged;

    /// <summary>点击回调（null = 不响应点击）</summary>
    private Action _onClick;

    /// <summary>宿主的悬停缩放（用来把“放大后的尺寸”算进提示框间距；没有就按 1 倍算）</summary>
    private HoverScaleBehaviour _hoverScale;

    /// <summary>当前是否处于悬停中（只在变化时广播，避免重复驱动缩放）</summary>
    private bool _hovering;

    protected override void OnBind()
    {
        SetClickable(null);   // 默认不可点击，需要点击的宿主自己开启
        _hoverScale = GetComponentInParent<HoverScaleBehaviour>();
    }

    protected override void OnUnBind()
    {
        NotifyHover(false);
        HideTip();
        Clear();
    }

    // ==================== 对外 ====================

    /// <summary>绑定一张牌：记录 id + 按配表 Image 渲染牌面</summary>
    /// <param name="cardId">牌 id（配表主键）</param>
    /// <param name="isSunk">是否沉底牌（染黄）</param>
    public void Bind(string cardId, bool isSunk = false)
    {
        CardId = cardId;
        RefreshFace(isSunk);
    }

    /// <summary>清空显示与点击（item 回收时调）</summary>
    public void Clear()
    {
        CardId = null;
        SetClickable(null);
        RefreshFace();
    }

    /// <summary>设置点击回调；传 null 表示不可点击（按钮组件会被直接关掉）</summary>
    public void SetClickable(Action onClick)
    {
        _onClick = onClick;

        if (comps.btnCard == null) return;

        comps.btnCard.onClick.RemoveListener(OnClickCard);
        comps.btnCard.enabled = onClick != null;

        if (onClick != null)
        {
            comps.btnCard.onClick.AddListener(OnClickCard);
        }
    }

    /// <summary>收起提示（只有自己的提示才会被关）</summary>
    public void HideTip()
    {
        HoverTipHelper.Hide(this);
    }

    // ==================== 悬停 ====================

    public void OnPointerEnter(PointerEventData eventData)
    {
        NotifyHover(true);
        ShowTip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        NotifyHover(false);
        HideTip();
    }

    /// <summary>被失活（列表回收 / 面板关闭）时收起提示，避免提示残留在屏幕上</summary>
    private void OnDisable()
    {
        NotifyHover(false);   // 失活时补一次“离开”，宿主正好把缩放收掉
        HideTip();
    }

    /// <summary>广播悬停状态（只在变化时发）</summary>
    private void NotifyHover(bool hovering)
    {
        if (_hovering == hovering) return;

        _hovering = hovering;
        OnHoverChanged?.Invoke(hovering);
    }

    private void ShowTip()
    {
        var cfg = string.IsNullOrEmpty(CardId) ? null : ConfigHelper.Get<CardConfig>(CardId);
        if (cfg == null) return;

        // 对齐目标用「可见牌面」：模组根节点的 rect 不一定等于牌的实际大小
        var target = comps.btnCard != null && comps.btnCard.image != null
            ? comps.btnCard.image.rectTransform
            : (RectTransform)transform;

        // 宿主悬停时会整体放大 → 让提示框按「放大后的尺寸」定位，间距才不会被放大吃掉
        float targetScale = _hoverScale != null ? _hoverScale.HoverScale : 1f;

        HoverTipHelper.Show(this, cfg.Name, cfg.Description, target, tipDirection, targetScale, tipExtraGap);
    }

    // ==================== 内部 ====================

    /// <summary>按当前 CardId 刷新牌面（无牌时清空；沉底牌染黄，其余复位白色）</summary>
    private void RefreshFace(bool isSunk = false)
    {
        if (comps.btnCard == null) return;

        var img = comps.btnCard.image;
        if (img == null) return;

        var cfg = string.IsNullOrEmpty(CardId) ? null : ConfigHelper.Get<CardConfig>(CardId);

        img.sprite = cfg != null ? CardResManager.Instance.GetCardImage(cfg.Image) : null;
        img.color = isSunk ? SunkColor : Color.white;
    }

    private void OnClickCard()
    {
        _onClick?.Invoke();
    }
}
