using DG.Tweening;
using Framework.UI;

/// <summary>
/// 通用提示面板 —— 淡入 → 停留一段时间 → 淡出 → 自动隐藏
///
/// 业务层不要直接 UIManager.Show&lt;TipPanel&gt;()，统一走 TipHelper.Show("文案")：
/// 面板的创建 / 复用、停留时长、开关动画都由这里负责。
/// </summary>
public partial class TipPanel : NormalPanel
{
    /// <summary>默认停留时长（秒）</summary>
    private const float DefaultHoldDuration = 1f;

    /// <summary>停留计时（停留结束 → 自动隐藏）</summary>
    private Sequence _holdSeq;

    // ==================== 动画开关（声明式，动画代码在 NormalPanel） ====================

    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;
    protected override float FadeDuration => 0.2f;

    // ==================== 对外 ====================

    /// <summary>
    /// 显示一条提示：写入文案并重新计时。
    /// 提示还在显示时再次调用，只会刷新文案 + 重置停留时间，不会重播淡入。
    /// </summary>
    /// <param name="content">提示文案（后续接提示表 / 语言表时改 TipHelper）</param>
    /// <param name="holdDuration">停留时长（秒）</param>
    public void ShowTip(string content, float holdDuration = DefaultHoldDuration)
    {
        comps.txtTip.text = content;

        ClearHold();
        _holdSeq = DOTween.Sequence();
        _holdSeq.AppendInterval(holdDuration)
                .OnComplete(HideSelf);
    }

    // ==================== 生命周期 ====================

    protected override void OnOpen() { }

    protected override void OnShow() { }

    protected override void OnHide()
    {
        ClearHold();
    }

    protected override void OnClose()
    {
        ClearHold();
    }

    protected override void OnPanelOpened()
    {
        base.OnPanelOpened();

        // 提示只负责展示：不参与交互，也不挡下面面板的点击
        SetInteractable(false);
    }

    private void ClearHold()
    {
        _holdSeq?.Kill();
        _holdSeq = null;
    }
}
