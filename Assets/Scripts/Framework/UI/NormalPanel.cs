using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 普通面板 —— 无特殊行为，纯展示
    /// 继承此类：MainPanel、ShopPanel、HudPanel 等
    ///
    /// 打开 / 关闭动画默认不开，需要动画的面板自行 override 开关（不用写动画代码）：
    ///   protected override bool FlyInOnOpen    => true;   // 打开（含 Hide 后重新 Show）时从下方飞入
    ///   protected override bool FlyOutOnClose  => true;   // 隐藏时向下方飞出，播完才真正隐藏
    ///   protected override bool FadeInOnOpen   => true;   // 打开时原地淡入（提示条 / Toast）
    ///   protected override bool FadeOutOnClose => true;   // 隐藏时原地淡出，播完才真正隐藏
    ///   protected override float FlyOffsetY    => 400f;   // 改位移距离
    ///   protected override float FadeDuration  => 0.2f;   // 改淡入淡出时长
    /// 注：飞入自带淡入，与纯淡入二选一（飞入优先）
    /// </summary>
    public abstract class NormalPanel : BasePanel
    {
        /// <summary>面板原位（Awake 时记录），飞入 / 飞出动画的基准点，避免反复开关累积偏移</summary>
        private Vector2 _baseAnchoredPos;

        /// <summary>打开时是否播放「从下方飞入」动画（默认关，子类开启）</summary>
        protected virtual bool FlyInOnOpen => false;

        /// <summary>隐藏时是否播放「向下方飞出」动画（默认关，子类开启）</summary>
        protected virtual bool FlyOutOnClose => false;

        /// <summary>打开时是否播放「原地淡入」动画（默认关，子类开启）</summary>
        protected virtual bool FadeInOnOpen => false;

        /// <summary>隐藏时是否播放「原地淡出」动画（默认关，子类开启）</summary>
        protected virtual bool FadeOutOnClose => false;

        /// <summary>飞入 / 飞出的位移距离（像素，默认 1920×1080 参考分辨率下的一个屏幕高度）</summary>
        protected virtual float FlyOffsetY => 1080f;

        /// <summary>淡入 / 淡出时长（秒）</summary>
        protected virtual float FadeDuration => AnimationHelper.PanelFadeDuration;

        protected override void Awake()
        {
            base.Awake();
            _baseAnchoredPos = RectTransform.anchoredPosition;
        }

        protected override void OnPanelOpened()
        {
            if (FlyInOnOpen)
            {
                AnimationHelper.FlyInFromBottom(RectTransform, CanvasGroup, _baseAnchoredPos, FlyOffsetY);
                return;
            }

            if (FadeInOnOpen) AnimationHelper.FadeIn(CanvasGroup, FadeDuration);
        }

        protected override bool OnPanelClosing()
        {
            if (FlyOutOnClose)
            {
                AnimationHelper.FlyOutToBottom(RectTransform, CanvasGroup, _baseAnchoredPos, FlyOffsetY,
                    onComplete: () =>
                    {
                        // 飞完时若已被重新 Show（IsOpen 又为 true），就不要把面板藏掉
                        if (!IsOpen) gameObject.SetActive(false);
                    });

                return true;   // 接管隐藏：等飞完再 SetActive(false)
            }

            if (FadeOutOnClose)
            {
                AnimationHelper.FadeOut(CanvasGroup, FadeDuration,
                    onComplete: () =>
                    {
                        if (!IsOpen) gameObject.SetActive(false);
                    });

                return true;   // 接管隐藏：等淡完再 SetActive(false)
            }

            return false;
        }
    }
}
