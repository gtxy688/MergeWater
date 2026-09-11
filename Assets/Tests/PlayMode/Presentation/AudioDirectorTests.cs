using System;
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
        public void PlayMusic_WithoutClip_LogsOnceAndStaysSilent()
        {
            var source = _root.AddComponent<AudioSource>();
            _audio.Configure(source, source);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("未指派 BGM"));

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
    }
}
