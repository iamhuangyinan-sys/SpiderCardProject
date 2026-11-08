using System;
using System.Text;
using Framework.UI;
using Framework.Event;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 关卡选择按钮模组 —— 显示关卡类型图标 + 名称 + 锁定状态；悬停放大并弹出关卡信息
///
/// btn_Level 的 Feedback 负责交互缩放和呼吸；业务只设置是否需要提醒。
/// 提示单独接收指针进出，使不可点击的锁定/已走过关卡仍能查看信息。
/// </summary>
public partial class LevelBtnModule : BaseModule, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>提示框首选方向（放不下会自动换方向）</summary>
    [SerializeField] private E_TipDirection tipDirection = E_TipDirection.Up;
    [SerializeField, Min(0f), Tooltip("提示框额外间距，Canvas 单位")]
    private float tipExtraGap = 6f;

    /// <summary>
    /// 点击回调 —— 由 LevelPanel 赋值。
    /// 选关流程包含「刷白路线 + 停留一小段再进关」，统一交给面板处理
    /// </summary>
    public Action<RunLevelData> OnClickLevel;

    private RunLevelData _cfg;
    private TextMeshProUGUI _txtType;
    private Image _imgIcon;

    [SerializeField, Tooltip("未解锁或地图查看时的图标颜色乘数；与是否可点击分开控制")]
    private Color lockedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);

    private Color _normalIconColor;

    /// <summary>进关停留期间屏蔽点击（只挡点击，外观仍按解锁 / 走过的状态）</summary>
    private bool _clickBlocked;

    /// <summary>屏蔽期间是否把「可选但没走过」的关卡一并置灰（地图查看模式用）</summary>
    private bool _greyOutWhenBlocked;

    private UIFeedbackController _feedback;

    protected override void OnBind()
    {
        comps.btnLevel.onClick.AddListener(OnClick);
        EventManager.Instance.AddListener(E_EventEnum.OnLanguageChanged, RefreshState);
        _txtType = comps.btnLevel.GetComponentInChildren<TextMeshProUGUI>();
        _imgIcon = comps.btnLevel.image;

        if (_imgIcon != null) _normalIconColor = _imgIcon.color;
        _feedback = comps.btnLevel.GetComponent<UIFeedbackController>();
    }

    protected override void OnUnBind()
    {
        comps.btnLevel.onClick.RemoveListener(OnClick);
        EventManager.Instance.RemoveListener(E_EventEnum.OnLanguageChanged, RefreshState);

        OnClickLevel = null;      // 模组会进对象池复用，回调必须清掉

        _feedback?.ResetFeedback();
        HideTip();

        _feedback = null;
        _cfg = null;
        _txtType = null;
        _imgIcon = null;
    }

    /// <summary>绑定关卡配置并刷新</summary>
    public void Bind(RunLevelData cfg)
    {
        _cfg = cfg;
        _feedback?.ResetFeedback();

        // 模组是池化复用的：上一次进关时留下的「屏蔽点击」不能带过来
        _clickBlocked = false;
        _greyOutWhenBlocked = false;

        HideTip();   // 复用时先收掉上一个关卡的提示

        RefreshIcon();
        RefreshState();
    }

    /// <summary>按关卡类型刷新图标。</summary>
    private void RefreshIcon()
    {
        var sprite = _cfg != null ? LevelResManager.Instance.GetIcon(_cfg.LevelType) : null;
        if (_imgIcon != null)
            _imgIcon.sprite = sprite;
        if (comps.imgShadow != null)
            comps.imgShadow.sprite = sprite;
    }

    /// <summary>当前绑定的关卡 id（未绑定时为 null）</summary>
    public string LevelId => _cfg != null ? _cfg.Id : null;

    /// <summary>
    /// 屏蔽 / 恢复点击：进关停留、地图查看模式用。
    /// 只改「能不能点」，**不影响亮/灰**；
    /// greyOutWhenBlocked = true 时，被屏蔽期间「可选但没走过」的关卡也一并置灰
    /// （地图是关卡进行中打开的，其它可选关卡这时候本来就不能选，应该看起来是灰的）。
    /// </summary>
    /// <param name="blocked">是否屏蔽点击</param>
    /// <param name="greyOutWhenBlocked">屏蔽期间是否把可选关卡置灰</param>
    public void SetClickBlocked(bool blocked, bool greyOutWhenBlocked = false)
    {
        if (_clickBlocked == blocked && _greyOutWhenBlocked == greyOutWhenBlocked) return;

        _clickBlocked = blocked;
        _greyOutWhenBlocked = greyOutWhenBlocked;
        RefreshState();
    }

    /// <summary>
    /// 刷新显示：类型文本 + 亮/灰 + 可点状态。
    /// 四种情况：
    ///   走过的关卡（chosenLevelIds 里）→ 永远亮（包括正在打的那一关）
    ///   可选的关卡（unlockedIds 里） → 亮 + 可点 + 呼吸；进关停留时不可点但保持亮
    ///   可选的关卡 + 地图查看中    → 置灰（这时不能选，也不呼吸）
    ///   未解锁                     → 灰 + 文本「锁定」
    /// </summary>
    public void RefreshState()
    {
        if (_cfg == null) return;

        bool clickable = LevelStore.Instance.IsUnlocked(_cfg.Id);
        bool visited = LevelStore.Instance.HasChosen(_cfg.Id);

        // 被屏蔽 + 要求置灰（地图查看）时，可选但没走过的关卡也当灰的
        bool dimmed = _clickBlocked && _greyOutWhenBlocked;
        bool bright = visited || (clickable && !dimmed);

        comps.btnLevel.interactable = clickable && !_clickBlocked;
        // 池化重绑会清空 Feedback；即使按钮的 interactable 没有变化，也同步有效禁用状态。
        _feedback?.SetInteractable(comps.btnLevel.IsInteractable());

        // 亮/灰属于地图业务状态：已走过的关卡不可点击，但仍保持明亮。
        // 不再使用 Button.colors，也不挂 UIColorEffect，避免两个入口同时改图标颜色。
        if (_imgIcon != null) _imgIcon.color = bright ? _normalIconColor : _normalIconColor * lockedColor;

        // 只有真正可点的关卡才呼吸；进关停留期间停掉
        _feedback?.SetAttention(clickable && !_clickBlocked);

        if (_txtType != null)
        {
            // 文本按数据状态走：被地图置灰的关卡仍然显示类型名（它不是「锁定」）
            _txtType.text = clickable || visited
                ? GetTypeName(_cfg.LevelType)
                : LanguageHelper.Get("ui_level_locked");
        }
    }

    /// <summary>标注「玩家在这里」（具体定位由面板统一刷新）</summary>
    public void SetPlayerHere(bool value)
    {
        if (comps.imgArrow != null) comps.imgArrow.gameObject.SetActive(value);
    }

    private static string GetTypeName(int levelType)
    {
        switch ((E_LevelTypeEnum)levelType)
        {
            case E_LevelTypeEnum.Shop: return LanguageHelper.Get("ui_level_type_shop");
            case E_LevelTypeEnum.Boss: return "BOSS";
            default: return LanguageHelper.Get("ui_level_type_normal");
        }
    }

    // ==================== 悬停提示 ====================

    public void OnPointerEnter(PointerEventData eventData) => ShowTip();

    public void OnPointerExit(PointerEventData eventData) => HideTip();

    private void OnDisable() => HideTip();

    private void HideTip()
    {
        HoverTipHelper.Hide(this);
    }

    private void ShowTip()
    {
        if (_cfg == null) return;

        // 对齐目标用「可见按钮」：模组根节点是 0 尺寸的容器，不能拿它当基准
        var target = comps.btnLevel != null && comps.btnLevel.image != null
            ? comps.btnLevel.image.rectTransform
            : (RectTransform)transform;

        // 关卡提示不写标题（普通关 / 商店关 / BOSS 关都不标注自己），信息全写在正文里
        HoverTipHelper.Show(this, null, BuildTipContent(_cfg), target, tipDirection, tipExtraGap);
    }

    /// <summary>
    /// 提示正文 —— 文案全部走 LanguageConfig 表（Id 见 Config/Excel/LanguageConfig.xlsx）
    /// </summary>
    private static string BuildTipContent(RunLevelData cfg)
    {
        // 商店关没有牌局，只说它是商店
        if ((E_LevelTypeEnum)cfg.LevelType == E_LevelTypeEnum.Shop)
        {
            return LanguageHelper.Get("ui_level_type_shop");
        }

        var sb = new StringBuilder();
        sb.Append(LanguageHelper.Get("ui_level_tip_columns", cfg.ColumnNum));
        sb.Append('\n').Append(LanguageHelper.Get("ui_level_tip_pockets", cfg.PocketNum));
        sb.Append('\n').Append(LanguageHelper.Get("ui_level_tip_target", cfg.NeedStraightNum));
        if (cfg is RunLevelData node)
        {
            sb.Append('\n').Append(LanguageHelper.Get("ui_level_tip_initial_cards", string.Join(" / ", node.initialCards)));
        }
        sb.Append('\n').Append(LanguageHelper.Get("ui_level_tip_reward", cfg.FinishReward));

        string evt = GetEventDesc(cfg.LevelEvent);
        if (!string.IsNullOrEmpty(evt))
        {
            sb.Append('\n').Append(evt);
        }

        return sb.ToString();
    }

    /// <summary>开局事件描述（读配表 LevelEventConfig.Description；0 = 没有开局事件）</summary>
    private static string GetEventDesc(int levelEvent)
    {
        if (levelEvent == 0) return string.Empty;

        var cfg = ConfigHelper.Get<LevelEventConfig>(levelEvent.ToString());
        return cfg != null ? LanguageHelper.Get(cfg.Description) : string.Empty;
    }

    private void OnClick()
    {
        if (_cfg == null) return;

        // 面板接管了选关流程；没有接管时（比如单独调试模组）退化成直接进入
        if (OnClickLevel != null) OnClickLevel.Invoke(_cfg);
        else LevelManager.Instance.SelectLevel(_cfg.Id);
    }
}
