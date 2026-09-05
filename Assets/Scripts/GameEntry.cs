using Framework.UI;
using UnityEngine;

/// <summary>
/// 游戏入口 —— 在框架初始化完成后执行游戏业务初始化
/// 挂载到场景中的 GameObject 上（与 FrameworkEntry 同级）
///
/// Unity 生命周期保证：所有 Awake 先于 Start 执行，
/// 因此 Start 时框架（FrameworkEntry.InitFramework）已初始化完毕。
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
        UIManager.Instance.ShowAsync<BeginPanel>(_ => { });

        Debug.Log("[GameEntry] 游戏业务初始化完成");
    }
}
