using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>非按钮控件的可选悬停适配器。宿主手动 SetHovered 时不需要挂；不要和 FeedbackButton 同挂。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIFeedbackController))]
[AddComponentMenu("UI/Feedback/Hover Input (Non Button)")]
public sealed class UIFeedbackHoverInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private UIFeedbackController feedback;
    private void Awake() => feedback = GetComponent<UIFeedbackController>();
    public void OnPointerEnter(PointerEventData eventData) => feedback.SetHovered(true);
    public void OnPointerExit(PointerEventData eventData) => feedback.SetHovered(false);
    private void OnDisable()
    {
        if (feedback != null) feedback.SetHovered(false, true);
    }
}
