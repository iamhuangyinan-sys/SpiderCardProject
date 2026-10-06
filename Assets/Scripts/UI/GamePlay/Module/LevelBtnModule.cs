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

    /// <summary>悬停缩放（挂在 btn_Level 上）；没有就退化成不缩放、提示框按 1 倍算</summary>
    private HoverScaleBehaviour _hoverScale;

    protected override void OnBind()
    {
        comps.btnLevel.onClick.AddListener(OnClick);
        _txtType = comps.btnLevel.GetComponentInChildren<TextMeshProUGUI>();
        _imgIcon = comps.btnLevel.image;

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

    /// <summary>刷新显示：类型 + 锁定状态</summary>
    public void RefreshState()
    {
        if (_cfg == null) return;

        bool unlocked = LevelStore.Instance.IsUnlocked(_cfg.Id);
        comps.btnLevel.interactable = unlocked;

        if (_txtType != null)
        {
            _txtType.text = unlocked ? GetTypeName(_cfg.LevelType) : "锁定";
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
    /// 开局事件描述（暂写死，与 CardsManager.BuildLevelEventCardIds 一一对应）
    ///   AddSpider：洗入 1 张蜘蛛牌（0200005）
    ///   AddRandom：洗入 2 张随机梅花 A-K → 再加 1 张黑色牌（0300001）
    /// </summary>
    private static string GetEventDesc(int levelEvent)
    {
        switch ((E_LevelEventEnum)levelEvent)
        {
            case E_LevelEventEnum.AddSpider: return "洗入 1 张蜘蛛牌";
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
