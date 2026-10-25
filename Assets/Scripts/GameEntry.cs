using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 主菜单场景入口 —— 挂载在 BeginPanelScene 的 GameObject 上
///
/// 框架（FrameworkEntry）在启动场景 Main 初始化完毕后常驻，
/// 本场景每次进入只需要打开开始界面；返回主菜单时也会重新执行这里。
/// </summary>
public class GameEntry : MonoBehaviour
{
    private void Start()
    {
        InitGame();
    }

    private void InitGame()
    {
        Debug.Log("[GameEntry] 游戏业务初始化开始...");

        // ===== 1. 打开开始界面 =====
        UIManager.Instance.ShowAsync<BeginPanel>(_ =>
        {
            // 内容就绪：告知场景控制器可以淡出黑屏遮罩
            SceneController.Instance.NotifySceneReady();
        });

        // ===== 2. 编辑器模式：控制台入口 =====
        ConsoleEntryPanel.ShowInEditor();

        Debug.Log("[GameEntry] 游戏业务初始化完成");
    }
}
