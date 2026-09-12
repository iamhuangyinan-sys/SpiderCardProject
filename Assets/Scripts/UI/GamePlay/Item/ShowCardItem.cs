using Framework.UI;
using UnityEngine;

/// <summary>
/// 展示用的单张牌 —— 只显示牌图（沉底牌显示黄色）
/// </summary>
public partial class ShowCardItem : BaseListItem
{
    /// <summary>沉底牌的显示颜色（黄色）</summary>
    private static readonly Color SunkColor = new Color(1f, 0.85f, 0.25f, 1f);

    public override void OnActivate() { }

    public override void OnRecycle()
    {
        comps.imgCard.sprite = null;
        comps.imgCard.color = Color.white;   // 复用前复位，避免黄色残留到别的牌上
    }

    /// <summary>按牌 id 显示牌图（isSunk = 沉底牌，染黄）</summary>
    public void Bind(string cardId, bool isSunk = false)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        var cfg = ConfigHelper.Get<CardConfig>(cardId);
        if (cfg == null) return;

        comps.imgCard.sprite = CardResManager.Instance.GetCardImage(cfg.Image);
        comps.imgCard.color = isSunk ? SunkColor : Color.white;
    }
}
