using Framework.UI;

/// <summary>
/// 提示工具 —— 业务层弹提示的统一入口（面板的创建 / 复用对业务透明）
///
/// 用法：TipHelper.Show(LanguageHelper.Get("tip_deal_empty_column"));
/// 文案统一放 LanguageConfig 表，这里只负责把字串显示出来。
/// </summary>
public static class TipHelper
{
    /// <summary>弹一条提示（默认停留 1 秒后自动淡出）</summary>
    public static void Show(string content)
    {
        var panel = UIManager.Instance.Show<TipPanel>();
        if (panel == null) return;

        panel.ShowTip(content);
    }
}
