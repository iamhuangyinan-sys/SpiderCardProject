using Framework.UI;
using Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class SettingsPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FadeInOnOpen => true;
    protected override bool FadeOutOnClose => true;

    [SerializeField, Tooltip("开始界面进入时使用的黑底；菜单进入时使用菜单保留的黑底。")]
    private GameObject background;

    private ScrollRect settingsList;
    private Action returnToSource;

    /// <summary>打开设置并隐藏来源面板，返回时恢复同一个面板实例。</summary>
    public static void OpenFrom<T>(T source) where T : BasePanel
    {
        if (source == null || !source.IsOpen || source.IsTransitioning || SceneController.Instance.IsLoading
            || UIManager.Instance.IsOpen<SettingsPanel>()) return;

        source.HideSelf(() => OpenAfterSourceHidden(source));
    }

    private static void OpenAfterSourceHidden<T>(T source) where T : BasePanel
    {
        if (source == null || source.IsOpen || SceneController.Instance.IsLoading
            || UIManager.Instance.GetPanel<T>() != source) return;

        var panel = UIManager.Instance.Show<SettingsPanel>(settings =>
        {
            settings.background.SetActive(!(source is MenuPanel));
        });
        if (panel == null)
        {
            UIManager.Instance.Show<T>();
            return;
        }

        panel.returnToSource = () =>
        {
            if (source != null && !SceneController.Instance.IsLoading
                && UIManager.Instance.GetPanel<T>() == source)
                UIManager.Instance.Show<T>();
        };
    }

    protected override void OnOpen()
    {
        settingsList = GetComponentInChildren<ScrollRect>();
        var labels = new List<string>();
        foreach (var language in PlayerSettingManager.LanguageOrder)
            labels.Add(PlayerSettingManager.LanguageNames[language]);
        comps.drpLanguage.ClearOptions();
        comps.drpLanguage.AddOptions(labels);
        RefreshSettings();

        comps.drpLanguage.onValueChanged.AddListener(OnLanguageChanged);
        comps.togFullScreen.onValueChanged.AddListener(OnFullScreenChanged);
        comps.btnClose.onClick.AddListener(OnClickClose);
    }

    protected override void OnShow() => RefreshSettings();

    protected override void OnHide()
    {
        comps.drpLanguage.Hide();
        returnToSource = null;
    }

    protected override void OnClose()
    {
        comps.drpLanguage.Hide();
        comps.drpLanguage.onValueChanged.RemoveListener(OnLanguageChanged);
        comps.togFullScreen.onValueChanged.RemoveListener(OnFullScreenChanged);
        comps.btnClose.onClick.RemoveListener(OnClickClose);
        returnToSource = null;
    }

    private void OnClickClose()
    {
        if (!IsOpen || IsTransitioning) return;
        var returnAction = returnToSource;
        returnToSource = null;
        CloseSelf(returnAction);
    }

    private void RefreshSettings()
    {
        var manager = PlayerSettingManager.Instance;
        for (int i = 0; i < PlayerSettingManager.LanguageOrder.Count; i++)
        {
            if (PlayerSettingManager.LanguageOrder[i] == manager.Language)
            {
                comps.drpLanguage.SetValueWithoutNotify(i);
                break;
            }
        }
        comps.drpLanguage.RefreshShownValue();
        comps.togFullScreen.SetIsOnWithoutNotify(manager.IsFullScreen);

        // 首次显示前完成嵌套布局，避免使用预制体尚未计算的分组尺寸。
        LayoutRebuilder.ForceRebuildLayoutImmediate(settingsList.content);
        settingsList.verticalNormalizedPosition = 1f;
    }

    private void OnLanguageChanged(int index)
    {
        if (index < 0 || index >= PlayerSettingManager.LanguageOrder.Count) return;
        PlayerSettingManager.Instance.SetLanguage(PlayerSettingManager.LanguageOrder[index]);
    }

    private void OnFullScreenChanged(bool isFullScreen) => PlayerSettingManager.Instance.SetFullScreen(isFullScreen);
}
