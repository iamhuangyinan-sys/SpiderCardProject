using System;
using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 通用「图标 + 信息文字」模组 —— 图标（可点 / 可悬停 / 可弹提示）+ 右侧一行数字或说明
///
/// 用法（面板里用 mod_Xxx 命名该物体绑定）：
///   comps.modMenu.OnClick += OnClickMenu;       // 点图标；不设回调就只是展示
///   comps.modLevelInfo.SetInfo("12");           // 右侧文字
///   comps.modMap.Interactable = false;          // 置灰 + 不响应点击 / 悬停
///   comps.modMap.gameObject.SetActive(false);   // 整块显隐
///
/// 悬停提示（跟 LevelBtnModule / CardModule 一个套路：提示内容就是本模组的序列化字段）：
///   面板里选中某个 mod_Xxx 实例 → 勾上「固定提示」→ 填标题 / 正文 → 悬停直接弹，不用写代码。
///   不勾就完全不弹（默认不勾，纯数字模块保持安静）。
///   方向默认 Down（这几个图标在顶部，向下弹；靠近屏幕底边时 HoverTipPanel 会自动换方向）。
///
/// 点击走根节点上的 Button（预制体里已经挂好，target graphic 是根节点的 Image），
/// 所以 interactable 的置灰、点击过滤都由 Button 负责，这里只转发 onClick。
/// 悬停由本模组收指针事件，驱动同节点上的 HoverScaleBehaviour
/// （预制体里 autoTrigger 不勾，由这里接管，避免两边都触发）。
/// </summary>
public partial class IconInfoModule : BaseModule, IPointerEnterHandler, IPointerExitHandler
{
    [Header("悬停提示")]
    /// <summary>固定提示：勾上 = 悬停时直接显示下面填的文案（不勾没有任何提示）</summary>
    [SerializeField] private bool fixedTip = false;

    /// <summary>提示标题（可空；空了只显示正文）</summary>
    [SerializeField] private string tipTitle = string.Empty;

    /// <summary>提示正文（可多行）</summary>
    [SerializeField, TextArea(2, 6)] private string tipContent = string.Empty;

    /// <summary>提示框首选方向（放不下会自动换方向）</summary>
    [SerializeField] private E_TipDirection tipDirection = E_TipDirection.Down;

    /// <summary>提示框额外让出的距离（像素，默认 0；个别图标想再远一点就在预制体上调）</summary>
    [SerializeField] private float tipExtraGap = 0f;

    /// <summary>点击回调（null = 不响应点击）</summary>
    public Action OnClick;

    /// <summary>是否可点（false = 按钮置灰，也不响应悬停）</summary>
    public bool Interactable
    {
        get => _button != null ? _button.interactable : _interactable ?? true;
        set
        {
            _interactable = value;   // 模组默认失活时还没绑定按钮，先记下，OnBind 再应用
            ApplyInteractable();
        }
    }

    protected override void OnBind()
    {
        _hoverScale = GetComponent<HoverScaleBehaviour>();

        _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(HandleClick);

        // 绑定前设过的状态 / 文字在这里补上（模组可能默认失活，Awake 晚于宿主赋值）
        if (_interactable.HasValue) ApplyInteractable();
        else if (_button != null) _interactable = _button.interactable;

        if (_info != null && comps.txtInfo != null) comps.txtInfo.text = _info;
    }

    protected override void OnUnBind()
    {
        OnClick = null;   // 模组会进对象池复用，回调必须清掉

        HideTip();

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
            _button = null;
        }

        _hoverScale?.SetHover(false, true);
        _hoverScale = null;
        _info = null;
        _interactable = null;
    }

    /// <summary>设置右侧信息文字（控件还没绑定时先缓存，绑定后自动补上）</summary>
    public void SetInfo(string text)
    {
        _info = text;

        if (comps.txtInfo != null) comps.txtInfo.text = text;
    }

    /// <summary>信息文字显隐（只留图标）</summary>
    public void SetInfoVisible(bool visible)
    {
        if (comps.txtInfo != null) comps.txtInfo.gameObject.SetActive(visible);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;   // 置灰的按钮不给悬停反馈

        _hoverScale?.SetHover(true);
        ShowTip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hoverScale?.SetHover(false);
        HideTip();
    }

    /// <summary>失活（面板关掉 / 池化回收）时把提示收掉，不留残影</summary>
    private void OnDisable() => HideTip();

    // ==================== 悬停提示 ====================

    /// <summary>弹固定提示（没勾 fixedTip / 文案为空时什么都不做）</summary>
    public void ShowTip()
    {
        if (!fixedTip) return;
        if (string.IsNullOrEmpty(tipTitle) && string.IsNullOrEmpty(tipContent)) return;

        // 悬停时整个图标会放大 → 让提示框按「放大后的尺寸」定位，间距才不会被放大吃掉
        float targetScale = _hoverScale != null ? _hoverScale.HoverScale : 1f;

        HoverTipHelper.Show(this, tipTitle, tipContent, (RectTransform)transform,
            tipDirection, targetScale, tipExtraGap);
    }

    /// <summary>收起提示（只有自己的提示才会被关）</summary>
    public void HideTip() => HoverTipHelper.Hide(this);

    /// <summary>Button.onClick 的转发（Button 已经过滤了 interactable）</summary>
    private void HandleClick() => OnClick?.Invoke();

    /// <summary>把缓存的 interactable 写到按钮上（按钮没绑定时什么都不做）</summary>
    private void ApplyInteractable()
    {
        if (_button == null || !_interactable.HasValue) return;

        _button.interactable = _interactable.Value;

        if (!_interactable.Value)
        {
            _hoverScale?.SetHover(false);   // 悬停中被禁用 → 立刻收起
            HideTip();
        }
    }

    /// <summary>根节点上的按钮（没有就退化成只能看）</summary>
    private Button _button;

    /// <summary>期望的 interactable（null = 没设置过，沿用预制体上 Button 的值）</summary>
    private bool? _interactable;

    /// <summary>悬停缩放（同节点上；没有就退化成不缩放）</summary>
    private HoverScaleBehaviour _hoverScale;

    /// <summary>待显示的文字（控件绑定前 SetInfo 的缓存）</summary>
    private string _info;
}
