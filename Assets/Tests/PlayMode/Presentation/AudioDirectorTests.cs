using System;
using System.Collections;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M5 验收 A4：音效/音乐关闭时静默降级，占位音可生成（R22）。</summary>
    public sealed class AudioDirectorTests
    {
        private GameObject _root;
        private AudioDirector _audio;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AudioDirectorTest");
            _audio = _root.AddComponent<AudioDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);

            _root = null;
            _audio = null;
        }

        [Test]
        public void PlaySfx_WithoutSourceOrWithSfxDisabled_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.PlaySfx(SfxId.Merge), "没有 AudioSource 时应静默");

            var source = _root.AddComponent<AudioSource>();
            _audio.Configure(source, null);

            _audio.SetSfxEnabled(false);
            Assert.DoesNotThrow(() => _audio.PlaySfx(SfxId.Merge), "关闭音效后应静默");
            Assert.That(_audio.SfxEnabled, Is.False);
        }

        [Test]
        public void PlaceholderClips_AreGeneratedWithAudioData()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var clip = PlaceholderAudioFactory.Create(id);

                Assert.That(clip, Is.Not.Null, $"{id} 应有占位音");
                Assert.That(clip.samples, Is.GreaterThan(0), $"{id} 应有采样数据");
                Assert.That(clip.channels, Is.EqualTo(1));
            }
        }

        [Test]
        public void PlaceholderClips_AreCachedAndReleased()
        {
            var first = _audio.GetClip(SfxId.Drop);
            var second = _audio.GetClip(SfxId.Drop);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first), "同一音效应复用缓存，避免每次合成新对象");
        }

        [Test]
        public void PlayMusic_WithoutClip_StaysSilentWithoutLogging()
        {
            var source = _root.AddComponent<AudioSource>();
            _audio.Configure(source, source);

            // 2026-09-13 需求方要求清掉运行期调试信息：未指派 BGM 是占位阶段的既定状态（本来就无音乐），
            // 不再往 Console 输出——原先这里 Expect 一条 Log，该日志已删除（用例名随之去掉 LogsOnce）。
            _audio.PlayMusic();
            _audio.PlayMusic();

            Assert.That(_audio.HasMusicClip, Is.False);
            Assert.That(_audio.MusicEnabled, Is.True);
        }

        [Test]
        public void SetMusicEnabledFalse_StopsWithoutThrowing()
        {
            var source = _root.AddComponent<AudioSource>();
            _audio.Configure(null, source);

            Assert.DoesNotThrow(() => _audio.SetMusicEnabled(false));
            Assert.That(_audio.MusicEnabled, Is.False);
        }

        /// <summary>
        /// 2026-09-14 需求方缺陷的集成守卫：**任何设置变化都不得让正在播的音乐重头开始**。
        /// 设置变化会经 <c>SettingsService.Changed → GameContext.ApplySettingsToAudio</c> 调用
        /// <c>SetMusicEnabled(true)</c> 与 <c>PlayMusic()</c>；拖音量条时这是**逐帧**发生的，
        /// 所以下面把三条调用都模拟一遍，断言播放位置没有被重置。
        /// </summary>
        [UnityTest]
        public IEnumerator VolumeChange_DoesNotRestartMusic()
        {
            var clip = PlaceholderAudioFactory.CreateTone("mw_test_bgm", 440f, 5f);
            var source = _root.AddComponent<AudioSource>();
            _audio.Configure(source, source, clip);

            try
            {
                _audio.PlayMusic();

                if (!source.isPlaying)
                    Assert.Ignore("当前环境没有音频输出（批处理/无音频设备），跳过播放位置断言；" +
                                  "规则本身由 EditMode 的 MusicPlaybackTests 覆盖");

                // 直接把播放位置推到 2.27s：同步、不依赖音频时钟；若被重新 Play() 会被重置为 0。
                source.timeSamples = 100000;
                var before = source.timeSamples;

                _audio.SetMusicVolume(0.5f);   // 拖音量条：逐帧触发的那种变化
                _audio.SetMusicEnabled(true);  // ApplySettingsToAudio 的第一件事
                _audio.PlayMusic();            // ApplySettingsToAudio 末尾的「保证在放」

                Assert.That(source.clip, Is.SameAs(clip), "不得更换 clip");
                Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(before),
                    "已经在放的音乐不得被重头播放（缺陷现象：改音效/音乐音量导致音乐重播）");
            }
            finally
            {
                UnityEngine.Object.Destroy(clip);
            }

            yield return null;
        }

        /// <summary>反向守卫：**关闭音乐后再打开**应当从头播放（需求方 2026-09-14 明确要求）。</summary>
        [UnityTest]
        public IEnumerator MusicToggledOffThenOn_RestartsFromTheBeginning()
        {
            var clip = PlaceholderAudioFactory.CreateTone("mw_test_bgm", 440f, 5f);
            var source = _root.AddComponent<AudioSource>();
            _audio.Configure(source, source, clip);

            try
            {
                _audio.PlayMusic();

                if (!source.isPlaying)
                    Assert.Ignore("当前环境没有音频输出（批处理/无音频设备），跳过播放位置断言");

                source.timeSamples = 100000;
                var before = source.timeSamples;

                _audio.SetMusicEnabled(false);
                Assert.That(source.isPlaying, Is.False, "关闭音乐后应停止播放");

                _audio.SetMusicEnabled(true);
                Assert.That(source.isPlaying, Is.True, "重新打开后应继续有音乐");
                Assert.That(source.timeSamples, Is.LessThan(before),
                    "重新打开应当从头播放（关闭→打开是唯一允许重播的路径）");
            }
            finally
            {
                UnityEngine.Object.Destroy(clip);
            }

            yield return null;
        }
    }
}
