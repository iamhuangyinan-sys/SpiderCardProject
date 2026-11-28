using Framework.UI;
using UnityEngine;

/// <summary>悬停提示中的关联牌。根节点尺寸等于放大后的可见牌面，供外层牌列排列。</summary>
public partial class HoverTipCardItemModule : BaseModule
{
    protected override void OnBind()
    {
        var cardRect = (RectTransform)comps.modCard.transform;
        var slot = (RectTransform)transform;
        slot.sizeDelta = Vector2.Scale(cardRect.rect.size, (Vector2)cardRect.localScale);
    }

    protected override void OnUnBind() { }

    public void SetCard(CardDef def) => comps.modCard.Bind(def);

    internal Rect GetShadowOutsets()
    {
        if (comps.imgShadow == null || !comps.imgShadow.isActiveAndEnabled) return default;

        Rect face = ((RectTransform)transform).rect;
        Bounds shadow = RectTransformUtility.CalculateRelativeRectTransformBounds(transform, comps.imgShadow.rectTransform);
        Vector2 min = Vector2.Min(Vector2.zero, (Vector2)shadow.min - face.min);
        Vector2 max = Vector2.Max(Vector2.zero, (Vector2)shadow.max - face.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
