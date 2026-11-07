using System.Collections.Generic;
using Framework.UI;

/// <summary>
/// 展示用的单张牌 —— 只显示牌图（沉底牌显示黄色）；默认不可点击，鼠标悬停放大
/// 选牌模式（ShopCardsPanel.PickCard）下会被挂上点击回调
/// </summary>
public partial class ShowCardItem : BaseListItem
{
    /// <summary>由牌面悬停驱动；展示牌即使不可点击，仍可缩放并显示提示。</summary>
    private UIFeedbackController _feedback;

    public override void OnActivate()
    {
        if (_feedback == null) _feedback = GetComponent<UIFeedbackController>();
        _feedback?.ResetFeedback();

        comps.modCard.OnHoverChanged = OnCardHover;
    }

    public override void OnRecycle()
    {
        comps.modCard.OnHoverChanged = null;
        _feedback?.ResetFeedback();
        comps.modCard.Clear();
    }

    /// <summary>牌面悬停变化 → 整个格子缩放</summary>
    private void OnCardHover(bool hovering) => _feedback?.SetHovered(hovering);

    /// <summary>按牌定义 + 属性显示牌图（有 Sunk 属性会染黄）</summary>
    public void Bind(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes = null)
    {
        comps.modCard.Bind(def, attributes);
    }

    /// <summary>设置点击回调；传 null 表示不可点击（删牌选牌等场合用）</summary>
    public void SetClickable(System.Action onClick)
    {
        comps.modCard.SetClickable(onClick);
    }
}
