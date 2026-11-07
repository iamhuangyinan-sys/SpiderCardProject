using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>保留 Button 点击与导航，只将输入统一转交给 Feedback；不执行内置颜色过渡。</summary>
[RequireComponent(typeof(UIFeedbackController))]
[AddComponentMenu("UI/Feedback/Feedback Button")]
public sealed class UIFeedbackButton : Button
{
    [SerializeField, Min(0.01f), Tooltip("键盘/手柄确认时，按下表现保留的秒数")]
    private float submitFeedbackDuration = 0.1f;

    private UIFeedbackController feedback;
    private bool hovered;
    private bool focused;
    private bool pointerDown;
    private bool submitting;
    private Coroutine submitRoutine;

    protected override void Awake()
    {
        base.Awake();
        feedback = GetComponent<UIFeedbackController>();
        transition = Transition.None;
    }

    protected override void OnEnable()
    {
        focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        base.OnEnable();
        Sync(true);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // 由原生 Selectable 通知：包括 interactable 和父 CanvasGroup 变化。
        // 不调用基类的变色/换图/Animator 过渡，效果统一由 Controller 负责。
        if (Application.isPlaying) Sync(instant);
    }

    private void Sync(bool immediate = false)
    {
        if (!Application.isPlaying) return;
        if (feedback == null) feedback = GetComponent<UIFeedbackController>();
        if (feedback == null || !IsActive()) return;
        feedback.SetInput(IsInteractable(), hovered, submitting || (pointerDown && hovered), focused, immediate);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        base.OnPointerEnter(eventData);
        Sync();
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        base.OnPointerExit(eventData);
        Sync();
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pointerDown = IsActive() && IsInteractable();
        base.OnPointerDown(eventData);
        Sync();
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pointerDown = false;
        base.OnPointerUp(eventData);
        Sync();
    }

    public override void OnSelect(BaseEventData eventData)
    {
        focused = true;
        base.OnSelect(eventData);
        Sync();
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        focused = false;
        base.OnDeselect(eventData);
        Sync();
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
        feedback?.NotifyClick();
        base.OnPointerClick(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (!IsActive() || !IsInteractable()) return;
        if (submitRoutine != null) StopCoroutine(submitRoutine);
        submitting = true;
        Sync();
        feedback?.NotifyClick();
        onClick.Invoke();
        if (this == null || !IsActive() || !IsInteractable())
        {
            submitting = false;
            if (this != null) Sync();
            return;
        }
        submitRoutine = StartCoroutine(FinishSubmit());
    }

    private IEnumerator FinishSubmit()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, submitFeedbackDuration));
        submitting = false;
        submitRoutine = null;
        Sync();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) return;
        CancelInput();
        Sync(true);
    }

    private void CancelInput()
    {
        if (submitRoutine != null) StopCoroutine(submitRoutine);
        submitRoutine = null;
        hovered = pointerDown = submitting = false;
    }

    protected override void OnDisable()
    {
        CancelInput();
        focused = false;
        base.OnDisable();
        if (feedback != null) feedback.ClearInteraction();
    }
}
