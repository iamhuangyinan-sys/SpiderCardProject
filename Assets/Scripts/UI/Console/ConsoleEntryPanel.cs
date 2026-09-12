using Framework.UI;

/// <summary>
/// 控制台入口面板 —— 点击按钮打开控制台面板，自身隐藏
/// 仅编辑器模式下会显示（由各场景入口调 ShowInEditor）
/// </summary>
public partial class ConsoleEntryPanel : NormalPanel
{
    /// <summary>编辑器模式下显示控制台入口（各场景入口调用；非编辑器下为空实现）</summary>
    public static void ShowInEditor()
    {
#if UNITY_EDITOR
        UIManager.Instance.Show<ConsoleEntryPanel>();
#endif
    }

    protected override void OnOpen()
    {
        comps.btnConsoleEntry.onClick.AddListener(OnClickEntry);
    }

    protected override void OnShow() { }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnConsoleEntry.onClick.RemoveListener(OnClickEntry);
    }

    /// <summary>打开控制台：隐藏入口，显示控制台面板</summary>
    private void OnClickEntry()
    {
        UIManager.Instance.Hide(this);
        UIManager.Instance.Show<ConsolePanel>();
    }
}
