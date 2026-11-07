using System;
using UnityEngine;

/// <summary>单一状态入口。自动收集同节点效果，按钮、指针适配器或业务脚本三选一驱动输入。</summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/Feedback/Feedback Controller")]
public sealed class UIFeedbackController : MonoBehaviour
{
    [SerializeField, Tooltip("暂停游戏时 UI 动画仍然播放")]
    private bool useUnscaledTime = true;

    [SerializeField, Tooltip("初次显示时是否需要呼吸提醒；也可由业务调用 SetAttention")]
    private bool attentionOnStart = false;

    private UIFeedbackEffect[] effects;
    private bool initialized;
    private bool interactable = true;
    private bool hovered;
    private bool pressed;
    private bool focused;
    private bool attention;

    public E_UIFeedbackState State { get; private set; }
    public bool IsHovered => interactable && hovered;
    public bool Attention => attention;
    public bool UseUnscaledTime => useUnscaledTime;
    public UIFeedbackContext Context => new UIFeedbackContext(State, attention, useUnscaledTime, hovered);

    public event Action<bool> HoverChanged;
    public event Action<E_UIFeedbackState> StateChanged;
    public event Action Clicked;

    private void Awake() => Initialize();
    private void OnEnable() => RefreshEffects(true);

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        attention = attentionOnStart;
        effects = GetComponents<UIFeedbackEffect>();
    }

    public void SetInput(bool canInteract, bool isHovered, bool isPressed, bool isFocused,
        bool immediate = false)
    {
        Initialize();
        bool wasHovered = IsHovered;
        bool wasPointerHovered = hovered;
        interactable = canInteract;
        hovered = isHovered;
        pressed = isPressed;
        focused = isFocused;
        var next = !interactable ? E_UIFeedbackState.Disabled
            : pressed ? E_UIFeedbackState.Pressed
            : hovered ? E_UIFeedbackState.Hovered
            : focused ? E_UIFeedbackState.Focused
            : E_UIFeedbackState.Normal;

        bool changed = State != next;
        State = next;
        // 禁用时状态枚举仍为 Disabled，但原始指针进出也需要通知可选悬停效果。
        if (changed || wasPointerHovered != hovered || immediate) ApplyEffects(immediate);
        if (wasHovered != IsHovered) HoverChanged?.Invoke(IsHovered);
        if (changed) StateChanged?.Invoke(State);
    }

    /// <summary>非按钮控件可由宿主手动传入悬停状态。</summary>
    public void SetHovered(bool value, bool immediate = false)
        => SetInput(interactable, value, pressed, focused, immediate);

    /// <summary>非按钮控件的可交互状态；按钮应直接设置 Button.interactable。</summary>
    public void SetInteractable(bool value, bool immediate = false)
        => SetInput(value, hovered, pressed, focused, immediate);

    public void SetAttention(bool value)
    {
        Initialize();
        if (attention == value) return;
        attention = value;
        ApplyEffects(false);
    }

    public void RefreshEffects(bool immediate = false)
    {
        Initialize();
        effects = GetComponents<UIFeedbackEffect>();
        ApplyEffects(immediate);
    }

    private void ApplyEffects(bool immediate)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (effects == null) effects = GetComponents<UIFeedbackEffect>();
        var context = Context;
        foreach (var effect in effects)
            if (effect != null) effect.Apply(context, immediate);
    }

    public void NotifyClick()
    {
        if (!isActiveAndEnabled || !interactable) return;
        if (effects == null) effects = GetComponents<UIFeedbackEffect>();
        foreach (var effect in effects)
            if (effect != null && effect.isActiveAndEnabled) effect.PlayClick(Context);
        Clicked?.Invoke();
    }

    /// <summary>回收时调用；清除业务提醒，清空输入，并还原所有原始视觉属性。</summary>
    public void ResetFeedback()
    {
        Initialize();
        attention = false;
        ClearInteraction();
    }

    /// <summary>隐藏时只清理瞬时输入，保留业务提醒意图，重新显示后可以继续呼吸。</summary>
    public void ClearInteraction()
    {
        bool wasHovered = IsHovered;
        bool stateChanged = State != E_UIFeedbackState.Normal;
        interactable = true;
        hovered = pressed = focused = false;
        State = E_UIFeedbackState.Normal;
        RestoreEffects();
        if (wasHovered) HoverChanged?.Invoke(false);
        if (stateChanged) StateChanged?.Invoke(State);
    }

    public void RestoreEffects()
    {
        foreach (var effect in GetComponents<UIFeedbackEffect>()) effect.Restore();
    }

    private void OnDisable() => ClearInteraction();
}
