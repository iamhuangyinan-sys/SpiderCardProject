using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Framework.Mgr;
using Framework.Pool;

namespace Framework
{
    /// <summary>
    /// 场景控制器 —— 切换场景时执行清理、过渡操作
    ///
    /// 使用方式：
    ///   SceneController.Instance.LoadScene("GameScene");
    ///   SceneController.Instance.LoadScene("MenuScene", () => Debug.Log("切换完成"));
    ///
    /// 过渡效果：订阅 OnBeforeLoad / OnAfterLoad 做淡入淡出
    ///   SceneController.Instance.OnBeforeLoad += () => { /* 黑屏遮罩淡入 */ };
    ///   SceneController.Instance.OnAfterLoad  += () => { /* 黑屏遮罩淡出 */ };
    /// </summary>
    public class SceneController : ManagerBase<SceneController>
    {
        /// <summary> 当前场景名 </summary>
        public string CurrentScene { get; private set; }

        /// <summary> 是否正在加载中 </summary>
        public bool IsLoading { get; private set; }

        /// <summary> 加载进度（0 ~ 1） </summary>
        public float Progress { get; private set; }

        // ==================== 事件 ====================

        /// <summary> 场景加载前（可在此做 UI 遮罩淡入） </summary>
        public event UnityAction OnBeforeLoad;

        /// <summary> 场景加载后（可在此做 UI 遮罩淡出） </summary>
        public event UnityAction OnAfterLoad;

        // ==================== 加载场景 ====================

        /// <summary>
        /// 异步加载场景（含过场景清理 + 加载回调）
        /// </summary>
        /// <param name="sceneName">目标场景名（需在 Build Settings 中注册）</param>
        /// <param name="onComplete">加载完成回调</param>
        public void LoadScene(string sceneName, UnityAction onComplete = null)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneController] 正在加载场景中，忽略重复请求");
                return;
            }

            MonoManager.Instance.StartCoroutine(DoLoadScene(sceneName, onComplete));
        }

        private IEnumerator DoLoadScene(string sceneName, UnityAction onComplete)
        {
            IsLoading = true;
            Progress = 0f;

            // ===== 1. 加载前 =====
            Debug.Log($"[SceneController] 切换场景：{CurrentScene} → {sceneName}");
            OnBeforeLoad?.Invoke();

            // ===== 2. 切场景清理 =====
            PoolManager.Instance.ClearOnSceneChange();

            // ===== 3. 异步加载 =====
            var ao = SceneManager.LoadSceneAsync(sceneName);
            if (ao == null)
            {
                Debug.LogError($"[SceneController] 场景不存在: {sceneName}");
                IsLoading = false;
                onComplete?.Invoke();
                yield break;
            }

            ao.allowSceneActivation = false;

            // 等待加载到 90%（剩 10% 留给激活阶段，防止黑屏）
            while (ao.progress < 0.9f)
            {
                Progress = ao.progress;
                yield return null;
            }

            Progress = 1f;
            ao.allowSceneActivation = true;

            // 等待场景激活
            yield return ao;

            CurrentScene = sceneName;
            IsLoading = false;

            // ===== 4. 加载后 =====
            Debug.Log($"[SceneController] 场景切换完成：{sceneName}");
            OnAfterLoad?.Invoke();
            onComplete?.Invoke();
        }
    }
}
