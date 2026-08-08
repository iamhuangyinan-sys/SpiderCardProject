using UnityEngine;
using UnityEngine.InputSystem;
using Framework.Audio;

/// <summary>
/// 音频测试 —— 挂场景 GameObject 上
/// 按 1 播放/暂停 BGM，按 2 播放音效
/// </summary>
public class SoundTest : MonoBehaviour
{
    private bool _bgmPlaying;

    void Update()
    {
        if (Keyboard.current == null) return;

        // ===== BGM 测试 =====
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (!_bgmPlaying)
            {
                BgmManager.Instance.Play("BgmTest");
                _bgmPlaying = true;
                Debug.Log("<color=cyan>[SoundTest] 开始播放 BGM</color>");
            }
            else
            {
                BgmManager.Instance.Pause();
                _bgmPlaying = false;
                Debug.Log("<color=cyan>[SoundTest] 暂停 BGM</color>");
            }
        }

        // ===== 音效测试 =====
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SoundManager.Instance.Play("ExplosionTest");
            Debug.Log("<color=green>[SoundTest] 播放音效 ExplosionTest</color>");
        }
    }
}
