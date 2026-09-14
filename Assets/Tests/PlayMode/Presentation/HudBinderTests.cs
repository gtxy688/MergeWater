using System.Collections;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M5 验收 A1/A2/A4 与 E3：HUD 绑定、跨局重绑与面板表现。</summary>
    public sealed class HudBinderTests
    {
        private PresentationHarness _harness;
        private SessionStub _session;
        private AimStub _aim;

        [SetUp]
        public void SetUp()
        {
            _harness = PresentationHarness.Create();
            _session = new SessionStub();
            _aim = new AimStub();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
            _harness = null;
            _session = null;
            _aim = null;
        }

        [Test]
        public void Scored_RaisesScoreTextToSnapshotValue()
        {
            var binder = _harness.CreateBinder();
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);

            _session.RaiseScored(15, 15, 1.0f, 1);

            Assert.That(_harness.View.scoreText.text, Is.EqualTo("15"));
            Assert.That(_harness.View.bestScoreText.text, Does.Contain("15"));
        }

        [Test]
        public void BindAfterRebind_SubscribesNewEventsOnlyOnce()
        {
            var binder = _harness.CreateBinder();
            var first = new SessionStub();
            var second = new SessionStub();

            binder.Bind(first, _aim, GameBalance.CreateDefault(), 0);
            binder.Bind(second, _aim, GameBalance.CreateDefault(), 0);

            first.RaiseScored(50, 50, 1.0f, 1);
            Assert.That(_harness.View.scoreText.text, Is.Not.EqualTo("50"), "旧局事件不应再更新 HUD");

            second.RaiseScored(80, 80, 1.0f, 1);
            Assert.That(_harness.View.scoreText.text, Is.EqualTo("80"), "新局事件应生效");

            // 需求方（2026-09-12）把连击提示移到了合成位置的飘字，因此这里用分数文本验证「不重复订阅」：
            // 旧局再抬分不应影响 HUD（新局事件已生效且只订阅一次）。
            first.RaiseScored(99, 99, 1.0f, 1);
            Assert.That(_harness.View.scoreText.text, Is.EqualTo("80"), "旧局事件不应再更新 HUD");
        }

        [Test]
        public void Unbind_StopsReceivingEvents()
        {
            var binder = _harness.CreateBinder();
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);
            binder.Unbind();

            _session.RaiseScored(33, 33, 1.0f, 1);

            Assert.That(_harness.View.scoreText.text, Is.Not.EqualTo("33"));
        }

        [Test]
        public void DangerEvents_ToggleDangerLinePulse()
        {
            var binder = _harness.CreateBinder();
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);

            _session.RaiseDanger();
            Assert.That(_harness.DangerLine.IsPulsing, Is.True, "越线应开始脉冲（R6/V2.25）");

            _session.RaiseDangerEnded();
            Assert.That(_harness.DangerLine.IsPulsing, Is.False, "越线结束应停止脉冲");
        }

        [Test]
        public void PhaseToGameOver_ShowsSettlementWithoutRevive()
        {
            var binder = _harness.CreateBinder();
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);
            _session.RaiseScored(240, 240, 1.5f, 4);

            _session.RaisePhase(RoundPhase.Playing, RoundPhase.GameOver);

            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(Presentation.PanelId.Settlement));
            Assert.That(_harness.View.settlementScoreText.text, Does.Contain("240"));
            Assert.That(_harness.View.reviveButton.interactable, Is.False, "结算后不可复活");
        }

        [Test]
        public void PhaseToReviving_ShowsSettlementWithReviveAvailable()
        {
            var binder = _harness.CreateBinder();
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);

            _session.RaisePhase(RoundPhase.Playing, RoundPhase.Reviving);

            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(Presentation.PanelId.Settlement));
            Assert.That(_harness.View.reviveButton.interactable, Is.True, "失败页应可复活（R7）");
            Assert.That(_harness.View.reviveLabel.text, Does.Contain("复活"));
        }

        [Test]
        public void MilestoneReached_ShowsNoClaimToast()
        {
            var binder = _harness.CreateBinder();
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);

            // 清掉可能存在的旧 Toast，避免误判
            _harness.Panels.ShowToast(string.Empty, 0.01f);
            _session.RaiseMilestone(new StageMilestone(200, ItemKind.Undo));

            // 需求方（2026-09-12）：里程碑不再发奖励，因此也不再有「获得道具」提示（V2.38）。
            Assert.That(_harness.View.toastText.text, Does.Not.Contain("200"),
                "里程碑达成不应再弹奖励提示（奖励只通过广告获取）");
            Assert.That(_harness.View.toastText.text, Does.Not.Contain("获得"),
                "不应出现「获得道具」类提示");
        }

        [UnityTest]
        public IEnumerator Update_RefreshesPendingFruit()
        {
            var binder = _harness.CreateBinder();
            _session.Score = 0;
            _session.Snapshot = new RoundSnapshot(0, 0, 0, 1f, RoundPhase.Playing, 0, 3, 5, false, false, 0);
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);

            yield return null;

            // 需求方（2026-09-12）已移除右上角 NEXT 预览：HudBinder 不再刷新 nextFruitIcon/Label
            //（脚手架仍保留这两个字段以兼容旧用例；真场景里它们不会被创建，见 SceneAssetTests）。
            Assert.That(_harness.Preview.PendingVisible, Is.True, "对局中应显示待投水果");
        }

        [UnityTest]
        public IEnumerator Update_WhileAiming_DrawsTrajectory()
        {
            var binder = _harness.CreateBinder();
            _session.Snapshot = new RoundSnapshot(0, 0, 0, 1f, RoundPhase.Playing, 0, 1, 2, false, false, 0);
            binder.Bind(_session, _aim, GameBalance.CreateDefault(), 0);

            _aim.PreviewPointCount = 6;
            _aim.State = new AimState(true, 1.2f, 0.77f);

            yield return null;

            var line = _harness.Preview.GetComponentInChildren<LineRenderer>();
            Assert.That(line.enabled, Is.True, "瞄准时应显示预测虚线（R1）");
            Assert.That(line.positionCount, Is.EqualTo(6));
        }
    }
}
