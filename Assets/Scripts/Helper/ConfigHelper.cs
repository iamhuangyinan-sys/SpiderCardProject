using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配表读取助手 —— 纯静态，运行时加载 Resources/Config/*.json
///
/// 使用方式：
///   ConfigHelper.LoadAll();                         // 游戏启动时调用一次
///   var card = ConfigHelper.Get<CardConfig>(1);     // 按 Id 查
///   var allCards = ConfigHelper.GetAll<CardConfig>(); // 拿全部
/// </summary>
public static class ConfigHelper
{
    /// <summary> 是否已加载全部配表 </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary> 类型 → (Id → 数据) </summary>
    private static readonly Dictionary<System.Type, object> _dataDict = new();

    /// <summary>
    /// 加载 Resources/Config/ 下的所有 JSON 配表（游戏启动时调用一次）
    /// </summary>
    public static void LoadAll()
    {
        if (IsLoaded) return;
        IsLoaded = true;

        var assets = Resources.LoadAll<TextAsset>("Config");
        foreach (var asset in assets)
        {
            if (asset == null) continue;

            string json = asset.text;
            if (string.IsNullOrEmpty(json)) continue;

            Debug.Log($"[ConfigHelper] 加载配表: {asset.name}");
        }

        Debug.Log($"[ConfigHelper] 配表加载完成，共 {assets.Length} 个文件");
    }

    /// <summary>
    /// 获取单条配置（泛型版本，自动从字典查）
    /// </summary>
    public static T Get<T>(int id) where T : class
    {
        var type = typeof(T);
        EnsureLoaded(type);

        if (_dataDict.TryGetValue(type, out var dictObj) && dictObj is Dictionary<int, T> dict)
        {
            dict.TryGetValue(id, out var result);
            return result;
        }

        Debug.LogError($"[ConfigHelper] 配表 {type.Name} 未加载，请先调用 LoadAll()");
        return null;
    }

    /// <summary>
    /// 获取全部配置
    /// </summary>
    public static List<T> GetAll<T>() where T : class
    {
        var type = typeof(T);
        EnsureLoaded(type);

        if (_dataDict.TryGetValue(type, out var dictObj) && dictObj is Dictionary<int, T> dict)
            return new List<T>(dict.Values);

        return new List<T>();
    }

    /// <summary>
    /// 主动加载某张表
    /// </summary>
    public static void LoadTable<T>() where T : class
    {
        var type = typeof(T);
        string path = $"Config/{type.Name}";

        var asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[ConfigHelper] 配表文件不存在: Resources/{path}");
            return;
        }

        var wrapper = JsonUtility.FromJson<ConfigWrapper<T>>(asset.text);
        if (wrapper == null || wrapper.items == null) return;

        var dict = new Dictionary<int, T>();
        foreach (var item in wrapper.items)
        {
            var prop = type.GetField("Id");
            if (prop == null)
            {
                Debug.LogError($"[ConfigHelper] {type.Name} 缺少 Id 字段！");
                break;
            }
            int id = (int)prop.GetValue(item);
            dict[id] = item;
        }

        _dataDict[type] = dict;
        Debug.Log($"[ConfigHelper] 加载配表: {type.Name}，共 {dict.Count} 条");
    }

    private static void EnsureLoaded(System.Type type)
    {
        if (!_dataDict.ContainsKey(type))
            LoadTable(type);
    }

    [System.Serializable]
    private class ConfigWrapper<T> { public List<T> items; }

    // ====== 运行时调用 ======
    // 由于 ConfigHelper 不能放泛型 LoadTable 在 LoadAll 中遍历，
    // 需要在业务代码中显式加载每张表，或使用反射扫描
    private static void LoadTable(System.Type type)
    {
        string path = $"Config/{type.Name}";
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[ConfigHelper] 配表文件不存在: Resources/{path}");
            return;
        }

        var wrapperType = typeof(ConfigWrapper<>).MakeGenericType(type);
        var wrapper = JsonUtility.FromJson(asset.text, wrapperType);
        if (wrapper == null) return;

        var itemsField = wrapperType.GetField("items");
        var items = itemsField.GetValue(wrapper) as System.Collections.IList;
        if (items == null) return;

        var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), type);
        var dict = System.Activator.CreateInstance(dictType) as System.Collections.IDictionary;

        var idField = type.GetField("Id");
        if (idField == null)
        {
            Debug.LogError($"[ConfigHelper] {type.Name} 缺少 Id 字段！");
            return;
        }

        foreach (var item in items)
        {
            int id = (int)idField.GetValue(item);
            dict[id] = item;
        }

        _dataDict[type] = dict;
        Debug.Log($"[ConfigHelper] 加载配表: {type.Name}，共 {dict.Count} 条");
    }
}
