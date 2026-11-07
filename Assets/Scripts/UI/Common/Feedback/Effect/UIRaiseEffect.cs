using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>悬停时把容器提到同级最后；等待缩回后恢复。父 LayoutGroup 下不改排序。</summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Effects/Raise On Hover")]
public sealed class UIRaiseEffect : UIFeedbackEffect
{
    [SerializeField, Tooltip("要提层的外层容器；留空使用当前节点")]
    private Transform target = null;
    [SerializeField, Tooltip("不可交互时也允许悬停提层，配合缩放组件的同名开关")]
    private bool hoverWhenDisabled = false;

    private Transform capturedTarget;
    private Transform originalParent;
    private int originalSibling;
    private bool raised;

    protected override bool Capture()
    {
        capturedTarget = target != null ? target : transform;
        return capturedTarget != null;
    }

    protected override void ApplyState(UIFeedbackContext context, bool immediate)
    {
        // 编辑器状态预览只看外观，不调整真实层级顺序。
        if (!Application.isPlaying || capturedTarget == null) return;
        if (context.State == E_UIFeedbackState.Hovered || context.State == E_UIFeedbackState.Pressed ||
            (context.State == E_UIFeedbackState.Disabled && hoverWhenDisabled && context.IsPointerHovered))
        {
            if (raised) return;
            var parent = capturedTarget.parent;
            if (parent == null || parent.GetComponent<LayoutGroup>() != null) return;
            originalParent = parent;
            originalSibling = capturedTarget.GetSiblingIndex();
            capturedTarget.SetAsLastSibling();
            raised = true;
        }
        else if (raised)
        {
            if (immediate) RestoreSibling();
            else Play(DOVirtual.DelayedCall(Duration, RestoreSibling), context);
        }
    }

    private void RestoreSibling()
    {
        if (!raised) return;
        raised = false;
        // 批量失活期间 Unity 禁止调整兄弟顺序；重新挂父节点后也不回写旧索引。
        if (capturedTarget == null || !capturedTarget.gameObject.activeInHierarchy ||
            capturedTarget.parent != originalParent || originalParent == null ||
            originalParent.GetComponent<LayoutGroup>() != null) return;
        capturedTarget.SetSiblingIndex(originalSibling);
    }

    protected override void RestoreValues() => RestoreSibling();

    // 不修改可动画的视觉属性，不需要 AnimationMode 快照或属性冲突检查。
    public override void CollectBindings(List<UIFeedbackBinding> bindings) { }
}
