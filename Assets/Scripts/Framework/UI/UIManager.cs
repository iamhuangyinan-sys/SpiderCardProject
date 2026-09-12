using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework.Mgr;
using Framework.Res;

namespace Framework.UI
{
    /// <summary>
    /// UI 管理器 —— 管理所有 UI 面板的生命周期和层级
    ///
    /// 场景结构：
    ///   [UICanvas] (DontDestroyOnLoad, Screen Space Overlay, 1920×1080)
    ///     ├── Layer_Bottom (sorting: 0)
    ///     ├── Layer_Normal (sorting: 100)
    ///     ├── Layer_Popup  (sorting: 200)
    ///     ├── Layer_Top    (sorting: 300)
    ///     └── Layer_System (sorting: 400)
    ///
    /// 使用方式：
    ///   var panel = UIManager.Instance.Show&lt;MainPanel&gt;("UI/MainPanel");
    ///   UIManager.Instance.Hide(panel);
    ///   UIManager.Instance.CloseAllPopups();
    /// </summary>
    public class UIManager : ManagerBase<UIManager>
    {
        /// <summary> UI 预制体根路径 </summary>
        private const string UI_ROOT = "Prefab/UI/";

        private Canvas _canvas;
        private readonly Dictionary<E_UILayerEnum, Transform> _layers = new();

        /// <summary> 路径 → 面板实例缓存 </summary>
        private readonly Dictionary<string, BasePanel> _panelCache = new();

        /// <summary> 弹窗栈（后进先出，用于 CloseTopPopup） </summary>
        private readonly List<PopupPanel> _popupStack = new();

        /// <summary> 场景切换遮罩（独立 Canvas，跨场景常驻） </summary>
        private CanvasGroup _sceneMask;

        /// <summary> 场景遮罩的 sortingOrder（压过所有常规 UI） </summary>
        private const int SceneMaskSortingOrder = 32000;

        protected override void OnInit()
        {
            // 幂等：重复 Init（如多个场景都挂了入口脚本）不重建，避免出现两套 Canvas / EventSystem
            if (_canvas != null) return;

            CreateCanvas();
            CreateLayers();
            CreateSceneMask();
        }

        protected override void OnDispose()
        {
            CloseAll();
            _panelCache.Clear();
            _layers.Clear();

            if (_sceneMask != null)
                Object.Destroy(_sceneMask.gameObject);

            if (_canvas != null)
                Object.Destroy(_canvas.gameObject);
        }

        // ==================== 画布初始化 ====================

        private void CreateCanvas()
        {
            // Canvas
            var canvasGo = new GameObject("[UICanvas]");
            Object.DontDestroyOnLoad(canvasGo);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // EventSystem（新版 Input System）
            var esGo = new GameObject("[EventSystem]");
            Object.DontDestroyOnLoad(esGo);

            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            // 默认使用 Default Input Actions，无需额外配置
        }

        private void CreateLayers()
        {
            string[] layerNames = { "Layer_Bottom", "Layer_Normal", "Layer_Popup", "Layer_Top", "Layer_System" };
            var values = (E_UILayerEnum[])System.Enum.GetValues(typeof(E_UILayerEnum));

            for (int i = 0; i < values.Length; i++)
            {
                var layerGo = new GameObject(layerNames[i]);
                layerGo.transform.SetParent(_canvas.transform, false);
                layerGo.transform.SetAsLastSibling();
                _layers[values[i]] = layerGo.transform;
            }
        }

        /// <summary>
        /// 创建场景切换遮罩：独立 Canvas + 极高 sortingOrder，
        /// 保证盖住 UIManager 自己的 Canvas，也盖住场景里手摆的其他 Canvas
        /// </summary>
        private void CreateSceneMask()
        {
            var go = new GameObject("[SceneMask]", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.GraphicRaycaster));
            Object.DontDestroyOnLoad(go);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SceneMaskSortingOrder;

            // 全屏拉伸
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
            img.raycastTarget = true;      // 配合 GraphicRaycaster，过渡期间挡住所有点击

            _sceneMask = go.AddComponent<CanvasGroup>();
            _sceneMask.alpha = 0f;
            _sceneMask.blocksRaycasts = false;

            go.SetActive(false);
        }

        // ==================== 场景切换遮罩 ====================

        /// <summary>遮罩淡入到全黑（协程，供 SceneController 等待完成）</summary>
        public IEnumerator FadeInSceneMask(float duration = 0.25f)
        {
            if (_sceneMask == null) yield break;

            _sceneMask.gameObject.SetActive(true);
            _sceneMask.blocksRaycasts = true;   // 过渡期间不许点

            yield return AnimationHelper.FadeRoutine(_sceneMask, 1f, duration);
        }

        /// <summary>遮罩淡出（协程），淡完自动隐藏并恢复点击</summary>
        public IEnumerator FadeOutSceneMask(float duration = 0.3f)
        {
            if (_sceneMask == null) yield break;

            yield return AnimationHelper.FadeRoutine(_sceneMask, 0f, duration);

            _sceneMask.blocksRaycasts = false;
            _sceneMask.gameObject.SetActive(false);
        }

        // ==================== 显示面板 ====================

        /// <summary>
        /// 显示面板（从 UIConfig 查路径和层级，优先从缓存取）
        /// </summary>
        public T Show<T>() where T : BasePanel
        {
            if (!UIConfig.TryGet<T>(out var info))
            {
                Debug.LogError($"[UIManager] {typeof(T).Name} 未在 UIConfig 中注册！");
                return null;
            }

            if (_panelCache.TryGetValue(info.PrefabPath, out var cachedPanel))
            {
                cachedPanel.ShowInternal();
                return cachedPanel as T;
            }

            var prefab = ResManager.Instance.Load<GameObject>(UI_ROOT + info.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] UI 预制体不存在: Resources/{info.PrefabPath}");
                return null;
            }

