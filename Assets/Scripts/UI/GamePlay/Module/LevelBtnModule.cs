using System;
using System.Text;
using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择按钮模组 —— 显示关卡类型图标 + 名称 + 锁定状态；悬停放大并弹出关卡信息
///
/// 悬停缩放挂在子节点 btn_Level 上（只有它有指针接收面），这里订阅它的 OnHoverChanged
/// 来驱动提示框 —— 跟 CardModule 那套是同一个套路。
/// </summary>
public partial class LevelBtnModule : BaseModule
{
    /// <summary>提示框首选方向（放不下会自动换方向）</summary>
    [SerializeField] private E_TipDirection tipDirection = E_TipDirection.Up;

    /// <summary>
    /// 点击回调 —— 由 LevelPanel 赋值。
    /// 选关流程包含「刷白路线 + 停留一小段再进关」，统一交给面板处理
    /// </summary>
    public Action<LevelConfig> OnClickLevel;

    private LevelConfig _cfg;
    private TextMeshProUGUI _txtType;
    private Image _imgIcon;

    /// <summary>预制体上配的 Button 禁用色 —— 未解锁关卡沿用它</summary>
    private Color _lockedColor;

    /// <summary>进关停留期间屏蔽点击（只挡点击，外观仍按解锁 / 走过的状态）</summary>
    private bool _clickBlocked;

    /// <summary>悬停缩放（挂在 btn_Level 上）；没有就退化成不缩放、提示框按 1 倍算</summary>
    private HoverScaleBehaviour _hoverScale;

    protected override void OnBind()
    {
        comps.btnLevel.onClick.AddListener(OnClick);
        _txtType = comps.btnLevel.GetComponentInChildren<TextMeshProUGUI>();
        _imgIcon = comps.btnLevel.image;

        // 记住预制体上的禁用色：未解锁关卡用它，走过的关卡改用正常色（亮但不可点）
        _lockedColor = comps.btnLevel.colors.disabledColor;

        _hoverScale = GetComponentInChildren<HoverScaleBehaviour>(true);
        if (_hoverScale != null) _hoverScale.OnHoverChanged = OnHoverChanged;
    }

    protected override void OnUnBind()
    {
        comps.btnLevel.onClick.RemoveListener(OnClick);

        OnClickLevel = null;      // 模组会进对象池复用，回调必须清掉

        if (_hoverScale != null) _hoverScale.OnHoverChanged = null;

        HideTip();

        _hoverScale = null;
        _txtType = null;
        _imgIcon = null;
    }

    /// <summary>绑定关卡配置并刷新</summary>
    public void Bind(LevelConfig cfg)
    {
        _cfg = cfg;

        // 模组是池化复用的：上一次进关时留下的「屏蔽点击」不能带过来
        _clickBlocked = false;

        HideTip();   // 复用时先收掉上一个关卡的提示

        RefreshIcon();
        RefreshState();
    }

    /// <summary>刷新关卡类型图标（走 LevelResManager → ResManager 缓存）</summary>
    private void RefreshIcon()
    {
        if (_cfg == null || _imgIcon == null) return;

        _imgIcon.sprite = LevelResManager.Instance.GetIcon(_cfg.LevelType);
    }

    /// <summary>当前绑定的关卡 id（未绑定时为 null）</summary>
    public string LevelId => _cfg != null ? _cfg.Id : null;

    /// <summary>
    /// 屏蔽 / 恢复点击：进关停留期间用。
    /// 只改「能不能点」，**不影响亮/灰**（选中的关卡不会因为不可点而变灰）
    /// </summary>
    public void SetClickBlocked(bool blocked)
    {
        if (_clickBlocked == blocked) return;

        _clickBlocked = blocked;
        RefreshState();
    }

