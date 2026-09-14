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
    /// 一条「音效 → 真实素材」的指派（需求方 2026-09-13：合成音改用 `Assets/Audios/pop.ogg`）。
    /// 槽位是**场景序列化数据**：在 `Main.unity` 的 `GameRoot/Presentation` 上直接拖素材，不要在代码里写路径。
    /// </summary>
    [Serializable]
    public struct SfxClipEntry
    {
        [Tooltip("要替换的 SfxId（Merge 与 ComboUp 可指向同一素材，连击音高由 pitch 实现）")]
        public SfxId id;

        [Tooltip("真实音频素材；留空则回退到运行时合成的占位音")]
        public AudioClip clip;
    }

    /// <summary>
    /// 音频与震动开关（R25）。音效优先用 `sfxClips` 里指派的真实素材，未指派的 SfxId 回退到运行时
    /// 合成的占位音（决策 D4：没有音频资产时也能听到反馈）；BGM 槽位为空时静默降级，不假装有音乐。
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>音效基准增益：音量条 100% 时音效源的音量。</summary>
        private const float SfxBaseGain = 1f;

        /// <summary>音乐基准增益：BGM 比音效轻，音量条 100% 时音乐源用 0.4。</summary>
        private const float MusicBaseGain = 0.4f;

        [SerializeField] private SfxClipEntry[] sfxClips;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip backgroundMusic;

        private readonly System.Collections.Generic.Dictionary<SfxId, AudioClip> _clips =
            new System.Collections.Generic.Dictionary<SfxId, AudioClip>();

        /// <summary>运行时**自己合成**的占位音。只有这些能在销毁时释放——工程资产销毁会连素材本体一起删掉。</summary>
        private readonly System.Collections.Generic.HashSet<SfxId> _generatedClips =
            new System.Collections.Generic.HashSet<SfxId>();

        public bool SfxEnabled { get; private set; } = true;

        public bool MusicEnabled { get; private set; } = true;

        public bool VibrateEnabled { get; private set; } = true;

        /// <summary>音量条音量（0..1），默认 100%。与开关正交：开关管静音，音量条管响度。</summary>
        public float SfxVolume { get; private set; } = 1f;

        public float MusicVolume { get; private set; } = 1f;

        public bool HasMusicClip => backgroundMusic != null;

        /// <summary>该音效是否已有真实素材（未指派时用占位音兜底）——供场景装配门禁与诊断使用。</summary>
        public bool HasClip(SfxId id)
        {
            EnsureClipsLoaded();
            return _clips.TryGetValue(id, out var clip) && clip != null;
        }

        /// <summary>
        /// 把场景里指派的真实素材装进查询表（`GetClip` 会懒装配一次，因此 `AddComponent` 后立刻取也拿得到）。
        /// 后写的条目覆盖先写的——误配重复时以列表里靠后的为准，避免「拖了没反应」还查不出原因。
        /// </summary>
        private void EnsureClipsLoaded()
        {
            if (sfxClips == null)
                return;

            foreach (var entry in sfxClips)
            {
                if (entry.clip == null)
                    continue;

                _clips[entry.id] = entry.clip;
                _generatedClips.Remove(entry.id);
            }
        }

        public void Configure(AudioSource sfx, AudioSource music, AudioClip musicClip = null)
        {
            sfxSource = sfx;
            musicSource = music;
            if (musicClip != null)
                backgroundMusic = musicClip;

            EnsureClipsLoaded();

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

        /// <summary>
        /// 音乐是否该（重新）开始播放——纯判断，便于 EditMode 直接单测。
        ///
        /// <para>规则：**换了曲子**要重播；**当前没在播**要开始播（首次启动、被外部停掉、以及
        /// 「关闭音乐后再打开」——需求方 2026-09-14 明确要求只有这种情况才重头播）；
        /// **已经在放同一首则什么都不做**。</para>
        /// </summary>
        public static bool ShouldStartMusic(AudioClip currentClip, AudioClip wantedClip, bool isPlaying) =>
            currentClip != wantedClip || !isPlaying;

        /// <summary>
        /// 保证 BGM 正在播放（**幂等**）。
        ///
        /// <para>2026-09-14 需求方反馈「修改音效为什么会导致音乐重新播放」：任何设置变化都会走
        /// <c>SettingsService.Changed</c> → <c>GameContext.ApplySettingsToAudio</c> → 本方法，
        /// 而拖动音量条时存档值**逐帧**变化，原先每次都 <c>Play()</c>（等于把播放位置重置到 0），
        /// 听感上就是音乐被反复从头播放。现在只有「换曲子」或「当前没在播」才真正开始播放。</para>
        /// </summary>
        public void PlayMusic()
        {
            if (!MusicEnabled || musicSource == null)
                return;

            if (backgroundMusic == null)
            {
                // 未指派 BGM 是占位阶段的既定状态（本来就无音乐），不往 Console 输出：
                // 2026-09-13 需求方要求清掉运行期调试信息。
                return;
            }

            if (musicSource.clip != backgroundMusic)
                musicSource.clip = backgroundMusic;

            musicSource.loop = true;

            // 已经在放同一首 → 直接返回，不碰播放位置（这是修复的核心一行）。
            if (!ShouldStartMusic(musicSource.clip, backgroundMusic, musicSource.isPlaying))
                return;

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
            EnsureClipsLoaded();

            if (_clips.TryGetValue(id, out var clip) && clip != null)
                return clip;

            try
            {
                clip = PlaceholderAudioFactory.Create(id);
                if (clip != null)
                    _generatedClips.Add(id);
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
                // 只释放自己合成的占位音：`sfxClips` / `backgroundMusic` 是工程资产，
                // 对它们调 Destroy 会连素材本体一起删掉（pop.ogg、BGM 直接消失）。
                if (pair.Value != null && _generatedClips.Contains(pair.Key))
                    Destroy(pair.Value);
            }

            _clips.Clear();
            _generatedClips.Clear();
        }
    }
}
