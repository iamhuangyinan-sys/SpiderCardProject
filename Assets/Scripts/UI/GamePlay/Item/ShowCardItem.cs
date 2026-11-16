using System.Collections.Generic;
using Framework.UI;

/// <summary>
/// 展示用的单张牌 —— 只显示牌图（沉底牌显示黄色）；鼠标悬停放大并显示 tip
/// 点击行为由 Bind 的 onClick 决定：
///   选牌模式（ShowCardsPanel.PickCard）→ 点一下算选中
///   纯展示模式 → 点一下打开单张详情（ShowOneCardPanel）
///   详情面板里 → 传 null，不可点击
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

    /// <summary>按牌定义 + 属性显示牌图（有 Sunk 属性会染黄）并设置点击回调</summary>
    /// <param name="onClick">点击回调；null = 不可点击（详情面板等纯展示场合）</param>
    public void Bind(CardDef def, IReadOnlyList<E_CardAttributeEnum> attributes = null,
        bool useInGameAttributes = false, System.Action onClick = null)
    {
        comps.modCard.Bind(def, attributes, useInGameAttributes);
        comps.modCard.SetClickable(onClick);
    }

    /// <summary>设置点击回调；传 null 表示不可点击（删牌选牌等场合用）</summary>
    public void SetClickable(System.Action onClick)
    {
        comps.modCard.SetClickable(onClick);
    }
}
