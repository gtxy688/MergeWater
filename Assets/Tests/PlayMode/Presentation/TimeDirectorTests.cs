using MergeWater.Core;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M5 验收 A3/E1：慢放与顿帧结束后必须恢复 timeScale（V2.22/V2.23）。</summary>
    public sealed class TimeDirectorTests
    {
        private GameObject _root;
        private TimeDirector _director;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            _root = new GameObject("TimeDirectorTest");
            _director = _root.AddComponent<TimeDirector>();
            _director.enabled = false; // 由测试直接 Tick，避免真实帧干扰
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);

            _root = null;
            _director = null;
            Time.timeScale = 1f;
        }

        [Test]
        public void HitStop_FreezesThenRestoresTimeScale()
        {
            _director.RequestHitStop(0.1f);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "顿帧期间 timeScale 为 0");

            _director.Tick(0.05f);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            _director.Tick(0.06f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "顿帧结束必须恢复为 1");
            Assert.That(_director.IsBusy, Is.False);
        }

        [Test]
        public void SlowMoAndHitStop_FinishRestoreTimeScaleToOne()
        {
            _director.RequestSlowMo(0.2f, 0.15f);
            Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(1e-3f), "V2.22：慢放 0.2×");

            _director.RequestHitStop(0.08f);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "顿帧优先于慢放");

            _director.Tick(0.09f);
            Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(1e-3f), "顿帧结束后回到慢放");

            _director.Tick(0.16f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "全部结束后恢复");
        }

        [Test]
        public void NestedSlowMo_TakesStrongestScaleAndLongestDuration()
        {
            _director.RequestSlowMo(0.5f, 0.3f);
            _director.RequestSlowMo(0.2f, 0.1f);

            Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(1e-3f), "取更强的慢放");

            _director.Tick(0.11f);
            Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(1e-3f), "较长的一次仍在进行");

            _director.Tick(0.2f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidRequests_AreIgnored()
        {
            _director.RequestSlowMo(0.2f, 0f);
            _director.RequestHitStop(0f);
            _director.Tick(0f);
            _director.Tick(-1f);

            Assert.That(Time.timeScale, Is.EqualTo(1f), "非法请求不应改变时间缩放");
            Assert.That(_director.IsBusy, Is.False);
        }

        [Test]
        public void ResetToNormal_AlwaysRestoresOne()
        {
            _director.RequestHitStop(1f);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            _director.ResetToNormal();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void HitStopSeconds_FromFeelRules_StaysInRange()
        {
            var balance = GameBalance.CreateDefault();

            for (var combo = 1; combo <= 12; combo++)
            {
                var seconds = FeelRules.HitStopSeconds(combo, balance);
                Assert.That(seconds, Is.InRange(0.08f, 0.12f), $"{combo} 连击顿帧应在 80–120ms");
            }
        }
    }
}
