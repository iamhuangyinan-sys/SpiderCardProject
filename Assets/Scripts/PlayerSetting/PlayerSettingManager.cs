using System;
using System.Collections.Generic;
using Framework.Mgr;
using Framework.Save;
using UnityEngine;

/// <summary>读取、应用和保存玩家设置，生命周期独立于游戏局。</summary>
public class PlayerSettingManager : ManagerBase<PlayerSettingManager>
{
    public static IReadOnlyList<E_LanguageEnum> LanguageOrder { get; } = Array.AsReadOnly(new[]
    {
        E_LanguageEnum.Chinese,
        E_LanguageEnum.English,
    });

    public static IReadOnlyDictionary<E_LanguageEnum, string> LanguageNames { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<E_LanguageEnum, string>(
            new Dictionary<E_LanguageEnum, string>
            {
                { E_LanguageEnum.Chinese, "中文" },
                { E_LanguageEnum.English, "English" },
            });

    public E_LanguageEnum Language => PlayerSettingStore.Instance.data.language;
    public bool IsFullScreen => PlayerSettingStore.Instance.data.isFullScreen;

    protected override void OnInit()
    {
        var data = SaveManager.Instance.LoadWithDefaults<PlayerSettingData>(E_SaveCustomEnum.PlayerSetting)
            ?? new PlayerSettingData();
        if (!LanguageNames.ContainsKey(data.language))
            data.language = E_LanguageEnum.Chinese;

        PlayerSettingStore.Instance.data = data;
        LanguageHelper.SetLanguage(data.language);
        ApplyFullScreen(data.isFullScreen);
        // 首次启动创建默认档，也将缺省字段或损坏设置修复后写回。
        Save();
    }

    public void SetLanguage(E_LanguageEnum language)
    {
        if (!LanguageNames.ContainsKey(language) || Language == language) return;
        PlayerSettingStore.Instance.data.language = language;
        LanguageHelper.SetLanguage(language);
        Save();
    }

    public void SetFullScreen(bool isFullScreen)
    {
        if (IsFullScreen == isFullScreen) return;
        PlayerSettingStore.Instance.data.isFullScreen = isFullScreen;
        ApplyFullScreen(isFullScreen);
        Save();
    }

    private static void ApplyFullScreen(bool isFullScreen)
    {
#if !UNITY_EDITOR
        Screen.fullScreenMode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
#endif
    }

    private static void Save()
    {
        SaveManager.Instance.Save(E_SaveCustomEnum.PlayerSetting, PlayerSettingStore.Instance.data);
    }
}