            return CreatePanel<T>(info, prefab);
        }

        /// <summary>
        /// 异步显示面板（推荐使用，避免卡顿）
        /// </summary>
        public void ShowAsync<T>(System.Action<T> onLoaded) where T : BasePanel
        {
            if (!UIConfig.TryGet<T>(out var info))
            {
                Debug.LogError($"[UIManager] {typeof(T).Name} 未在 UIConfig 中注册！");
                onLoaded?.Invoke(null);
                return;
            }

            if (_panelCache.TryGetValue(info.PrefabPath, out var cachedPanel))
            {
                cachedPanel.ShowInternal();
                onLoaded?.Invoke(cachedPanel as T);
                return;
            }

            ResManager.Instance.LoadAsync<GameObject>(UI_ROOT + info.PrefabPath, prefab =>
            {
                if (prefab == null)
                {
                    Debug.LogError($"[UIManager] UI 预制体不存在: Resources/{info.PrefabPath}");
                    onLoaded?.Invoke(null);
                    return;
                }

                var panel = CreatePanel<T>(info, prefab);
                PushPopup(panel);
                onLoaded?.Invoke(panel);
            });
        }

        private T CreatePanel<T>(UIPanelInfo info, GameObject prefab) where T : BasePanel
        {
            if (!_layers.TryGetValue(info.Layer, out var parent))
            {
                Debug.LogError($"[UIManager] 层级不存在: {info.Layer}");
                return null;
            }

            var go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;

            var panel = go.GetComponent<T>();
            if (panel == null)
            {
                Debug.LogError($"[UIManager] 预制体上缺少 {typeof(T).Name} 组件: {info.PrefabPath}");
                Object.Destroy(go);
                return null;
            }

            panel.Layer = info.Layer;
            panel.PrefabPath = info.PrefabPath;
            _panelCache[info.PrefabPath] = panel;

            var canvas = go.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = (int)info.Layer;

            panel.OpenInternal();
            PushPopup(panel);
            return panel;
        }

        // ==================== 隐藏 / 关闭 ====================

        /// <summary>
        /// 隐藏面板（SetActive(false)，保留缓存，下次 Show 直接打开）
        /// </summary>
        public void Hide(BasePanel panel)
        {
            if (panel == null) return;
            PopPopup(panel);
            panel.HideInternal();
        }

        /// <summary>
        /// 永久关闭面板（从缓存移除并销毁）
        /// </summary>
        public void Close(BasePanel panel)
        {
            if (panel == null) return;
            PopPopup(panel);
            panel.CloseInternal();
            _panelCache.Remove(panel.PrefabPath);
            Object.Destroy(panel.gameObject);
        }

        /// <summary>
        /// 关闭某个层级的所有面板
        /// </summary>
        public void CloseLayer(E_UILayerEnum layer)
        {
            foreach (var kv in _panelCache)
            {
                if (kv.Value.Layer == layer && kv.Value.IsOpen)
                    kv.Value.HideInternal();
            }
        }

        /// <summary>
        /// 关闭栈顶弹窗
        /// </summary>
        public void CloseTopPopup()
        {
            if (_popupStack.Count == 0) return;
            var top = _popupStack[_popupStack.Count - 1];
            Hide(top);
        }

        /// <summary>
        /// 关闭所有弹窗（从栈顶到栈底依次关闭）
        /// </summary>
        public void CloseAllPopups()
        {
            for (int i = _popupStack.Count - 1; i >= 0; i--)
            {
                if (_popupStack[i].IsOpen)
                    _popupStack[i].HideInternal();
            }
            _popupStack.Clear();
        }

        /// <summary>
        /// 隐藏所有已打开的面板（切场景时调用）
        /// 只隐藏、**保留缓存**：面板下次 Show 可直接复用，无需重新加载预制体；
        /// 监听不会解绑（那是 OnClose 的事），所以重新 Show 时状态是连续的
        /// </summary>
        public void HideAll()
        {
            foreach (var kv in _panelCache)
            {
                if (kv.Value != null && kv.Value.IsOpen)
                    kv.Value.HideInternal();
            }
        }

        /// <summary>
        /// 关闭并销毁所有面板（彻底清理，用于框架销毁等场景）
        /// 会触发 OnClose 并真正销毁物体
        /// </summary>
        public void CloseAll()
        {
            var panels = new List<BasePanel>(_panelCache.Values);

            _panelCache.Clear();
            _popupStack.Clear();

            foreach (var panel in panels)
            {
                if (panel == null) continue;

                panel.CloseInternal();          // 触发 OnClose，解除事件监听
                Object.Destroy(panel.gameObject);
            }
        }

        // ==================== 查询 ====================

        /// <summary>
        /// 获取已缓存的面板（可能未打开）
        /// </summary>
        public T GetPanel<T>() where T : BasePanel
        {
            return UIConfig.TryGet<T>(out var info) && _panelCache.TryGetValue(info.PrefabPath, out var panel)
                ? panel as T : null;
        }

        /// <summary>
        /// 面板是否正在打开状态
        /// </summary>
        public bool IsOpen<T>() where T : BasePanel
        {
            return UIConfig.TryGet<T>(out var info)
                && _panelCache.TryGetValue(info.PrefabPath, out var panel)
                && panel.IsOpen;
        }

        // ==================== 弹窗栈 ====================

        private void PushPopup(BasePanel panel)
        {
            if (panel is PopupPanel popup && !_popupStack.Contains(popup))
                _popupStack.Add(popup);
        }

        private void PopPopup(BasePanel panel)
        {
            if (panel is PopupPanel popup)
                _popupStack.Remove(popup);
        }
    }
}
