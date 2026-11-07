using Framework.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 悬停提示里的单条描述（一个框）。标题可空，正文自动换行、框高自适应。
///
/// 预制体约定：
///   根节点        由本类同步为 img_Layout 的尺寸，供外层提示列表排列
///   img_Layout    框底 + ContentSizeFitter（双向 PreferredSize）+ VerticalLayoutGroup
///   img_Shadow    与 img_Layout 同级，拉伸跟随根节点，不参与内容尺寸计算
///   Title         标题行（HorizontalLayoutGroup），下面挂图标 + 标题字
///   img_Icon      标题行左侧的图标（可空，属性描述用）
///   txt_Title     标题（可空）
///   txt_Content   正文（宽度由本类写入，高度由 ContentSizeFitter 按换行自动撑）
/// </summary>
public partial class HoverTipItemModule : BaseModule
{
    /// <summary>正文单行最大宽度（像素，不含框内边距）—— 超过就自动换行</summary>
    [SerializeField] private float maxContentWidth = 420f;

    [Tooltip("影子相对框体的偏移（Canvas 单位，右下方为正 X、负 Y）")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(6f, -6f);

    [Tooltip("影子在框体四周各扩出的尺寸（Canvas 单位）")]
    [SerializeField] private Vector2 shadowPadding = Vector2.zero;

    /// <summary>标题行（图标 + 标题的父节点，从 txt_Title 往上取）</summary>
    private GameObject _titleRow;

    protected override void OnBind()
    {
        _titleRow = comps.txtTitle != null && comps.txtTitle.transform.parent != null
            ? comps.txtTitle.transform.parent.gameObject
            : null;

        // 外层排列的是模组根节点，实际可见框必须与根节点重合。
        if (comps.imgLayout != null)
        {
            var layoutRect = comps.imgLayout.rectTransform;
            layoutRect.anchorMin = layoutRect.anchorMax = new Vector2(0.5f, 0.5f);
            layoutRect.pivot = new Vector2(0.5f, 0.5f);
            layoutRect.anchoredPosition = Vector2.zero;
            comps.imgLayout.raycastTarget = false;
        }

        if (comps.imgShadow != null)
        {
            // 保留预制体配置的影子图片、颜色和透明度，只接管尺寸与偏移。
            var shadowRect = comps.imgShadow.rectTransform;
            shadowRect.SetAsFirstSibling();
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.anchoredPosition = shadowOffset;
            shadowRect.sizeDelta = shadowPadding * 2f;
            comps.imgShadow.raycastTarget = false;
        }
    }

    protected override void OnUnBind() { }

    /// <summary>
    /// 设置一条描述。返回正文实际占用的宽度（面板用它把所有框统一成同一宽度）。
    /// 标题为空时隐藏标题节点（只清文字的话 TMP 空文本仍占一行高度）
    /// </summary>
    /// <param name="title">标题（可空）</param>
    /// <param name="content">正文（可空）</param>
    /// <param name="icon">标题行左侧的图标（null = 不显示）</param>
    public float SetTip(string title, string content, Sprite icon = null)
    {
        bool hasTitle = !string.IsNullOrEmpty(title);
        bool hasIcon = icon != null;

        // 标题行：图标和标题都没有时整行隐藏，否则会留一条控件高度的空带
        if (_titleRow != null) _titleRow.SetActive(hasTitle || hasIcon);

        if (comps.imgIcon != null)
        {
            comps.imgIcon.sprite = icon;   // 没图标时置空，避免复用到上一条的图
            comps.imgIcon.gameObject.SetActive(hasIcon);
        }

        if (comps.txtTitle != null)
        {
            comps.txtTitle.gameObject.SetActive(hasTitle);

            if (hasTitle)
            {
                comps.txtTitle.text = title;
                comps.txtTitle.ForceMeshUpdate();   // 立刻刷新标题的 preferredWidth（框宽依赖它）
            }
        }

        if (comps.txtContent == null) return 0f;

        bool hasContent = !string.IsNullOrEmpty(content);
        comps.txtContent.gameObject.SetActive(hasContent);
        if (!hasContent) return 0f;

        comps.txtContent.text = content;

        // 宽度取「按 maxContentWidth 排布后实际需要的宽度」：
        //   短文本 → 文字本身的宽度（框贴着字）
        //   超长   → 封顶在 maxContentWidth，此时 TMP 会自动换行
        float width = comps.txtContent.GetPreferredValues(content, maxContentWidth, 0f).x;
        SetContentWidth(width);

        return width;
    }

    /// <summary>统一正文宽度（面板取所有条目的最大宽度后调这个，实现“等宽框”）</summary>
    public void SetContentWidth(float width)
    {
        if (comps.txtContent == null) return;

        comps.txtContent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        // 尺寸变了要让 TMP 立刻按新宽度重排，否则紧接着读到的 preferredHeight 还是旧宽度下的
        comps.txtContent.ForceMeshUpdate();
    }

    /// <summary>正文宽度统一后，自下而上完成布局，再把可见框尺寸同步到模组根节点。</summary>
    public void RebuildLayout()
    {
        if (comps.imgLayout == null) return;

        // 根节点没有布局控制器，直接重建外层列表不会进入 img_Layout 子树。
        // 先完成文字、标题行各自的 ContentSizeFitter，避免父布局读到复用前的旧尺寸。
        if (comps.txtTitle != null && comps.txtTitle.gameObject.activeSelf)
            LayoutRebuilder.ForceRebuildLayoutImmediate(comps.txtTitle.rectTransform);
        if (_titleRow != null && _titleRow.activeSelf)
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_titleRow.transform);
        if (comps.txtContent != null && comps.txtContent.gameObject.activeSelf)
            LayoutRebuilder.ForceRebuildLayoutImmediate(comps.txtContent.rectTransform);

        var layoutRect = comps.imgLayout.rectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);

        // 外层 VerticalLayoutGroup 不控制条目尺寸，按这里的实际宽高排列。
        // 影子通过拉伸锚点跟随这个尺寸，不把偏移/扩边计入框体或条目间距。
        var itemRect = (RectTransform)transform;
        itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layoutRect.rect.width);
        itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layoutRect.rect.height);
    }
}