    /// <summary>
    /// 刷新显示：类型文本 + 亮/灰 + 可点状态。
    /// 三种状态：
    ///   走过的关卡（chosenLevelIds 里）→ 亮，但不可点（就是已经打过的关）
    ///   可选的关卡（unlockedIds 里） → 亮，且可点（进关停留期间临时不可点）
    ///   未解锁                     → 灰，不可点
    /// </summary>
    public void RefreshState()
    {
        if (_cfg == null) return;

        bool clickable = LevelStore.Instance.IsUnlocked(_cfg.Id);
        bool visited = LevelStore.Instance.HasChosen(_cfg.Id);

        // 亮 = 可选 或 走过；只有未解锁才是灰
        bool bright = clickable || visited;

        comps.btnLevel.interactable = clickable && !_clickBlocked;

        // Button 在 interactable = false 时会强制把图标染成 disabledColor：
        // 亮着的关卡把它换成正常色，就做到了「亮着但（暂时）点不动」
        var colors = comps.btnLevel.colors;
        colors.disabledColor = bright ? colors.normalColor : _lockedColor;
        comps.btnLevel.colors = colors;

        // 只有真正可点的关卡才呼吸；进关停留期间停掉
        _hoverScale?.SetBreathing(clickable && !_clickBlocked);

        if (_txtType != null)
        {
            _txtType.text = bright ? GetTypeName(_cfg.LevelType) : "锁定";
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
            case E_LevelTypeEnum.Shop: return "商店";
            case E_LevelTypeEnum.Boss: return "BOSS";
            default: return "普通";
        }
    }

    // ==================== 悬停提示 ====================

    private void OnHoverChanged(bool hovering)
    {
        if (!hovering)
        {
            HideTip();
            return;
        }

        ShowTip();
    }

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

        // 悬停时整个按钮会放大 → 提示框按放大后的尺寸定位，间距才不会被吃掉
        float targetScale = _hoverScale != null ? _hoverScale.HoverScale : 1f;

        // 关卡提示不写标题（普通关 / 商店关 / BOSS 关都不标注自己），信息全写在正文里
        HoverTipHelper.Show(this, null, BuildTipContent(_cfg), target, tipDirection, targetScale);
    }

    /// <summary>
    /// 提示正文 —— 目前全部写死在代码里，后续可以改成事件 / 关卡配表驱动
    /// </summary>
    private static string BuildTipContent(LevelConfig cfg)
    {
        // 商店关没有牌局，只说它是商店
        if ((E_LevelTypeEnum)cfg.LevelType == E_LevelTypeEnum.Shop)
        {
            return "商店";
        }

        var sb = new StringBuilder();
        sb.Append("场上列数 ").Append(cfg.ColumnNum);
        sb.Append('\n').Append("牌包数 ").Append(cfg.PocketNum);
        sb.Append('\n').Append("通关金币 ").Append(cfg.FinishReward);

        string evt = GetEventDesc(cfg.LevelEvent);
        if (!string.IsNullOrEmpty(evt))
        {
            sb.Append('\n').Append("开局事件：").Append(evt);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 开局事件描述（暂写死，与 CardsManager.BuildLevelEventDefs 一一对应）
    ///   AddSpider：洗入 1 张万能蜘蛛（可连接任意点数花色牌）
    ///   AddRandom：洗入 2 张随机梅花 A-K → 再加 1 张无花色无点数的牌
    /// </summary>
    private static string GetEventDesc(int levelEvent)
    {
        switch ((E_LevelEventEnum)levelEvent)
        {
            case E_LevelEventEnum.AddSpider: return "洗入 1 张万能蜘蛛";
            case E_LevelEventEnum.AddRandom: return "洗入 2 张随机梅花牌 + 1 张黑色牌";
            default: return string.Empty;
        }
    }

    private void OnClick()
    {
        if (_cfg == null) return;

        // 面板接管了选关流程；没有接管时（比如单独调试模组）退化成直接进入
        if (OnClickLevel != null) OnClickLevel.Invoke(_cfg);
        else LevelManager.Instance.SelectLevel(_cfg.Id);
    }
}
