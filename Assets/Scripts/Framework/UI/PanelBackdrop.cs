using Framework;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>将背景独立于面板内容保存，面板隐藏时可以继续显示；切场景统一收起。</summary>
    [DisallowMultipleComponent]
    public sealed class PanelBackdrop : MonoBehaviour
    {
        [SerializeField] private CanvasGroup background;
        private SceneController sceneController;

        private void Awake()
        {
            if (background == null) return;
            background.gameObject.SetActive(false);
            int index = transform.GetSiblingIndex();
            background.transform.SetParent(transform.parent, true);
            background.transform.SetSiblingIndex(index);
            sceneController = SceneController.Instance;
            sceneController.OnBeforeLoad += HideForScene;
        }

        public void Show(bool animated)
        {
            if (background == null) return;
            // 菜单从设置恢复时保留同一个、已经完全显示的黑底。
            if (background.gameObject.activeSelf && background.alpha >= 1f) return;
            background.gameObject.SetActive(true);
            if (animated) AnimationHelper.FadeIn(background);
            else
            {
                AnimationHelper.Kill(background);
                background.alpha = 1f;
                background.blocksRaycasts = true;
            }
        }

        public void Hide(bool animated)
        {
            if (background == null) return;
            if (animated && background.gameObject.activeSelf)
                AnimationHelper.FadeOut(background, onComplete: () => background.gameObject.SetActive(false));
            else
            {
                AnimationHelper.Kill(background);
                background.gameObject.SetActive(false);
            }
        }

        private void HideForScene() => Hide(false);

        private void OnDestroy()
        {
            if (sceneController != null) sceneController.OnBeforeLoad -= HideForScene;
            if (background != null)
            {
                AnimationHelper.Kill(background);
                Destroy(background.gameObject);
            }
        }
    }
}
