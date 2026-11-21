using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>从语言表读取固定悬停提示，适用于按钮、图片和文字。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class LocalizedHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string titleId;
    [SerializeField] private string contentId;
    [SerializeField] private E_TipDirection direction = E_TipDirection.Down;
    [SerializeField] private float extraGap;

    private UIFeedbackController _feedback;
    private Button _button;

    private void Awake()
    {
        _feedback = GetComponent<UIFeedbackController>();
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (_feedback != null) _feedback.HoverChanged += OnHoverChanged;
    }

    private void OnDisable()
    {
        if (_feedback != null) _feedback.HoverChanged -= OnHoverChanged;
        HoverTipHelper.Hide(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_feedback == null) OnHoverChanged(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_feedback == null) OnHoverChanged(false);
    }

    private void OnHoverChanged(bool hovering)
    {
        if (!hovering || !isActiveAndEnabled || (_button != null && !_button.IsInteractable()))
        {
            HoverTipHelper.Hide(this);
            return;
        }

        var target = _button != null && _button.image != null ? _button.image.rectTransform : (RectTransform)transform;
        HoverTipHelper.Show(this, LanguageHelper.Get(titleId), LanguageHelper.Get(contentId), target, direction, extraGap);
    }
}
