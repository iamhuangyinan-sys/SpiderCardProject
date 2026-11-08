using System;

/// <summary>跨游戏局保留的玩家设置；字段初始值也是缺省设置。</summary>
[Serializable]
public class PlayerSettingData
{
    public E_LanguageEnum language = E_LanguageEnum.Chinese;
    public bool isFullScreen = true;
}
