using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 动画工具 —— 对 DOTween 的通用语义化封装（纯静态，不服务具体业务）
/// 各业务模块调用本类编排动画，不直接依赖 DOTween API。
/// </summary>
public static class AnimationHelper
{
    /// <summary>UI 面板入场默认时长</summary>
    public const float PanelFlyInDuration = 0.3f;

    /// <summary>UI 面板淡入 / 淡出默认时长</summary>
    public const float PanelFadeDuration = 0.2f;

    /// <summary>从 from 瞬移，再飞到 to（发牌、移动等用）</summary>
    public static Tween FlyTo(Transform trans, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutQuad)
    {
        trans.position = from;
        return trans.DOMove(to, duration).SetEase(ease);
    }

    /// <summary>直接移动到 to</summary>
    public static Tween MoveTo(Transform trans, Vector3 to, float duration, Ease ease = Ease.OutQuad)
    {
        return trans.DOMove(to, duration).SetEase(ease);
    }

    /// <summary>缩放到目标值（入场 / 翻牌等用）</summary>
    public static Tween ScaleTo(Transform trans, Vector3 target, float duration, Ease ease = Ease.OutBack)
    {
        return trans.DOScale(target, duration).SetEase(ease);
    }

    /// <summary>
    /// UI 面板从下方飞入：瞬移到 basePos 下方 offsetY，再上滑回 basePos + 淡入。
    /// basePos 由调用方传入（面板原位），不读当前 anchoredPosition，
    /// 这样反复飞出 / 飞入也不会累积偏移。
    /// </summary>
    /// <param name="rect">面板 RectTransform</param>
    /// <param name="canvasGroup">淡入用（可为 null，则只做位移）</param>
    /// <param name="basePos">面板原位（anchoredPosition）</param>
    /// <param name="offsetY">起始下移距离（一般传面板高度，保证从屏幕外飞入）</param>
    public static Tween FlyInFromBottom(RectTransform rect, CanvasGroup canvasGroup, Vector2 basePos, float offsetY,
        float duration = PanelFlyInDuration, Ease ease = Ease.OutCubic)
    {
        if (rect == null) return null;

        // 清掉残留动画，避免重复打开时位置错乱
        rect.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();

        rect.anchoredPosition = basePos - new Vector2(0f, offsetY);

        var seq = DOTween.Sequence();
        seq.Join(rect.DOAnchorPos(basePos, duration).SetEase(ease));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;     // 恢复交互（飞出时被置 false，不恢复会导致整个面板按钮失效）
            canvasGroup.blocksRaycasts = true;   // 飞入完成后恢复点击
            seq.Join(canvasGroup.DOFade(1f, duration));
        }

        return seq;
    }

    /// <summary>
    /// UI 面板向下方飞出：从 basePos 下滑到 basePos 下方 offsetY + 淡出，动画结束后回调。
    /// 结束时会把位置复位到 basePos，所以下次飞入的基准始终一致。
    /// </summary>
    public static Tween FlyOutToBottom(RectTransform rect, CanvasGroup canvasGroup, Vector2 basePos, float offsetY,
        float duration = PanelFlyInDuration, Ease ease = Ease.InCubic, TweenCallback onComplete = null)
    {
        if (rect == null)
        {
            onComplete?.Invoke();
            return null;
        }

        rect.DOKill();
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        Vector2 target = basePos - new Vector2(0f, offsetY);

        var seq = DOTween.Sequence();
        seq.Join(rect.DOAnchorPos(target, duration).SetEase(ease));
        if (canvasGroup != null) seq.Join(canvasGroup.DOFade(0f, duration));

        seq.OnComplete(() =>
        {
            rect.anchoredPosition = basePos;   // 复位，保证下次打开基准一致
            onComplete?.Invoke();
        });

        return seq;
    }

    /// <summary>
    /// UI 面板淡入：透明 → 不透明，并恢复交互。
    /// 与 FlyInFromBottom 的区别：只做透明度、不动位置（提示条 / Toast 用）。
    /// </summary>
    public static Tween FadeIn(CanvasGroup canvasGroup, float duration = PanelFadeDuration,
        Ease ease = Ease.OutQuad)
    {
        if (canvasGroup == null) return null;

        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;     // 淡出时被置 false，不恢复会导致面板失效
        canvasGroup.blocksRaycasts = true;

        return canvasGroup.DOFade(1f, duration).SetEase(ease);
    }

    /// <summary>
    /// UI 面板淡出：不透明 → 透明，并禁止交互，播完回调（一般在回调里 SetActive(false)）
    /// </summary>
    public static Tween FadeOut(CanvasGroup canvasGroup, float duration = PanelFadeDuration,
        Ease ease = Ease.InQuad, TweenCallback onComplete = null)
    {
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            return null;
        }

        canvasGroup.DOKill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        return canvasGroup.DOFade(0f, duration).SetEase(ease).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// CanvasGroup 淡入 / 淡出（协程版）——供需要「等动画播完再继续」的流程 yield
    /// </summary>
    public static IEnumerator FadeRoutine(CanvasGroup canvasGroup, float targetAlpha, float duration,
        Ease ease = Ease.Linear)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.DOKill();
        yield return canvasGroup.DOFade(targetAlpha, duration).SetEase(ease).WaitForCompletion();
    }

    /// <summary>取消该物体上的所有动画（回收 / 打断时调用，避免动画残留）</summary>
    public static void Kill(Transform trans)
    {
        trans.DOKill();
    }
}
