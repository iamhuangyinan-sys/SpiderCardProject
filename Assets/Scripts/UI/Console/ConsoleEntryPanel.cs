using Framework.UI;

/// <summary>
/// 控制台入口面板 —— 点击按钮打开控制台面板，自身隐藏
/// 在编辑器和测试包中由各场景入口显示
/// </summary>
public partial class ConsoleEntryPanel : NormalPanel
{
#if UNITY_EDITOR || SPIDERCARD_TEST_BUILD
    /// <summary>显示控制台入口</summary>
    public static void ShowEntry()
    {
        UIManager.Instance.Show<ConsoleEntryPanel>();
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
#else
    protected override void OnOpen() { }
    protected override void OnShow() { }
    protected override void OnHide() { }
    protected override void OnClose() { }
#endif
}
