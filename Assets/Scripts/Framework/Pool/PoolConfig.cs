using UnityEngine;

namespace Framework.Pool
{
    /// <summary>
    /// 对象池配置 —— 挂载到预制体上
    ///
    /// destroyOnSceneLoad = true  → 切场景时该池自动清空（如子弹、特效）
    /// destroyOnSceneLoad = false → 切场景保留（如 UI 面板、音频源）
    /// </summary>
    public class PoolConfig : MonoBehaviour
    {
        [Tooltip("切场景时是否清空该池")]
        public bool destroyOnSceneLoad = true;

        [Tooltip("初始化时预生成数量（0 = 不预生成）")]
        public int preloadCount = 0;

        [Tooltip("池最大容量（-1 = 无限制）")]
        public int maxSize = -1;
    }
}
