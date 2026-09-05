using System;
using System.Collections.Generic;
using Framework.Mgr;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 卡牌资源管理器 —— 封装 ResManager，统一加载/预加载纸牌贴图
/// 当前加载整张牌面图，后续可拆分为「花色 + 点数」两部分
/// </summary>
public class CardResManager : ManagerBase<CardResManager>
{
    /// <summary>纸牌贴图在 Resources 下的目录</summary>
    private const string ResDir = "GamePlay/Card/SampleCards/";

    /// <summary>文件名（不含扩展名）→ 牌面 Sprite</summary>
    private readonly Dictionary<string, Sprite> _faceSprites = new();

    /// <summary>牌背贴图</summary>
    private Sprite _backSprite;

    protected override void OnInit()
    {
        Preload();
    }

    /// <summary>预加载所有纸牌贴图（牌背 + 52 张牌面）</summary>
    public void Preload()
    {
        _backSprite = ResManager.Instance.Load<Sprite>(ResDir + "cardBack");

        foreach (E_CardSuitEnum suit in Enum.GetValues(typeof(E_CardSuitEnum)))
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                string fileName = GetFileName(suit, rank);
                var sprite = ResManager.Instance.Load<Sprite>(ResDir + fileName);
                if (sprite != null)
                {
                    _faceSprites[fileName] = sprite;
                }
            }
        }
    }

    /// <summary>获取牌面贴图</summary>
    public Sprite GetFaceSprite(E_CardSuitEnum suit, int rank)
    {
        string fileName = GetFileName(suit, rank);
        return _faceSprites.TryGetValue(fileName, out var sprite) ? sprite : null;
    }

    /// <summary>获取牌背贴图</summary>
    public Sprite GetBackSprite() => _backSprite;

    /// <summary>拼装文件名：card + 花色 + 点数（如 cardHeartsA、cardClubs10）</summary>
    private static string GetFileName(E_CardSuitEnum suit, int rank)
    {
        return "card" + suit + RankToString(rank);
    }

    /// <summary>点数转字符串：1→A，11→J，12→Q，13→K，其余为数字</summary>
    private static string RankToString(int rank)
    {
        switch (rank)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return rank.ToString();
        }
    }
}
