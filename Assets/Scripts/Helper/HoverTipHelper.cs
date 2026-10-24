using System.Collections.Generic;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 悬停提示工具 —— 业务层显示 / 隐藏触发式提示框的统一入口
///
/// 用法：
///   HoverTipHelper.Show(this, "牌名", "描述", targetRect, E_TipDirection.Down);
///   HoverTipHelper.Hide(this);
///
/// owner 一般传调用者自己（如 CardModule），用来避免多个 item 互相抢提示：
/// 只有当前持有者能关掉提示，不会出现"A 移出时把 B 刚显示的提示关掉"
/// </summary>
public static class HoverTipHelper
{
    /// <summary>当前提示框的持有者（null = 当前没显示）</summary>
    private static object _owner;

    /// <summary>显示提示（标题与正文都为空时直接收起）</summary>
    /// <param name="owner">调用者（谁显示谁才能关）</param>
    /// <param name="title">标题（可空）</param>
    /// <param name="content">正文（可空）</param>
    /// <param name="target">要对齐的目标</param>
    /// <param name="preferred">首选方向</param>
    /// <param name="targetScale">目标最终会放大到多少倍（悬停放大时用，保证间距不被放大吃掉）</param>
    /// <param name="extraGap">额外让出的距离（像素）</param>
    public static void Show(object owner, string title, string content, RectTransform target,
        E_TipDirection preferred, float targetScale = 1f, float extraGap = 0f)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
        {
            Hide(owner);
            return;
        }

        var panel = UIManager.Instance.Show<HoverTipPanel>();
        if (panel == null) return;

        _owner = owner;
        panel.ShowTip(title, content, target, preferred, targetScale, extraGap);
    }

    /// <summary>显示一组描述（每条一个框）；没有可显示的内容时直接收起</summary>
    /// <param name="owner">调用者（谁显示谁才能关）</param>
    /// <param name="tips">每条描述（空条目会被跳过）</param>
    public static void Show(object owner, IReadOnlyList<HoverTipData> tips, RectTransform target,
        E_TipDirection preferred, float targetScale = 1f, float extraGap = 0f)
    {
        if (!HasAny(tips))
        {
            Hide(owner);
            return;
        }

        var panel = UIManager.Instance.Show<HoverTipPanel>();
        if (panel == null) return;

        _owner = owner;
        panel.ShowTips(tips, target, preferred, targetScale, extraGap);
    }

    /// <summary>列表里是否至少有一条有内容</summary>
    private static bool HasAny(IReadOnlyList<HoverTipData> tips)
    {
        if (tips == null) return false;

        for (int i = 0; i < tips.Count; i++)
        {
            if (tips[i].HasAny) return true;
        }

        return false;
    }

    /// <summary>收起提示（只有当前持有者能关）</summary>
    public static void Hide(object owner)
    {
        if (!ReferenceEquals(_owner, owner)) return;

        _owner = null;

        var panel = UIManager.Instance.GetPanel<HoverTipPanel>();
        if (panel != null && panel.IsOpen)
        {
            UIManager.Instance.Hide(panel);
        }
    }
}

/// <summary>
/// 一条悬停提示的内容 —— 每条渲染成一个 HoverTipItemModule 框（标题可空）。
/// 描述从哪来（配表 / 数据属性推导）由业务层决定，这里只管"要显示什么"
/// </summary>
public struct HoverTipData
{
    /// <summary>标题（可空；为空时这条不显示标题字）</summary>
    public string Title;

    /// <summary>正文（可空）</summary>
    public string Content;

    /// <summary>标题行左侧的图标（null = 不显示；目前只有牌属性描述用）</summary>
    public Sprite Icon;

    /// <summary>标题 / 正文 / 图标至少有一个（全空 = 这条不用显示）</summary>
    public bool HasAny => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Content) || Icon != null;

    public HoverTipData(string title, string content, Sprite icon = null)
    {
        Title = title;
        Content = content;
        Icon = icon;
    }
}
