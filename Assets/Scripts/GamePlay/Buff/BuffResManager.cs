using System.Collections.Generic;
using Framework.Mgr;
using UnityEngine;

public class BuffResManager : ManagerBase<BuffResManager>
{
    private const string IconDir = "GamePlay/Buff/";
    private readonly Dictionary<string, Sprite> _spriteCache = new();

    public Sprite GetIcon(E_BuffEnum id)
    {
        var icon = BuffHelper.GetConfig(id)?.Icon;
        if (string.IsNullOrEmpty(icon)) return null;

        var path = IconDir + icon;
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null) _spriteCache[path] = sprite;
        return sprite;
    }

    protected override void OnDispose()
    {
        _spriteCache.Clear();
    }
}
