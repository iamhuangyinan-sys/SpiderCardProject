using System.Collections.Generic;
using Framework.Mgr;
using UnityEngine;

/// <summary>
/// 卡牌资源管理器 —— 加载牌背与整张牌图
/// 资源都放在 Resources/GamePlay/Card/ 下
/// </summary>
public class CardResManager : ManagerBase<CardResManager>
{
    private const string CARD_DIR = "GamePlay/Card/";

    /// <summary>sprite 缓存：完整资源路径 → sprite</summary>
    private readonly Dictionary<string, Sprite> _spriteCache = new();

    private Sprite _backSprite;

    protected override void OnInit()
    {
        Preload();
    }

    /// <summary>预加载牌背</summary>
    public void Preload()
    {
        _backSprite = LoadSprite(CARD_DIR + "cardBack");
    }

    /// <summary>获取牌背贴图</summary>
    public Sprite GetBackSprite() => _backSprite;

    /// <summary>按 id 查配表</summary>
    public CardConfig GetCardConfig(string id)
    {
        return ConfigHelper.Get<CardConfig>(id);
    }

    /// <summary>获取整张牌图（imagePath 为相对 GamePlay/Card/ 的文件名，不含扩展名）</summary>
    public Sprite GetCardImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;
        return LoadSprite(CARD_DIR + imagePath);
    }

    private Sprite LoadSprite(string resPath)
    {
        if (_spriteCache.TryGetValue(resPath, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>(resPath);
        if (sprite != null) _spriteCache[resPath] = sprite;
        return sprite;
    }
}
