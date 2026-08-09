using UnityEngine;
using UnityEngine.InputSystem;
using Framework.UI;

/// <summary>
/// UI 测试 —— 挂场景 GameObject 上
/// 按 1 打开 TestPanel，按 2 关闭 TestPanel，按 3 关闭栈顶弹窗
/// </summary>
public class UITest : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current == null) return;

        // 按 1：显示 TestPanel
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            UIManager.Instance.Show<TestPanel>();
        }

        // 按 2：隐藏 TestPanel
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            var panel = UIManager.Instance.GetPanel<TestPanel>();
            if (panel != null)
                panel.HideSelf();
        }

        // 按 3：关闭 TestPanel（销毁）
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            var panel = UIManager.Instance.GetPanel<TestPanel>();
            if (panel != null)
                panel.CloseSelf();
        }

        // 按 4：关闭栈顶弹窗
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            UIManager.Instance.CloseTopPopup();
        }
    }
}
