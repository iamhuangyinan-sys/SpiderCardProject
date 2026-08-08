using UnityEngine;
using Framework.Res;
using Framework.Mgr;

namespace Framework.Audio
{
    /// <summary>
    /// 背景音乐管理器 —— 管理 [BGM] 播放器 GameObject，过场景不销毁，循环播放
    ///
    /// 使用方式：
    ///   BgmManager.Instance.Play("MainTheme");
    ///   BgmManager.Instance.Pause();
    ///   BgmManager.Instance.Resume();
    ///   BgmManager.Instance.Stop();
    ///   BgmManager.Instance.SetVolume(0.5f);
    /// </summary>
    public class BgmManager : ManagerBase<BgmManager>
    {
        /// <summary> BGM 资源根路径 </summary>
        private const string BGM_ROOT = "Audio/BGM/";

        private GameObject _bgmGo;
        private AudioSource _audioSource;
        private string _currentPath;
        private float _volume = 1f;

        protected override void OnInit()
        {
            // 创建 [BGM] 播放器 GameObject
            _bgmGo = new GameObject("[BGM]");
            Object.DontDestroyOnLoad(_bgmGo);

            _audioSource = _bgmGo.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.volume = _volume;
        }

        protected override void OnDispose()
        {
            if (_bgmGo != null)
                Object.Destroy(_bgmGo);
        }

        // ==================== 播放控制 ====================

        /// <summary>
        /// 播放背景音乐（异步加载，自动循环）
        /// </summary>
        /// <param name="name">BGM 文件名（不含路径和扩展名），如 "MainTheme"</param>
        public void Play(string name)
        {
            string fullPath = BGM_ROOT + name;

            if (_currentPath == fullPath && _audioSource.clip != null)
            {
                if (!_audioSource.isPlaying)
                    _audioSource.Play();
                return;
            }

            _currentPath = fullPath;
            ResManager.Instance.LoadAsync<AudioClip>(fullPath, clip =>
            {
                if (clip == null)
                {
                    Debug.LogError($"[BgmManager] 音频不存在: Resources/{fullPath}");
                    return;
                }

                _audioSource.clip = clip;
                _audioSource.Play();
                Debug.Log($"[BgmManager] 播放: {name}");
            });
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            _audioSource.Pause();
        }

        /// <summary>
        /// 继续播放
        /// </summary>
        public void Resume()
        {
            _audioSource.UnPause();
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            _audioSource.Stop();
            _currentPath = null;
        }

        // ==================== 音量 ====================

        /// <summary>
        /// 设置音量（0 ~ 1）
        /// </summary>
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            _audioSource.volume = _volume;
        }

        /// <summary>
        /// 获取当前音量
        /// </summary>
        public float GetVolume() => _volume;

        // ==================== 状态 ====================

        /// <summary> 是否正在播放 </summary>
        public bool IsPlaying => _audioSource.isPlaying;

        /// <summary> 当前播放的音频路径 </summary>
        public string CurrentPath => _currentPath;
    }
}
