using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 音乐播放的**幂等规则**（2026-09-14 缺陷的回归守卫）。
    ///
    /// <para>现象（需求方）：「修改音效为什么会导致音乐重新播放」。根因是
    /// <c>AudioDirector.PlayMusic</c> 原先无条件 <c>Play()</c>，而任何设置变化都会经
    /// <c>SettingsService.Changed → GameContext.ApplySettingsToAudio</c> 走到它；
    /// 拖音量条时存档值**逐帧**变化 → 逐帧重播。</para>
    ///
    /// <para>规则收敛到 <see cref="AudioDirector.ShouldStartMusic"/> 这个纯函数：只有「换了曲子」或
    /// 「当前没在播」才重新开始；已经在放同一首时什么都不做。这里只测规则本身（不依赖音频设备），
    /// 集成行为见 PlayMode 的 <c>AudioDirectorTests.VolumeChange_DoesNotRestartMusic</c>。</para>
    /// </summary>
    public sealed class MusicPlaybackTests
    {
        [Test]
        public void SameClipWhilePlaying_DoesNotRestart()
        {
            var clip = PlaceholderAudioFactory.CreateTone("mw_test_bgm", 440f, 0.1f);
            try
            {
                Assert.That(AudioDirector.ShouldStartMusic(clip, clip, isPlaying: true), Is.False,
                    "已经在放同一首时不得重新开始——调音量会逐帧走到这里（2026-09-14 缺陷）");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void NoClipAssignedYet_Starts()
        {
            var clip = PlaceholderAudioFactory.CreateTone("mw_test_bgm", 440f, 0.1f);
            try
            {
                Assert.That(AudioDirector.ShouldStartMusic(null, clip, isPlaying: false), Is.True,
                    "首次启动（还没放）应当开始播放");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void StoppedClip_StartsAgainFromTheBeginning()
        {
            var clip = PlaceholderAudioFactory.CreateTone("mw_test_bgm", 440f, 0.1f);
            try
            {
                // 「关闭音乐后再打开」走的正是这条：Stop() 之后 isPlaying=false → 重新开始。
                // 需求方 2026-09-14 明确要求：只有这种情况才让音乐重新播放。
                Assert.That(AudioDirector.ShouldStartMusic(clip, clip, isPlaying: false), Is.True,
                    "关闭音乐后再打开应当从头播放");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void DifferentClip_Starts()
        {
            var oldClip = PlaceholderAudioFactory.CreateTone("mw_test_bgm_old", 440f, 0.1f);
            var newClip = PlaceholderAudioFactory.CreateTone("mw_test_bgm_new", 523.25f, 0.1f);
            try
            {
                Assert.That(AudioDirector.ShouldStartMusic(oldClip, newClip, isPlaying: true), Is.True,
                    "换了曲子应当重播");
            }
            finally
            {
                Object.DestroyImmediate(oldClip);
                Object.DestroyImmediate(newClip);
            }
        }
    }
}
