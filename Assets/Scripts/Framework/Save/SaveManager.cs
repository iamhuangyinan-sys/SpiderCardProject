using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Framework.Mgr;

namespace Framework.Save
{
    // ============================================================
    //  基础类型存档数据结构（一个文件存所有基础 key）
    // ============================================================

    [Serializable]
    internal class BasicSaveWrapper
    {
        public List<BasicSaveEntry> entries = new List<BasicSaveEntry>();
    }

    [Serializable]
    internal class BasicSaveEntry
    {
        public string key;
        public string value;
    }

    // ============================================================
    //  SaveManager —— 本地存档管理器
    // ============================================================

    /// <summary>
    /// 本地存档管理器
    ///
    /// 基础类型（int/string/float/bool）：统一存入 basic.json，按 E_SaveBasicEnum 枚举查询
    /// 自定义类型：每个 key 存为独立 JSON 文件，按 E_SaveCustomEnum 枚举查询
    ///
    /// 使用流程：
    ///   1. 在 SaveEnums.cs 中添加你的 Key
    ///   2. 在 Init 中调用 RegisterBasic / RegisterCustom 注册
    ///   3. 用 Save / Load 读写数据
    ///
    /// 示例：
    ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.HighScore, 0);
    ///   SaveManager.Instance.Save(E_SaveBasicEnum.HighScore, 9999);
    ///   int score = SaveManager.Instance.Load&lt;int&gt;(E_SaveBasicEnum.HighScore);
    /// </summary>
    public class SaveManager : ManagerBase<SaveManager>
    {
        private string _saveDir;
        private string _basicFilePath;

        /// <summary> 基础 Key → 默认值（用于判断类型 + 缺省返回） </summary>
        private readonly Dictionary<E_SaveBasicEnum, object> _basicDefaults = new();

        /// <summary> 自定义 Key → 文件名（不含扩展名） </summary>
        private readonly Dictionary<E_SaveCustomEnum, string> _customPaths = new();

        /// <summary> 基础数据内存缓存 </summary>
        private BasicSaveWrapper _basicCache;

        private readonly object _fileLock = new();

        // ==================== 生命周期 ====================

        protected override void OnInit()
        {
            _saveDir = Path.Combine(Application.persistentDataPath, "Saves");
            _basicFilePath = Path.Combine(_saveDir, "basic.json");

            if (!Directory.Exists(_saveDir))
                Directory.CreateDirectory(_saveDir);
        }

        // ==================== 注册路径（集中写在 FrameworkEntry.RegisterSavePaths 中）====================

        /// <summary>
        /// 注册基础类型 Key
        /// </summary>
        /// <param name="key">枚举 Key</param>
        /// <param name="defaultValue">默认值（同时决定了该 Key 的数据类型）</param>
        /// <example>
        ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.HighScore, 0);          // int
        ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.PlayerName, "无名");    // string
        ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.Volume, 1.0f);          // float
        ///   SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.IsFirstLaunch, true);   // bool
        /// </example>
        public void RegisterBasic(E_SaveBasicEnum key, object defaultValue)
        {
            _basicDefaults[key] = defaultValue;
        }

        /// <summary>
        /// 注册自定义类型 Key
        /// </summary>
        /// <param name="key">枚举 Key</param>
        /// <param name="fileName">存档文件名（不含扩展名，如 "player_data"）</param>
        /// <example>
        ///   SaveManager.Instance.RegisterCustom&lt;PlayerData&gt;(E_SaveCustomEnum.PlayerData, "player_data");
        ///   SaveManager.Instance.RegisterCustom&lt;GameSettings&gt;(E_SaveCustomEnum.GameSettings, "game_settings");
        /// </example>
        public void RegisterCustom(E_SaveCustomEnum key, string fileName)
        {
            _customPaths[key] = fileName;
        }

        // ==================== 基础类型存取 ====================

        /// <summary>
        /// 保存基础类型数据（立即写入文件）
        /// </summary>
        public void Save<T>(E_SaveBasicEnum key, T value)
        {
            EnsureBasicLoaded();
            string keyName = key.ToString();

            lock (_fileLock)
            {
                // 更新内存缓存
                var entry = _basicCache.entries.Find(e => e.key == keyName);
                if (entry == null)
                {
                    entry = new BasicSaveEntry { key = keyName };
                    _basicCache.entries.Add(entry);
                }
                entry.value = value?.ToString();

                WriteBasicFile();
            }
        }

        /// <summary>
        /// 读取基础类型数据
        /// </summary>
        public T Load<T>(E_SaveBasicEnum key)
        {
            EnsureBasicLoaded();
            string keyName = key.ToString();

            lock (_fileLock)
            {
                var entry = _basicCache.entries.Find(e => e.key == keyName);
                if (entry == null || string.IsNullOrEmpty(entry.value))
                    return GetBasicDefault<T>(key);

                try
                {
                    return (T)Convert.ChangeType(entry.value, typeof(T));
                }
                catch
                {
                    Debug.LogWarning($"[SaveManager] 基础 Key [{keyName}] 值转换失败，返回默认值");
                    return GetBasicDefault<T>(key);
                }
            }
        }

        /// <summary>
        /// 检查基础 Key 是否已存过数据
        /// </summary>
        public bool HasKey(E_SaveBasicEnum key)
        {
            EnsureBasicLoaded();
            lock (_fileLock)
            {
                return _basicCache.entries.Exists(e => e.key == key.ToString());
            }
        }

        /// <summary>
        /// 删除某个基础 Key 的数据
        /// </summary>
        public void DeleteKey(E_SaveBasicEnum key)
        {
            EnsureBasicLoaded();
            lock (_fileLock)
            {
                _basicCache.entries.RemoveAll(e => e.key == key.ToString());
                WriteBasicFile();
            }
        }

        // ==================== 自定义类型存取 ====================

        /// <summary>
        /// 保存自定义类型数据为 JSON 文件
        /// </summary>
        public void Save<T>(E_SaveCustomEnum key, T data) where T : class
        {
            if (!_customPaths.TryGetValue(key, out string fileName))
            {
                Debug.LogError($"[SaveManager] 自定义 Key [{key}] 未注册！请先调用 RegisterCustom");
                return;
            }

            string filePath = GetCustomFilePath(fileName);
            string json = JsonUtility.ToJson(data, true);

            lock (_fileLock)
            {
                File.WriteAllText(filePath, json);
            }
        }

        /// <summary>
        /// 从 JSON 文件读取自定义类型数据
        /// </summary>
        public T Load<T>(E_SaveCustomEnum key) where T : class, new()
        {
            if (!_customPaths.TryGetValue(key, out string fileName))
            {
                Debug.LogError($"[SaveManager] 自定义 Key [{key}] 未注册！请先调用 RegisterCustom");
                return null;
            }

            string filePath = GetCustomFilePath(fileName);

            lock (_fileLock)
            {
                if (!File.Exists(filePath))
                    return new T();

                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonUtility.FromJson<T>(json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveManager] 读取自定义存档失败 [{fileName}]: {e.Message}");
                    return new T();
                }
            }
        }

        /// <summary>
        /// 检查自定义 Key 的存档文件是否存在
        /// </summary>
        public bool HasKey(E_SaveCustomEnum key)
        {
            if (!_customPaths.TryGetValue(key, out string fileName))
                return false;
            return File.Exists(GetCustomFilePath(fileName));
        }

        /// <summary>
        /// 删除某个自定义 Key 的存档文件
        /// </summary>
        public void DeleteKey(E_SaveCustomEnum key)
        {
            if (!_customPaths.TryGetValue(key, out string fileName))
                return;

            string filePath = GetCustomFilePath(fileName);
            lock (_fileLock)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        // ==================== 数据迁移（改名后使用）====================

        /// <summary>
        /// 迁移基础类型 Key：把旧枚举名对应的数据搬到新 Key 下，然后删掉旧数据
        /// 用法：枚举改名后，在 SaveRegistry.RegisterAll() 中调用一次即可
        /// </summary>
        /// <param name="oldKeyName">旧枚举成员名（字符串），如 "HighScore"</param>
        /// <param name="newKey">新枚举 Key</param>
        public void MigrateBasicKey(string oldKeyName, E_SaveBasicEnum newKey)
        {
            EnsureBasicLoaded();

            lock (_fileLock)
            {
                var oldEntry = _basicCache.entries.Find(e => e.key == oldKeyName);
                if (oldEntry == null) return;  // 旧数据不存在，无需迁移

                // 把旧值写入新 Key（如果新 Key 还没有数据）
                string newKeyName = newKey.ToString();
                var newEntry = _basicCache.entries.Find(e => e.key == newKeyName);
                if (newEntry == null)
                {
                    newEntry = new BasicSaveEntry { key = newKeyName, value = oldEntry.value };
                    _basicCache.entries.Add(newEntry);
                }
                else if (string.IsNullOrEmpty(newEntry.value))
                {
                    newEntry.value = oldEntry.value;
                }

                // 删除旧条目
                _basicCache.entries.Remove(oldEntry);
                WriteBasicFile();

                Debug.Log($"[SaveManager] 基础 Key 迁移：{oldKeyName} → {newKeyName}");
            }
        }

        /// <summary>
        /// 迁移自定义类型存档：把旧文件重命名为新 Key 对应的文件
        /// 用法：修改 RegisterCustom 的文件名后，在 SaveRegistry.RegisterAll() 中调用一次即可
        /// </summary>
        /// <param name="oldFileName">旧文件名（不含扩展名），如 "old_player"</param>
        /// <param name="newKey">新枚举 Key（需已注册）</param>
        public void MigrateCustomPath(string oldFileName, E_SaveCustomEnum newKey)
        {
            if (!_customPaths.TryGetValue(newKey, out string newFileName))
            {
                Debug.LogError($"[SaveManager] 迁移失败：{newKey} 尚未注册！请先 RegisterCustom");
                return;
            }

            string oldPath = GetCustomFilePath(oldFileName);
            string newPath = GetCustomFilePath(newFileName);

            lock (_fileLock)
            {
                if (!File.Exists(oldPath)) return;        // 旧文件不存在
                if (File.Exists(newPath)) return;         // 新文件已存在，不覆盖

                File.Move(oldPath, newPath);
                Debug.Log($"[SaveManager] 自定义存档迁移：{oldFileName}.json → {newFileName}.json");
            }
        }

        // ==================== 全局操作 ====================

        /// <summary>
        /// 删除所有存档文件
        /// </summary>
        public void ClearAll()
        {
            lock (_fileLock)
            {
                _basicCache?.entries.Clear();
                if (Directory.Exists(_saveDir))
                {
                    Directory.Delete(_saveDir, true);
                    Directory.CreateDirectory(_saveDir);
                }
            }
        }

        /// <summary>
        /// 立即将所有缓存写入磁盘
        /// </summary>
        public void Flush()
        {
            lock (_fileLock)
            {
                if (_basicCache != null)
                    WriteBasicFile();
            }
        }

        // ==================== 内部方法 ====================

        private void EnsureBasicLoaded()
        {
            if (_basicCache != null) return;

            lock (_fileLock)
            {
                if (_basicCache != null) return;

                if (File.Exists(_basicFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(_basicFilePath);
                        _basicCache = JsonUtility.FromJson<BasicSaveWrapper>(json) ?? new BasicSaveWrapper();
                    }
                    catch
                    {
                        _basicCache = new BasicSaveWrapper();
                    }
                }
                else
                {
                    _basicCache = new BasicSaveWrapper();
                }
            }
        }

        private void WriteBasicFile()
        {
            string json = JsonUtility.ToJson(_basicCache, true);
            File.WriteAllText(_basicFilePath, json);
        }

        private T GetBasicDefault<T>(E_SaveBasicEnum key)
        {
            if (_basicDefaults.TryGetValue(key, out object def) && def is T typedDef)
                return typedDef;
            return default;
        }

        private string GetCustomFilePath(string fileName)
        {
            return Path.Combine(_saveDir, fileName + ".json");
        }
    }
}
