using System.Collections;
using MergeWater.Core;
using MergeWater.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// Mock 广告的行为守卫（2026-09-14）。对应需求里的异常清单：
    /// 三种结果（完整播完 / 中途关闭 / 失败）、连续快速点击、播放中重复请求、界面销毁后回调。
    ///
    /// <para>测试里把模拟时长压到 0.05 秒，避免每次真等 1.5 秒；**默认时长本身**由
    /// EditMode 的 `AdsAdapterTests.MockMode_CreatesMockAdapter_WithConfiguredResultAndDuration` 守住。</para>
    /// </summary>
    public sealed class AdsMockTests
    {
        private GameObject _host;
        private MockAdsService _ads;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AdsMockTest");
            _ads = _host.AddComponent<MockAdsService>();
            _ads.Initialize();
            _ads.Configure(MockAdResult.Success, 0.05f, 0.05f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.Destroy(_host);

            _host = null;
            _ads = null;
        }

        [UnityTest]
        public IEnumerator Success_ReturnsCompleted_AndClearsShowingFlag()
        {
            RewardedResult? captured = null;
            _ads.ShowRewarded(AdPlacement.Revive, result => captured = result);

            Assert.That(_ads.IsShowing, Is.True, "播放期间 IsShowing 必须为真（M7 用它冻结输入 + 显示遮罩）");

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(captured, Is.EqualTo(RewardedResult.Completed), "完整播完应返回 Completed");
            Assert.That(_ads.IsShowing, Is.False, "播放结束必须复位 IsShowing");
        }

        [UnityTest]
        public IEnumerator Cancel_ReturnsSkipped()
        {
            _ads.Configure(MockAdResult.Cancel, 0.05f);

            RewardedResult? captured = null;
            _ads.ShowRewarded(AdPlacement.Revive, result => captured = result);
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(captured, Is.EqualTo(RewardedResult.Skipped), "中途关闭应返回 Skipped（不发奖）");
        }

        [UnityTest]
        public IEnumerator Error_ReturnsFailed()
        {
            _ads.Configure(MockAdResult.Error, 0.05f);

            RewardedResult? captured = null;
            _ads.ShowRewarded(AdPlacement.Revive, result => captured = result);
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(captured, Is.EqualTo(RewardedResult.Failed), "失败应返回 Failed（UI 提示稍后再试）");
        }

        [UnityTest]
        public IEnumerator SecondRequestWhileShowing_IsRejectedImmediately_AndOnlyOnePlaybackHappens()
        {
            var firstCallbacks = 0;
            RewardedResult? secondResult = null;

            _ads.ShowRewarded(AdPlacement.Revive, _ => firstCallbacks++);
            _ads.ShowRewarded(AdPlacement.Revive, result => secondResult = result); // 播放中的第二次请求

            Assert.That(secondResult, Is.EqualTo(RewardedResult.Unavailable),
                "播放中再次请求必须立即拒绝（连续点击广告按钮）");

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(_ads.RewardedShown, Is.EqualTo(1), "第二次请求不得真的播放一次广告");
            Assert.That(firstCallbacks, Is.EqualTo(1), "第一次请求只回调一次");
            Assert.That(_ads.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyedWhileShowing_StillInvokesCallbackWithFailure()
        {
            RewardedResult? captured = null;
            _ads.ShowRewarded(AdPlacement.Revive, result => captured = result);

            Object.Destroy(_host); // 播放中宿主被销毁（切场景 / 界面销毁）
            _host = null;

            yield return null;

            Assert.That(captured, Is.EqualTo(RewardedResult.Failed),
                "销毁后回调不能被吞掉——业务层会一直等，表现为结算页按钮卡死");
        }
    }
}
