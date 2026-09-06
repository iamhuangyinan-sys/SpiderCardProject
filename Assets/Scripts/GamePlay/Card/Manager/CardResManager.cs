using Framework.Mgr;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 卡牌资源管理器 —— 加载牌背与花色符号（suit.png 为 multiple sprite）
/// </summary>
public class CardResManager : ManagerBase<CardResManager>
{
    /// <summary>纸牌贴图在 Resources 下的目录</summary>
    private const string ResDir = "GamePlay/Card/SampleCards/";

    /// <summary>花色符号（suit.png 的子精灵，顺序：红心、梅花、黑桃、方块）</summary>
    private Sprite[] _suitSprites;

    /// <summary>牌背贴图</summary>
    private Sprite _backSprite;

    /// <summary>正面空牌底</summary>
    private Sprite _emptySprite;

    protected override void OnInit()
    {
        Preload();
    }

    /// <summary>预加载牌背与花色符号</summary>
    public void Preload()
    {
        _backSprite = ResManager.Instance.Load<Sprite>(ResDir + "cardBack");
        _emptySprite = ResManager.Instance.Load<Sprite>(ResDir + "cardEmpty");
        _suitSprites = Resources.LoadAll<Sprite>(ResDir + "suit");
    }

    /// <summary>获取花色符号（按枚举映射到 suit.png 的子精灵索引）</summary>
    public Sprite GetSuitSprite(E_CardSuitEnum suit)
    {
        int index = SuitToSpriteIndex(suit);
        if (_suitSprites == null || index < 0 || index >= _suitSprites.Length) return null;
        return _suitSprites[index];
    }

    /// <summary>获取牌背贴图</summary>
    public Sprite GetBackSprite() => _backSprite;

    /// <summary>获取正面空牌底</summary>
    public Sprite GetEmptySprite() => _emptySprite;

    /// <summary>花色枚举 → suit.png 里的子精灵索引（0=红心、1=梅花、2=黑桃、3=方块）</summary>
    private static int SuitToSpriteIndex(E_CardSuitEnum suit)
    {
        switch (suit)
        {
            case E_CardSuitEnum.Hearts: return 0;
            case E_CardSuitEnum.Clubs: return 1;
            case E_CardSuitEnum.Spades: return 2;
            case E_CardSuitEnum.Diamonds: return 3;
            default: return 0;
        }
    }
}
