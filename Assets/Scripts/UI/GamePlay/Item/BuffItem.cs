using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class BuffItem : BaseListItem, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Tooltip("提示框首选方向，空间不足时自动换方向")]
    private E_TipDirection tipDirection = E_TipDirection.Left;

    [SerializeField, Min(0f), Tooltip("提示框额外间距，Canvas 单位")]
    private float tipExtraGap = 0f;

    [SerializeField, Tooltip("Buff 已激活时的图标颜色")]
    private Color activatedColor = new Color32(255, 111, 26, 255);

    [SerializeField, Tooltip("Tip 最后一行的激活提示文字颜色")]
    private Color activationHintColor = new Color32(255, 111, 26, 255);

    private Image _icon;
    private Color _normalColor;
    private UIFeedbackController _feedback;
    private BuffConfig _config;
    private E_BuffEnum _id;
    private bool _isActivated;
    private bool _isHovered;

    public override void OnActivate()
    {
        if (_icon == null)
        {
            _icon = comps.btnBuff.image;
            _normalColor = _icon.color;
        }

        comps.btnBuff.onClick.AddListener(ToggleActivation);
        _feedback ??= comps.btnBuff.GetComponent<UIFeedbackController>();
        _feedback?.ResetFeedback();
    }

    public void Bind(E_BuffEnum id, BuffState state)
    {
        bool activationChanged = _isActivated != state.IsActivated;
        _isActivated = state.IsActivated;
        _id = id;
        _config = BuffHelper.GetConfig(id);
        _icon.sprite = BuffResManager.Instance.GetIcon(id);
        _icon.enabled = _icon.sprite != null;
        _icon.color = _isActivated ? activatedColor : _normalColor;

        bool showCount = _config?.IsCount == true;
        comps.imgNum.gameObject.SetActive(showCount);
        comps.txtNum.text = showCount ? state.Count.ToString() : string.Empty;

        if (_isHovered && activationChanged) ShowTip();
    }

    private void ToggleActivation()
    {
        if (_config?.CanActivate == true) BuffManager.Instance.ToggleActivation(_id);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        ShowTip();
    }

    private void ShowTip()
    {
        if (_config == null) return;

        string description = LanguageHelper.Get(_config.Description);
        if (_config.CanActivate)
        {
            string hint = LanguageHelper.Get(_isActivated ? "buff_tip_deactivate" : "buff_tip_activate");
            if (!string.IsNullOrEmpty(description)) description += "\n";
            description += $"<color=#{ColorUtility.ToHtmlStringRGBA(activationHintColor)}><b>{hint}</b></color>";
        }

        HoverTipHelper.Show(this, LanguageHelper.Get(_config.Name), description,
            _icon.rectTransform, tipDirection, tipExtraGap);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTip();
    }

    private void OnDisable()
    {
        HideTip();
    }

    private void HideTip()
    {
        _isHovered = false;
        HoverTipHelper.Hide(this);
    }

    public override void OnRecycle()
    {
        comps.btnBuff.onClick.RemoveListener(ToggleActivation);
        HideTip();
        _feedback?.ResetFeedback();
        _config = null;
        _isActivated = false;
        _icon.sprite = null;
        _icon.color = _normalColor;
        comps.txtNum.text = string.Empty;
    }
}
