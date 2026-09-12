using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Framework.Mgr;
using Framework.Pool;
using Framework.UI;

namespace Framework
{
    /// <summary>
    /// 场景控制器 —— 切换场景时执行清理、过渡操作
    ///
    /// 使用方式：
    ///   SceneController.Instance.LoadScene("GameScene");
    ///   SceneController.Instance.LoadScene("MenuScene", () => Debug.Log("切换完成"));
    ///
    /// 过渡流程（黑屏遮罩，避免看到场景 / UI 元素中途消失）：
    ///   ① 遮罩淡入到全黑（等完成，期间挡点击）
    ///   ② OnBeforeLoad + 切场景清理
    ///   ③ 异步加载场景（保持全黑）
    ///   ④ 等新场景内容就绪（新场景入口调 NotifySceneReady，最多等 SceneReadyTimeout）
    ///   ⑤ 遮罩淡出
    ///   ⑥ OnAfterLoad + onComplete
    ///
    /// 新场景入口记得在把内容建完后调一句 SceneController.Instance.NotifySceneReady()，
    /// 否则会一直等到超时才淡出。
    /// </summary>
    public class SceneController : ManagerBase<SceneController>
    {
        /// <summary>等待新场景「内容就绪」的超时时间（秒）</summary>
        private const float SceneReadyTimeout = 3f;

        /// <summary>黑屏淡入时长（秒）</summary>
        private const float MaskFadeInDuration = 0.25f;

        /// <summary>黑屏淡出时长（秒）</summary>
        private const float MaskFadeOutDuration = 0.3f;

        /// <summary> 当前场景名 </summary>
        public string CurrentScene { get; private set; }

        /// <summary> 是否正在加载中 </summary>
        public bool IsLoading { get; private set; }

        /// <summary> 加载进度（0 ~ 1） </summary>
        public float Progress { get; private set; }

        /// <summary> 新场景内容是否已就绪（由入口调 NotifySceneReady 置位） </summary>
        private bool _sceneReady;

        // ==================== 事件 ====================

        /// <summary> 场景加载前（此时画面已全黑） </summary>
        public event UnityAction OnBeforeLoad;

        /// <summary> 场景加载后（遮罩即将淡出） </summary>
        public event UnityAction OnAfterLoad;

        // ==================== 加载场景 ====================

        /// <summary>
        /// 通知场景控制器：新场景内容已就绪，可以淡出遮罩了
        /// 由新场景入口脚本在 UI / 内容建完后调用
        /// </summary>
        public void NotifySceneReady()
        {
            _sceneReady = true;
        }

        /// <summary>
        /// 异步加载场景（含过场景清理 + 黑屏过渡 + 加载回调）
        /// </summary>
        /// <param name="sceneName">目标场景名（需在 Build Settings 中注册）</param>
        /// <param name="onComplete">加载完成回调（遮罩淡出后触发）</param>
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
            _sceneReady = false;

            Debug.Log($"[SceneController] 切换场景：{CurrentScene} → {sceneName}");

            // ===== 1. 先淡入到全黑：画面全黑后再动场景，避免看到元素消失 =====
            yield return UIManager.Instance.FadeInSceneMask(MaskFadeInDuration);

            // ===== 2. 加载前通知 + 切场景清理（此时已经看不到画面了）=====
            // 先把旧场景的 UI 全部隐藏：面板挂在 DontDestroyOnLoad 的 Canvas 上，
            // 若等切完再关，会跟着新场景一起露出来闪一下。
            // 只隐藏不销毁 —— 缓存保留，下次 Show 直接复用（面板的关闭动画也播在黑幕下面，看不见）
            UIManager.Instance.HideAll();

            OnBeforeLoad?.Invoke();
            PoolManager.Instance.ClearOnSceneChange();

            // ===== 3. 异步加载（保持全黑）=====
            var ao = SceneManager.LoadSceneAsync(sceneName);
            if (ao == null)
            {
                Debug.LogError($"[SceneController] 场景不存在: {sceneName}");
                yield return UIManager.Instance.FadeOutSceneMask(MaskFadeOutDuration);
                IsLoading = false;
                onComplete?.Invoke();
                yield break;
            }

            ao.allowSceneActivation = false;

            // 等待加载到 90%（剩 10% 留给激活阶段）
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

            // ===== 4. 等新场景内容就绪再淡出，避免看到尚未初始化完的画面 =====
            float waited = 0f;
            while (!_sceneReady && waited < SceneReadyTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_sceneReady)
            {
                Debug.LogWarning($"[SceneController] {sceneName} 未在 {SceneReadyTimeout}s 内调用 NotifySceneReady，直接淡出");
            }

            // ===== 5. 淡出遮罩 =====
            yield return UIManager.Instance.FadeOutSceneMask(MaskFadeOutDuration);

            IsLoading = false;

            // ===== 6. 加载后 =====
            Debug.Log($"[SceneController] 场景切换完成：{sceneName}");
            OnAfterLoad?.Invoke();
            onComplete?.Invoke();
        }
    }
}
