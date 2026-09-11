using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择按钮模组 —— 显示关卡类型图标 + 名称 + 锁定状态
/// </summary>
public partial class LevelBtnModule : BaseModule
{
    private LevelConfig _cfg;
    private TextMeshProUGUI _txtType;
    private Image _imgIcon;

    protected override void OnBind()
    {
        comps.btnLevel.onClick.AddListener(OnClick);
        _txtType = comps.btnLevel.GetComponentInChildren<TextMeshProUGUI>();
        _imgIcon = comps.btnLevel.image;
    }

    protected override void OnUnBind()
    {
        comps.btnLevel.onClick.RemoveListener(OnClick);
        _txtType = null;
        _imgIcon = null;
    }

    /// <summary>绑定关卡配置并刷新</summary>
    public void Bind(LevelConfig cfg)
    {
        _cfg = cfg;
        RefreshIcon();
        RefreshState();
    }

    /// <summary>刷新关卡类型图标（走 LevelResManager → ResManager 缓存）</summary>
    private void RefreshIcon()
    {
        if (_cfg == null || _imgIcon == null) return;

        _imgIcon.sprite = LevelResManager.Instance.GetIcon(_cfg.LevelType);
    }

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

    private static string GetTypeName(int levelType)
    {
        switch ((E_LevelTypeEnum)levelType)
        {
            case E_LevelTypeEnum.Shop: return "商店";
            case E_LevelTypeEnum.Boss: return "BOSS";
            default: return "普通";
        }
    }

    private void OnClick()
    {
        if (_cfg == null) return;
        LevelManager.Instance.SelectLevel(_cfg.Id);
    }
}
