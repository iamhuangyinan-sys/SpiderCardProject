using System.Collections;
using UnityEngine;
using Framework.Pool;

namespace Framework.Audio
{
    /// <summary>
    /// 音效播放器 —— 挂载在 Sound 预制体上
    /// 播完自动 Despawn 回池；被外部回收时自动停止播放
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundPlayer : MonoBehaviour
    {
        private AudioSource _source;
        private Coroutine _waitCoroutine;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
        }

        /// <summary>
        /// 播放一个音效，播完自动回池
        /// </summary>
        public void Play(AudioClip clip, float volume)
        {
            // 如果之前还在播，先停掉
            StopInternal();

            _source.clip = clip;
            _source.volume = volume;
            _source.Play();

            _waitCoroutine = StartCoroutine(WaitForEnd());
        }

        private IEnumerator WaitForEnd()
        {
            yield return new WaitForSeconds(_source.clip.length);
            _waitCoroutine = null;
            PoolManager.Instance.Despawn(gameObject);
        }

        /// <summary>
        /// 被回池或销毁时，停止播放
        /// </summary>
        private void OnDisable()
        {
            StopInternal();
        }

        private void StopInternal()
        {
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }
            if (_source != null)
                _source.Stop();
        }
    }
}
