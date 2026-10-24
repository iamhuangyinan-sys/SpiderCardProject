using Framework.UI;
using UnityEngine;

/// <summary>
/// 悬停提示里的单条描述（一个框）。标题可空，正文自动换行、框高自适应。
///
/// 预制体约定：
///   根节点        Image（框底）+ ContentSizeFitter（双向 PreferredSize）+ VerticalLayoutGroup
///   Title         标题行（HorizontalLayoutGroup），下面挂图标 + 标题字
///   img_Icon      标题行左侧的图标（可空，属性描述用）
///   txt_Title     标题（可空）
///   txt_Content   正文（宽度由本类写入，高度由 ContentSizeFitter 按换行自动撑）
/// </summary>
public partial class HoverTipItemModule : BaseModule
{
    /// <summary>正文单行最大宽度（像素，不含框内边距）—— 超过就自动换行</summary>
    [SerializeField] private float maxContentWidth = 420f;

    /// <summary>标题行（图标 + 标题的父节点，从 txt_Title 往上取）</summary>
    private GameObject _titleRow;

    protected override void OnBind()
    {
        _titleRow = comps.txtTitle != null && comps.txtTitle.transform.parent != null
            ? comps.txtTitle.transform.parent.gameObject
            : null;
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
}
