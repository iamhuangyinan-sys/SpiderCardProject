using UnityEngine;

/// <summary>
/// 纸牌游戏场景入口 —— 挂载在 CardGameScene 场景的 GameObject 上
/// 场景加载完成后初始化纸牌游戏系统
/// （框架由 Main 场景的 FrameworkEntry 初始化并 DontDestroyOnLoad 保留）
/// </summary>
public class CardGameEntry : MonoBehaviour
{
    private void Start()
    {
        CardGameModule.Instance.Init();
    }

    private void OnDestroy()
    {
        CardGameModule.Instance.Dispose();
    }
}
