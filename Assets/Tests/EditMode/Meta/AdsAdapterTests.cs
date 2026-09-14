using MergeWater.Core;
using MergeWater.Meta;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 广告模式开关与三态映射（2026-09-14）：Mock / WeChat 的切换逻辑全部是纯逻辑，不必进 Play。
    ///
    /// <para>关键约定：WeChat 模式在「非微信运行时」或「adUnitId 为空」时必须**自动退回 Mock**，
    /// 否则编辑器里连复活流程都测不了；而真实适配器的可用性只看「有没有 adUnitId + 是不是微信小游戏运行时」。</para>
    /// </summary>
    public sealed class AdsAdapterTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp() => _host = new GameObject("AdsAdapterTest");

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.DestroyImmediate(_host);

            _host = null;
        }

        [Test]
        public void MockMode_CreatesMockAdapter_WithConfiguredResultAndDuration()
        {
            var service = AdsAdapterFactory.Create(AdsMode.Mock, string.Empty, MockAdResult.Cancel, 1.5f,
                _host.transform);

            var mock = service as MockAdsService;
            Assert.That(mock, Is.Not.Null, "Mock 模式应创建 MockAdsService");
            Assert.That(mock.RewardedResult, Is.EqualTo(MockAdResult.Cancel), "Inspector 选的模拟结果应生效");
            Assert.That(mock.RewardedSeconds, Is.EqualTo(1.5f).Within(0.001f),
                "模拟时长应为 1.5 秒（需求方要求 1~2 秒，不能点击即完成）");
        }

        [Test]
        public void MockAdapter_IsReusedInsteadOfStacked()
        {
            var first = AdsAdapterFactory.CreateMock(_host.transform, MockAdResult.Success, 0f);
            var second = AdsAdapterFactory.CreateMock(_host.transform, MockAdResult.Error, 0f);

            Assert.That(second, Is.SameAs(first), "同一宿主上不应叠加多个 Mock 适配器");
            Assert.That(first.RewardedResult, Is.EqualTo(MockAdResult.Error), "重复创建应更新配置");
        }

        [Test]
        public void WeChatMode_OutsideWeChatRuntime_FallsBackToMock()
        {
            Assume.That(WeChatAdsService.IsWeChatRuntime, Is.False,
                "本用例只在非微信运行时（编辑器 / PC）有意义");

            var service = AdsAdapterFactory.Create(AdsMode.WeChat, "adunit-xxxxxxxxxxxxxxxx",
                MockAdResult.Success, 0f, _host.transform);

            Assert.That(service, Is.InstanceOf<MockAdsService>(),
                "非微信运行时下 WeChat 模式必须自动降级为 Mock（并打告警）");
        }

        [Test]
        public void WeChatMode_WithoutAdUnitId_FallsBackToMock()
        {
            var service = AdsAdapterFactory.Create(AdsMode.WeChat, string.Empty, MockAdResult.Success, 0f,
                _host.transform);

            Assert.That(service, Is.InstanceOf<MockAdsService>(),
                "adUnitId 为空时必须降级为 Mock（尚未开通流量主的当前状态）");
        }

        [Test]
        public void WeChatService_Availability_DependsOnAdUnitIdAndRuntime()
        {
            var withoutId = new WeChatAdsService { RewardedAdUnitId = string.Empty };
            Assert.That(withoutId.IsAvailable, Is.False, "没有 adUnitId 一律不可用");

            var configured = new WeChatAdsService { RewardedAdUnitId = "adunit-xxxxxxxxxxxxxxxx" };
            Assert.That(configured.IsAvailable, Is.EqualTo(WeChatAdsService.IsWeChatRuntime),
                "有 adUnitId 时，是否可用只取决于「是不是微信小游戏运行时」");
        }

        [Test]
        public void MockResult_MapsToBusinessResults()
        {
            Assert.That(MockAdsService.ToRewardedResult(MockAdResult.Success), Is.EqualTo(RewardedResult.Completed),
                "完整播完 → 发奖");
            Assert.That(MockAdsService.ToRewardedResult(MockAdResult.Cancel), Is.EqualTo(RewardedResult.Skipped),
                "中途关闭 → 不发奖");
            Assert.That(MockAdsService.ToRewardedResult(MockAdResult.Error), Is.EqualTo(RewardedResult.Failed),
                "失败 → 不发奖并提示");
        }

        [Test]
        public void AdsRuntimeConfig_DefaultsToMockSuccessWithOnePointFiveSeconds()
        {
            var config = new AdsRuntimeConfig();

            Assert.That(config.Mode, Is.EqualTo(AdsMode.Mock), "默认必须是 Mock（当前没有真实广告位）");
            Assert.That(config.MockResult, Is.EqualTo(MockAdResult.Success));
            Assert.That(config.MockRewardedSeconds,
                Is.EqualTo(AdsAdapterFactory.DefaultMockRewardedSeconds).Within(0.001f));
            Assert.That(config.WeChatAdUnitId, Is.Empty, "默认不带广告位 id");
        }
    }
}
