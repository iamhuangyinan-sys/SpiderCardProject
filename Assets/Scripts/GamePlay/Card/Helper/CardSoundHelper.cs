using Framework.Audio;

/// <summary>
/// 卡牌音效工具 —— 集中卡牌音效名与播放入口，业务层不写裸字符串。
/// 音效文件位于 Resources/Audio/Sound/Card/（SoundManager 自动拼 Audio/Sound/ 前缀）。
/// </summary>
public static class CardSoundHelper
{
    /// <summary>卡牌音效子目录</summary>
    private const string Root = "Card/";

    /// <summary>拿起牌</summary>
    private const string Pick = Root + "cardPick";

    /// <summary>放下牌（松开 / 飞牌落地）</summary>
    private const string Place = Root + "cardPlace";

    /// <summary>翻牌</summary>
    private const string Flip = Root + "cardFlip";

    /// <summary>拿起牌音效</summary>
    public static void PlayPick() => SoundManager.Instance.Play(Pick);

    /// <summary>放下牌音效（松开牌、飞牌共用）</summary>
    public static void PlayPlace() => SoundManager.Instance.Play(Place);

    /// <summary>翻牌音效</summary>
    public static void PlayFlip() => SoundManager.Instance.Play(Flip);
}
