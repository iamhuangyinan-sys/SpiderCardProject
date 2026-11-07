using UnityEngine;

/// <summary>统一交互状态；鼠标悬停和导航焦点分开，避免点击后一直保持悬停外观。</summary>
public enum E_UIFeedbackState
{
    Normal,
    Hovered,
    Pressed,
    Focused,
    Disabled
}

public readonly struct UIFeedbackContext
{
    public E_UIFeedbackState State { get; }
    public bool Attention { get; }
    public bool UseUnscaledTime { get; }
    /// <summary>原始指针状态；禁用时仍保留，供显式允许的悬停效果使用。</summary>
    public bool IsPointerHovered { get; }
    public bool CanBreathe => Attention && State == E_UIFeedbackState.Normal;

    public UIFeedbackContext(E_UIFeedbackState state, bool attention, bool useUnscaledTime,
        bool isPointerHovered = false)
    {
        State = state;
        Attention = attention;
        UseUnscaledTime = useUnscaledTime;
        IsPointerHovered = isPointerHovered;
    }
}

/// <summary>效果写入的属性，用于编辑器预览快照和冲突检查。</summary>
public readonly struct UIFeedbackBinding
{
    public Component Target { get; }
    public string PropertyPath { get; }

    public UIFeedbackBinding(Component target, string propertyPath)
    {
        Target = target;
        PropertyPath = propertyPath;
    }
}
