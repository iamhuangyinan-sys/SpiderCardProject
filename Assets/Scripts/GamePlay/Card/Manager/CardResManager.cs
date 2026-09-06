using System.Collections.Generic;
using Framework.Mgr;
using UnityEngine;

/// <summary>
/// 卡牌资源管理器 —— 加载牌背、牌底，解析配表的 Icon/Pattern 路径并加载 sprite
/// 路径约定：Icon 在 GamePlay/Card/Icon/，Pattern 在 GamePlay/Card/Pattern/
/// 配表 spec 格式："suit:0"（multiple 图子精灵）或 "xxx"（single 图）
/// </summary>
public class CardResManager : ManagerBase<CardResManager>
{
    private const string BG_DIR = "GamePlay/Card/Bg/";
    private const string ICON_DIR = "GamePlay/Card/Icon/";
    private const string PATTERN_DIR = "GamePlay/Card/Pattern/";

    /// <summary>sprite 缓存：完整 key（path:index 或 path）→ sprite</summary>
    private readonly Dictionary<string, Sprite> _spriteCache = new();

    private Sprite _backSprite;
    private Sprite _emptySprite;

    protected override void OnInit()
    {
        Preload();
    }

    /// <summary>预加载牌背与牌底</summary>
    public void Preload()
    {
        _backSprite = LoadSprite(BG_DIR + "cardBack", -1);
        _emptySprite = LoadSprite(BG_DIR + "cardEmpty", -1);
    }

    /// <summary>获取牌背贴图</summary>
    public Sprite GetBackSprite() => _backSprite;

    /// <summary>获取正面空牌底</summary>
    public Sprite GetEmptySprite() => _emptySprite;

    /// <summary>按 id 查配表</summary>
    public CardConfig GetCardConfig(string id)
    {
        return ConfigHelper.Get<CardConfig>(id);
    }

    /// <summary>获取图标 sprite（spec 如 "suit:0"）</summary>
    public Sprite GetIconSprite(string spec)
    {
        ParseSpec(spec, out string path, out int index);
        return LoadSprite(ICON_DIR + path, index);
    }

    /// <summary>获取图案 sprite（spec 如 "suit:0"）</summary>
    public Sprite GetPatternSprite(string spec)
    {
        ParseSpec(spec, out string path, out int index);
        return LoadSprite(PATTERN_DIR + path, index);
    }

    /// <summary>按资源路径加载 sprite（带缓存，multiple 图取子精灵）</summary>
    private Sprite LoadSprite(string resPath, int index)
    {
        string key = index >= 0 ? $"{resPath}:{index}" : resPath;
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;

        Sprite sprite;
        if (index >= 0)
        {
            var sprites = Resources.LoadAll<Sprite>(resPath);
            sprite = (sprites != null && index < sprites.Length) ? sprites[index] : null;
        }
        else
        {
            sprite = Resources.Load<Sprite>(resPath);
        }

        if (sprite != null) _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>解析 "path:index" 或 "path"</summary>
    private static void ParseSpec(string spec, out string path, out int index)
    {
        index = -1;
        path = spec ?? "";
        if (string.IsNullOrEmpty(spec)) return;

        int colon = spec.LastIndexOf(':');
        if (colon >= 0 && int.TryParse(spec.Substring(colon + 1), out index))
        {
            path = spec.Substring(0, colon);
        }
        else
        {
            path = spec;
        }
    }
}
