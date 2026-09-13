using System;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>音效标识（对应 GDD §11 的 SFX 清单，≤10 个）。</summary>
    public enum SfxId
    {
        Drop = 0,
        Merge = 1,
        ComboUp = 2,
        DangerBeat = 3,
        Fail = 4,
        Button = 5,
        Claim = 6,
        Revive = 7
    }

    /// <summary>
    /// 占位音频合成（决策 D4）：用包络正弦/方波生成短音，无需音频资产即可听到反馈。
    /// 指派真实 <see cref="AudioClip"/> 后可直接替换，调用方不变。
    /// </summary>
    public static class PlaceholderAudioFactory
    {
        public const int SampleRate = 44100;

        public static AudioClip CreateTone(string name, float frequency, float seconds,
            float amplitude = 0.22f, float decay = 7f, bool square = false)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * Mathf.Max(0.01f, seconds)));
            var data = new float[count];
            var attack = Mathf.Max(1, Mathf.RoundToInt(SampleRate * 0.004f));

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)SampleRate;
                var phase = 2f * Mathf.PI * frequency * t;
                var wave = square ? (Mathf.Sin(phase) >= 0f ? 1f : -1f) : Mathf.Sin(phase);
                var envelope = Mathf.Exp(-decay * t) * Mathf.Min(1f, i / (float)attack);
                data[i] = wave * envelope * amplitude;
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateNoise(string name, float seconds, float amplitude = 0.18f, float decay = 9f)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * Mathf.Max(0.01f, seconds)));
            var data = new float[count];
            var rng = new System.Random(20260911);
            var smoothed = 0f;

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)SampleRate;
                var white = (float)(rng.NextDouble() * 2.0 - 1.0);
                smoothed = Mathf.Lerp(smoothed, white, 0.35f);
                data[i] = smoothed * Mathf.Exp(-decay * t) * amplitude;
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>按 SfxId 生成占位音；<see cref="SfxId.ComboUp"/> 复用合成音并由 pitch 决定音高。</summary>
        public static AudioClip Create(SfxId id)
        {
            switch (id)
            {
                case SfxId.Drop:
                    return CreateTone("mw_sfx_drop", 660f, 0.07f, 0.16f, 24f);
                case SfxId.Merge:
                    return CreateTone("mw_sfx_merge", 523.25f, 0.16f, 0.22f, 9f);
                case SfxId.ComboUp:
                    return CreateTone("mw_sfx_combo", 523.25f, 0.16f, 0.22f, 9f);
                case SfxId.DangerBeat:
                    return CreateTone("mw_sfx_danger", 196f, 0.14f, 0.2f, 6f, true);
                case SfxId.Fail:
                    return CreateNoise("mw_sfx_fail", 0.45f, 0.2f, 6f);
                case SfxId.Button:
                    return CreateTone("mw_sfx_button", 880f, 0.05f, 0.14f, 30f);
                case SfxId.Claim:
                    return CreateTone("mw_sfx_claim", 784f, 0.18f, 0.18f, 8f);
                case SfxId.Revive:
                    return CreateTone("mw_sfx_revive", 392f, 0.3f, 0.2f, 4f);
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 音频与震动开关（R25）。没有任何音频资产时使用占位合成音；BGM 槽位为空时静默降级并
    /// 只告警一次，不假装有音乐。
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>音效基准增益：音量条 100% 时音效源的音量。</summary>
        private const float SfxBaseGain = 1f;

        /// <summary>音乐基准增益：BGM 比音效轻，音量条 100% 时音乐源用 0.4。</summary>
        private const float MusicBaseGain = 0.4f;

        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip backgroundMusic;

        private readonly System.Collections.Generic.Dictionary<SfxId, AudioClip> _clips =
            new System.Collections.Generic.Dictionary<SfxId, AudioClip>();

        private bool _musicWarned;

        public bool SfxEnabled { get; private set; } = true;

        public bool MusicEnabled { get; private set; } = true;

        public bool VibrateEnabled { get; private set; } = true;

        /// <summary>音量条音量（0..1），默认 100%。与开关正交：开关管静音，音量条管响度。</summary>
        public float SfxVolume { get; private set; } = 1f;

        public float MusicVolume { get; private set; } = 1f;

        public bool HasMusicClip => backgroundMusic != null;

        public void Configure(AudioSource sfx, AudioSource music, AudioClip musicClip = null)
        {
            sfxSource = sfx;
            musicSource = music;
            if (musicClip != null)
                backgroundMusic = musicClip;

            // 音量可能先于音源注入（读档 → 应用设置），这里补一次同步。
            ApplyVolumes();
        }

        /// <summary>设置音效音量（0..1）。</summary>
        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        /// <summary>设置音乐音量（0..1）。</summary>
        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            if (sfxSource != null)
                sfxSource.volume = SfxBaseGain * SfxVolume;

            if (musicSource != null)
                musicSource.volume = MusicBaseGain * MusicVolume;
        }

        public void SetSfxEnabled(bool enabled)
        {
            SfxEnabled = enabled;
            if (!enabled && sfxSource != null)
                sfxSource.Stop();
        }

        public void SetMusicEnabled(bool enabled)
        {
            MusicEnabled = enabled;

            if (musicSource == null)
                return;

            if (enabled)
                PlayMusic();
            else
                musicSource.Stop();
        }

        public void SetVibrateEnabled(bool enabled) => VibrateEnabled = enabled;

        public void PlayMusic()
        {
            if (!MusicEnabled || musicSource == null)
                return;

            if (backgroundMusic == null)
            {
                if (!_musicWarned)
                {
                    _musicWarned = true;
                    Debug.Log("[AudioDirector] 未指派 BGM，占位阶段静默降级（无音乐）。");
                }

                return;
            }

            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.Play();
        }

        /// <summary>播放音效；<paramref name="pitch"/> 用于连击音阶（V2.24）。</summary>
        public void PlaySfx(SfxId id, float pitch = 1f)
        {
            if (!SfxEnabled || sfxSource == null)
                return;

            var clip = GetClip(id);
            if (clip == null)
                return;

            sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 4f);
            sfxSource.PlayOneShot(clip);
        }

        public void Vibrate()
        {
            if (!VibrateEnabled)
                return;

#if UNITY_ANDROID || UNITY_IOS
            if (Application.isMobilePlatform)
                Handheld.Vibrate();
#endif
        }

        public AudioClip GetClip(SfxId id)
        {
            if (_clips.TryGetValue(id, out var clip) && clip != null)
                return clip;

            try
            {
                clip = PlaceholderAudioFactory.Create(id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioDirector] 生成占位音失败（{id}）：{e.Message}");
                clip = null;
            }

            _clips[id] = clip;
            return clip;
        }

        private void OnDestroy()
        {
            foreach (var pair in _clips)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }

            _clips.Clear();
        }
    }
}
