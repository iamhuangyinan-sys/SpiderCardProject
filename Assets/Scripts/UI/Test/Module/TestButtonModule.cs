using Framework.UI;
using UnityEngine;
using UnityEngine.Events;

public partial class TestButtonModule : BaseModule
{
    protected override void OnBind()
    {
        comps.togTest.isOn = false;
        comps.togTest.onValueChanged.AddListener(OnToggleTest);
    }

    protected override void OnUnBind()
    {
    }

    public void SetTestBtnOnClick(UnityAction action)
    {
        comps.btnTest.onClick.AddListener(action);
    }

    public void SetTestBtnText(string text)
    {
        comps.txtTest.text = text;
    }

    public void SetTestBtnInteractable(bool interactable)
    {
        comps.btnTest.interactable = interactable;
    }

    public void SetTestBtnImageColor(Color color)
    {
        comps.imgTest.color = color;
    }

    private void OnToggleTest(bool isOn)
    {
        if (isOn)
            comps.txtTest.text = "ON";
        else
            comps.txtTest.text = "OFF";
    }
}

