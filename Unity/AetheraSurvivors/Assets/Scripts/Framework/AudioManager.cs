
// ============================================================
// 文件名：AudioManager.cs
// 功能描述：音频管理器 — BGM/SFX统一管理
//          支持BGM淡入淡出、多通道SFX、音量控制、静音
//          适配微信小游戏：编辑器用AudioSource，微信用InnerAudioContext
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #49
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 音频管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. BGM播放/停止/切换（淡入淡出）
    /// 2. SFX多通道播放
    /// 3. 全局音量控制和静音开关
    /// 4. 微信小游戏平台适配
    /// 
    /// 微信小游戏适配说明：
    /// - 微信小游戏中Unity AudioSource可能受限
    /// - 后续需要通过WXBridge调用InnerAudioContext API
    /// - 当前版本先用Unity原生AudioSource，平台适配预留接口
    /// 
    /// 使用示例：
    ///   AudioManager.Instance.PlayBGM("Audio/BGM/bgm_battle");
    ///   AudioManager.Instance.PlaySFX("Audio/SFX/sfx_tower_shoot");
    ///   AudioManager.Instance.SetBGMVolume(0.5f);
    ///   AudioManager.Instance.MuteSFX = true;
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        // ========== 常量 ==========

        /// <summary>SFX最大同时播放通道数</summary>
        private const int MaxSFXChannels = 8;

        /// <summary>默认BGM淡入淡出时间</summary>
        private const float DefaultFadeTime = 0.5f;

        // ========== 私有字段 ==========

        /// <summary>BGM播放器</summary>
        private AudioSource _bgmSource;

        /// <summary>SFX播放器池</summary>
        private AudioSource[] _sfxSources;

        /// <summary>当前BGM路径</summary>
        private string _currentBGMPath;

        /// <summary>BGM音量（0-1）</summary>
        private float _bgmVolume = 1f;

        /// <summary>SFX音量（0-1）</summary>
        private float _sfxVolume = 1f;

        /// <summary>BGM是否静音</summary>
        private bool _bgmMuted;

        /// <summary>SFX是否静音</summary>
        private bool _sfxMuted;

        /// <summary>当前正在执行的BGM淡入淡出协程</summary>
        private Coroutine _bgmFadeCoroutine;

        /// <summary>SFX音频缓存（路径→AudioClip）</summary>
        private readonly Dictionary<string, AudioClip> _sfxCache = new Dictionary<string, AudioClip>(32);

        // ========== 公共属性 ==========

        /// <summary>BGM音量（0-1）</summary>
        public float BGMVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                if (_bgmSource != null && !_bgmMuted)
                {
                    _bgmSource.volume = _bgmVolume;
                }
            }
        }

        /// <summary>SFX音量（0-1）</summary>
        public float SFXVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        /// <summary>BGM是否静音</summary>
        public bool MuteBGM
        {
            get => _bgmMuted;
            set
            {
                _bgmMuted = value;
                if (_bgmSource != null)
                {
                    _bgmSource.volume = _bgmMuted ? 0f : _bgmVolume;
                }
            }
        }

        /// <summary>SFX是否静音</summary>
        public bool MuteSFX
        {
            get => _sfxMuted;
            set => _sfxMuted = value;
        }

        /// <summary>当前是否正在播放BGM</summary>
        public bool IsBGMPlaying => _bgmSource != null && _bgmSource.isPlaying;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 创建BGM AudioSource
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = _bgmVolume;

            // 创建SFX AudioSource池
            _sfxSources = new AudioSource[MaxSFXChannels];
            for (int i = 0; i < MaxSFXChannels; i++)
            {
                _sfxSources[i] = gameObject.AddComponent<AudioSource>();
                _sfxSources[i].loop = false;
                _sfxSources[i].playOnAwake = false;
            }
        }

        protected override void OnDispose()
        {
            StopBGM();
            StopAllSFX();
            _sfxCache.Clear();
        }

        // ========== 公共方法：BGM ==========

        /// <summary>
        /// 播放BGM（带淡入淡出）
        /// 如果当前已在播放同一首BGM，则忽略
        /// </summary>
        /// <param name="path">Resources下的音频路径</param>
        /// <param name="fadeTime">淡入淡出时间</param>
        /// <param name="loop">是否循环播放</param>
        public void PlayBGM(string path, float fadeTime = DefaultFadeTime, bool loop = true)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 相同BGM不重复播放
            if (_currentBGMPath == path && _bgmSource.isPlaying) return;

            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM加载失败: {path}");
                return;
            }

            _currentBGMPath = path;

            // 停止之前的淡入淡出
            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
            }

            if (fadeTime > 0f && _bgmSource.isPlaying)
            {
                // 淡出当前BGM → 切换 → 淡入新BGM
                _bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(clip, fadeTime, loop));
            }
            else
            {
                // 直接播放
                _bgmSource.clip = clip;
                _bgmSource.loop = loop;
                _bgmSource.volume = _bgmMuted ? 0f : _bgmVolume;
                _bgmSource.Play();
            }
        }

        /// <summary>
        /// 停止BGM（带淡出）
        /// </summary>
        public void StopBGM(float fadeTime = DefaultFadeTime)
        {
            if (!_bgmSource.isPlaying) return;

            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
            }

            if (fadeTime > 0f)
            {
                _bgmFadeCoroutine = StartCoroutine(FadeOutBGM(fadeTime));
            }
            else
            {
                _bgmSource.Stop();
                _currentBGMPath = null;
            }
        }

        /// <summary>
        /// 暂停BGM
        /// </summary>
        public void PauseBGM()
        {
            if (_bgmSource.isPlaying)
            {
                _bgmSource.Pause();
            }
        }

        /// <summary>
        /// 恢复BGM
        /// </summary>
        public void ResumeBGM()
        {
            if (!_bgmSource.isPlaying && _bgmSource.clip != null)
            {
                _bgmSource.UnPause();
            }
        }

        // ========== 公共方法：SFX ==========

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="path">Resources下的音频路径</param>
        /// <param name="volumeScale">音量缩放（在全局SFX音量基础上的倍率）</param>
        /// <param name="pitch">音调（1.0=正常）</param>
        public void PlaySFX(string path, float volumeScale = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(path) || _sfxMuted) return;

            // 从缓存或Resources加载AudioClip
            if (!_sfxCache.TryGetValue(path, out var clip))
            {
                clip = Resources.Load<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogWarning($"[AudioManager] SFX加载失败: {path}");
                    return;
                }
                _sfxCache[path] = clip;
            }

            PlaySFXClip(clip, volumeScale, pitch);
        }

        /// <summary>
        /// 直接播放AudioClip音效（不通过路径加载）
        /// </summary>
        public void PlaySFXClip(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null || _sfxMuted) return;

            // 查找空闲的SFX通道
            var source = GetFreeSFXSource();
            if (source == null)
            {
                // 所有通道都在播放，抢占最先播放的那个
                source = _sfxSources[0];
            }

            source.clip = clip;
            source.volume = _sfxVolume * volumeScale;
            source.pitch = pitch;
            source.Play();
        }

        /// <summary>
        /// 停止所有SFX
        /// </summary>
        public void StopAllSFX()
        {
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                if (_sfxSources[i].isPlaying)
                {
                    _sfxSources[i].Stop();
                }
            }
        }

        // ========== 公共方法：预加载 ==========

        /// <summary>
        /// 预加载SFX音效到缓存
        /// </summary>
        /// <param name="path">Resources下的音频路径</param>
        public void PreloadSFX(string path)
        {
            if (_sfxCache.ContainsKey(path)) return;

            var clip = Resources.Load<AudioClip>(path);
            if (clip != null)
            {
                _sfxCache[path] = clip;
            }
        }

        /// <summary>
        /// 批量预加载SFX
        /// </summary>
        public void PreloadSFXBatch(List<string> paths)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                PreloadSFX(paths[i]);
            }
        }

        /// <summary>
        /// 清空SFX缓存
        /// </summary>
        public void ClearSFXCache()
        {
            _sfxCache.Clear();
        }

        // ========== 私有方法 ==========

        /// <summary>获取空闲的SFX AudioSource</summary>
        private AudioSource GetFreeSFXSource()
        {
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    return _sfxSources[i];
                }
            }
            return null;
        }

        /// <summary>BGM交叉淡入淡出</summary>
        private IEnumerator CrossFadeBGM(AudioClip newClip, float fadeTime, bool loop)
        {
            float halfFade = fadeTime * 0.5f;

            // 淡出
            float startVolume = _bgmSource.volume;
            float timer = 0f;

            while (timer < halfFade)
            {
                timer += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / halfFade);
                yield return null;
            }

            // 切换曲目
            _bgmSource.Stop();
            _bgmSource.clip = newClip;
            _bgmSource.loop = loop;
            _bgmSource.Play();

            // 淡入
            float targetVolume = _bgmMuted ? 0f : _bgmVolume;
            timer = 0f;

            while (timer < halfFade)
            {
                timer += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, targetVolume, timer / halfFade);
                yield return null;
            }

            _bgmSource.volume = targetVolume;
            _bgmFadeCoroutine = null;
        }

        /// <summary>BGM淡出</summary>
        private IEnumerator FadeOutBGM(float fadeTime)
        {
            float startVolume = _bgmSource.volume;
            float timer = 0f;

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeTime);
                yield return null;
            }

            _bgmSource.Stop();
            _bgmSource.volume = _bgmMuted ? 0f : _bgmVolume;
            _currentBGMPath = null;
            _bgmFadeCoroutine = null;
        }
    }
}
