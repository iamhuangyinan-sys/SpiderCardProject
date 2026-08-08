using UnityEngine;
using Framework.Res;
using Framework.Pool;
using Framework.Mgr;

namespace Framework.Audio
{
    /// <summary>
    /// 音效管理器 —— 通过对象池管理 Sound 预制体，异步加载 AudioClip 播放
    ///
    /// 使用方式：
    ///   SoundManager.Instance.Play("CardFlip");
    ///   SoundManager.Instance.Play("ButtonClick", 0.5f);
    ///   SoundManager.Instance.SetVolume(0.8f);
    /// </summary>
    public class SoundManager : ManagerBase<SoundManager>
    {
        /// <summary> 音效资源根路径 </summary>
        private const string SOUND_ROOT = "Audio/Sound/";

        /// <summary> Sound 预制体池路径 </summary>
        private const string POOL_PATH = "FrameworkPrefab/Sound/Sound";

        private float _volume = 1f;

        // ==================== 播放 ====================

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="name">音效文件名（不含路径和扩展名），如 "CardFlip"</param>
        /// <param name="volume">音效独立音量（会和全局音量相乘），默认 1</param>
        public void Play(string name, float volume = 1f)
        {
            string fullPath = SOUND_ROOT + name;

            ResManager.Instance.LoadAsync<AudioClip>(fullPath, clip =>
            {
                if (clip == null)
                {
                    Debug.LogError($"[SoundManager] 音效不存在: Resources/{fullPath}");
                    return;
                }

                PoolManager.Instance.SpawnAsync(POOL_PATH, go =>
                {
                    if (go == null) return;

                    var player = go.GetComponent<SoundPlayer>();
                    if (player == null)
                    {
                        Debug.LogError($"[SoundManager] Sound 预制体缺少 SoundPlayer 组件！");
                        PoolManager.Instance.Despawn(go);
                        return;
                    }

                    player.Play(clip, volume * _volume);
                });
            });
        }

        // ==================== 音量 ====================

        /// <summary>
        /// 设置全局音效音量（0 ~ 1），不影响正在播放的音效
        /// </summary>
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// 获取全局音效音量
        /// </summary>
        public float GetVolume() => _volume;
    }
}
