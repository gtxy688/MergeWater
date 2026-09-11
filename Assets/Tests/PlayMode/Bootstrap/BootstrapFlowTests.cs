using System.Collections;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Meta;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M7 验收 A3/A4 与 E1：隐私门控与「同意后直接进入对局」（R20、R26）。</summary>
    public sealed class BootstrapFlowTests
    {
        private BootstrapTestHarness _harness;

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
            _harness = null;
        }

        [UnityTest]
        public IEnumerator WhenPrivacyNotAccepted_AnalyticsAndAdsAreNotInitialized_AndPanelShown()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: false);
            yield return _harness.Activate();

            var context = _harness.Context;
            Assert.That(context, Is.Not.Null);

            Assert.That(context.Privacy.IsAccepted, Is.False);
            Assert.That(context.Analytics.IsInitialized, Is.False, "同意前不得初始化上报（R20）");
            Assert.That(context.Ads.IsAvailable, Is.False, "同意前不得初始化广告（R20）");
            Assert.That(context.Ads.Inner, Is.InstanceOf<NullAdsService>());
            Assert.That(context.Session, Is.Null, "未同意时不应开局");
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(PanelId.Privacy), "首启必现隐私弹窗");
            Assert.That(_harness.Panels.CurrentPanel != PanelId.None, Is.True);
        }

        [UnityTest]
        public IEnumerator AfterPrivacyAccepted_RoundStartsDirectlyWithoutHomePage()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            var context = _harness.Context;

            Assert.That(context.Privacy.IsAccepted, Is.True);
            Assert.That(context.Analytics.IsInitialized, Is.True, "同意后初始化上报");
            Assert.That(context.Ads.IsAvailable, Is.True, "同意后初始化广告适配器");
            Assert.That(context.Ads.Inner, Is.InstanceOf<MockAdsService>());
            Assert.That(context.Session, Is.Not.Null, "同意后直接进入对局（R26，无独立开始页）");
            Assert.That(context.Session.Phase, Is.EqualTo(RoundPhase.Ready));
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(PanelId.None), "不应停留在首页或弹窗");
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator AcceptButton_InitializesAnalyticsAndStartsRound()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: false);
            yield return _harness.Activate();

            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(PanelId.Privacy));

            // 走真实按钮点击路径
            _harness.View.privacyAcceptButton.onClick.Invoke();
            yield return null;

            var context = _harness.Context;
            Assert.That(context.Privacy.IsAccepted, Is.True);
            Assert.That(context.Analytics.IsInitialized, Is.True);
            Assert.That(context.Session, Is.Not.Null);
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(PanelId.None));
        }

        [UnityTest]
        public IEnumerator Analytics_BeforeConsent_DropsEvents()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: false);
            yield return _harness.Activate();

            _harness.Context.Analytics.Track(AnalyticsEventNames.AppLaunch);

            Assert.That(_harness.Analytics.Records, Is.Empty, "同意前事件应被丢弃");

            _harness.View.privacyAcceptButton.onClick.Invoke();
            yield return null;

            _harness.Context.Analytics.Track(AnalyticsEventNames.FirstDrop);
            Assert.That(_harness.Analytics.HasEvent(AnalyticsEventNames.FirstDrop), Is.True,
                "同意后事件应进入 sink");
        }

        [UnityTest]
        public IEnumerator SharedScene_OnAccept_RoundIsPlayableThroughRealAimPath()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            var session = _harness.Context.Session;
            var aim = _harness.Root.GetComponent<MergeWater.Aim.AimController>();
            var screen = _harness.Presentation.Camera.WorldToScreenPoint(Vector3.zero);
            var point = new Vector2(screen.x, screen.y);

            // 真实的「按下 → 松手」输入路径：AimController.DropRequested → GameContext → ReleaseDrop
            aim.HandleFrame(new MergeWater.Aim.PointerFrame(true, true, false, true, point));
            aim.HandleFrame(new MergeWater.Aim.PointerFrame(false, false, true, true, point));
            yield return null;

            Assert.That(session.Phase, Is.EqualTo(RoundPhase.Playing), "首次投放后进入 Playing");
            Assert.That(session.DropCount, Is.EqualTo(1));
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(1), "应真的在场地生成了水果");
        }

        [UnityTest]
        public IEnumerator MissingFieldReference_LogsErrorAndDoesNotThrow()
        {
            // 只有表现层、没有 GameField：应记录错误并停止对局初始化（E1）。
            var root = new GameObject("NoFieldRoot");
            root.SetActive(false);
            var presentation = PresentationHarness.Create(root.transform);
            presentation.CreateBinder();
            var bootstrapper = root.AddComponent<GameBootstrapper>();
            bootstrapper.SaveStoreOverride = new InMemorySaveStore();
            bootstrapper.AnalyticsSinkOverride = new InMemoryAnalyticsSink();
            bootstrapper.ConfigureReferences(null, null,
                presentation.Root.GetComponent<HudBinder>(), presentation.Panels, presentation.Audio,
                presentation.Feedback, null, null, null, null, presentation.Camera);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("缺少 GameField"));

            root.SetActive(true);
            yield return null;

            Assert.That(bootstrapper.Context, Is.Null, "缺少场地时不应构造上下文");

            Object.Destroy(root);
            presentation.Dispose();
        }
    }
}
