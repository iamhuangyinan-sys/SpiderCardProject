using System;
using Framework.Res;
using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class AddSuitItemModule : BaseModule, IPointerClickHandler
{
    private UIFeedbackController _feedback;
    private Action<AddSuitItemModule> _onChoose;
    private string _imagePath;
    private bool _selectable;

    public E_CardSuitEnum Suit { get; private set; }

    public void Bind(E_CardSuitEnum suit, Action<AddSuitItemModule> onChoose)
    {
        Suit = suit;
        _onChoose = onChoose;
        _imagePath = $"GamePlay/CardBuild/cardBuild{suit}";
        comps.imgCardVisual.sprite = ResManager.Instance.Load<Sprite>(_imagePath);
        SetSelectable(true);
    }

    public void SetSelectable(bool value)
    {
        _selectable = value;
        _feedback.ResetFeedback();
        _feedback.SetInteractable(value, immediate: true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_selectable && eventData.button == PointerEventData.InputButton.Left)
            _onChoose?.Invoke(this);
    }

    protected override void OnBind()
    {
        _feedback = GetComponent<UIFeedbackController>();
    }

    protected override void OnUnBind()
    {
        _onChoose = null;
        if (_imagePath != null) ResManager.Instance.UnloadAsset<Sprite>(_imagePath);
    }
}
