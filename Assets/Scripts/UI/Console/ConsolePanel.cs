using Framework.UI;
using UnityEngine;

/// <summary>
/// 控制台面板 —— 输入命令行，点确定（或回车）执行
/// 关闭时重新显示 ConsoleEntryPanel
/// </summary>
public partial class ConsolePanel : NormalPanel
{
    protected override void OnOpen()
    {
        comps.btnOk.onClick.AddListener(OnClickOk);
        comps.btnClose.onClick.AddListener(OnClickClose);
        comps.iptConsole.onSubmit.AddListener(OnSubmit);
    }

    protected override void OnShow()
    {
        comps.iptConsole.text = string.Empty;
        comps.iptConsole.ActivateInputField();
    }

    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnOk.onClick.RemoveListener(OnClickOk);
        comps.btnClose.onClick.RemoveListener(OnClickClose);
        comps.iptConsole.onSubmit.RemoveListener(OnSubmit);
    }

    /// <summary>提交输入框（回车）</summary>
    private void OnSubmit(string text)
    {
        ExecuteCurrentInput();
    }

    /// <summary>点击确定</summary>
    private void OnClickOk()
    {
        ExecuteCurrentInput();
    }

    /// <summary>执行输入框中的命令，并清空输入框保持焦点</summary>
    private void ExecuteCurrentInput()
    {
        string input = comps.iptConsole.text;
        if (string.IsNullOrWhiteSpace(input)) return;

        comps.iptConsole.text = string.Empty;
        comps.iptConsole.ActivateInputField();

        if (ConsoleCommandManager.Instance.Execute(input, out string output))
        {
            Debug.Log($"[Console] > {input}\n{output}");
        }
        else
        {
            Debug.LogWarning($"[Console] 未知命令: {input}");
        }
    }

    /// <summary>关闭控制台：隐藏自身，重新显示入口</summary>
    private void OnClickClose()
    {
        UIManager.Instance.Hide(this);
        UIManager.Instance.Show<ConsoleEntryPanel>();
    }
}
