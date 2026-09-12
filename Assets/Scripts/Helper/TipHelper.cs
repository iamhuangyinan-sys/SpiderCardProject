using Framework.UI;

/// <summary>
/// 提示工具 —— 业务层弹提示的统一入口（面板的创建 / 复用对业务透明）
///
/// 用法：TipHelper.Show("有空列时不能发牌");
/// 后续接提示表 / 语言表时，只需把这里的 string 换成「查表得到文案」，业务调用点不用改。
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
