using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 配表测试 —— 挂场景 GameObject 上，按 1 输出 TestConfig 数据
/// </summary>
public class ConfigTest : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            var all = ConfigHelper.GetAll<TestConfig>();
            Debug.Log($"<color=cyan>===== TestConfig 共 {all.Count} 条 =====</color>");

            foreach (var cfg in all)
            {
                Debug.Log($"  Id={cfg.Id}  Name={cfg.Name}  Level={cfg.Level}  Attack={cfg.Attack}");
            }
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            var cfg = ConfigHelper.Get<TestConfig>(1);
            if (cfg != null)
                Debug.Log($"<color=cyan>单条查询 Id=1: Name={cfg.Name}  Level={cfg.Level}  Attack={cfg.Attack}</color>");
            else
                Debug.LogWarning("Id=1 不存在");
        }
    }
}
