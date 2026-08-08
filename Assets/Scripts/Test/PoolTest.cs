using UnityEngine;
using UnityEngine.InputSystem;
using Framework.Pool;

/// <summary>
/// 对象池测试 —— 挂场景 GameObject 上
/// 按 1 生成 Cube，按 2 回收所有 Cube
/// </summary>
public class PoolTest : MonoBehaviour
{
    private int _count;

    void Update()
    {
        if (Keyboard.current == null) return;

        // 按 1：生成一个 Cube
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            PoolManager.Instance.SpawnAsync("Test/Cube", cube =>
            {
                if (cube == null) return;
                cube.transform.position = Random.insideUnitSphere * 5f;
                _count++;
                Debug.Log($"<color=green>[PoolTest] 生成 Cube #{_count}</color>");
            });
        }

        // 按 2：回收所有 Cube 到池中
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            var allCubes = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            int despawned = 0;
            foreach (var go in allCubes)
            {
                // 只匹配 Cube 实例，排除 "CubePool" 池根节点
                if (go.name == "Cube" && go.activeInHierarchy)
                {
                    PoolManager.Instance.Despawn(go);
                    despawned++;
                }
            }
            _count = 0;
            Debug.Log($"<color=yellow>[PoolTest] 回收了 {despawned} 个 Cube 到池中</color>");
        }

        // 按 3：模拟切场景（清除 destroyOnSceneLoad 的池）
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            PoolManager.Instance.ClearOnSceneChange();
            _count = 0;
            Debug.Log("<color=red>[PoolTest] 模拟切场景 — 已清除过场景即销毁的池</color>");
        }
    }
}
