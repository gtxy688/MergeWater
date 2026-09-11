using System.Collections;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M7 验收 A5/E2 与 R7：重开局重置、旧局事件不串局、复活编排。</summary>
    public sealed class RoundLifecycleTests
    {
        private BootstrapTestHarness _harness;

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
            _harness = null;
        }

        private static void DropOnce(BootstrapTestHarness harness)
        {
            var aim = harness.Root.GetComponent<MergeWater.Aim.AimController>();
            var screen = harness.Presentation.Camera.WorldToScreenPoint(Vector3.zero);
            var point = new Vector2(screen.x, screen.y);
            aim.HandleFrame(new MergeWater.Aim.PointerFrame(true, true, false, true, point));
            aim.HandleFrame(new MergeWater.Aim.PointerFrame(false, false, true, true, point));
        }

        [UnityTest]
        public IEnumerator NewRound_ResetsScoreComboDangerAndQueue_WithoutLeakingPreviousEvents()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            DropOnce(_harness);
            yield return null;
            Assert.That(_harness.Context.Session.Phase, Is.EqualTo(RoundPhase.Playing));

            // 直接触发一次合成，让本局有分数
            var radius = _harness.Context.Balance.GetTier(1).Radius;
            _harness.Field.SpawnAt(1, new Vector2(-radius + 0.01f, 0f), out _);
            _harness.Field.SpawnAt(1, new Vector2(radius - 0.01f, 0f), out _);
            yield return new WaitForSeconds(0.4f);

            var scoredSession = _harness.Context.Session;
            Assert.That(scoredSession.Score, Is.EqualTo(15), "1 级合成按 2 级基础分计 15");

            // 重开一局
            var fresh = _harness.Context.NewRound();
            Assert.That(fresh, Is.Not.Null);
            Assert.That(fresh.Score, Is.EqualTo(0), "重开应重置分数");
            Assert.That(fresh.Phase, Is.EqualTo(RoundPhase.Ready), "重开回到 Ready");
            Assert.That(fresh.DropCount, Is.EqualTo(0));
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(0), "重开应清空场地");

            // 旧局必须已解绑：再合成一次只应被新局计分一次
            var tier1 = _harness.Context.Balance.GetTier(1).Radius;
            _harness.Field.SpawnAt(1, new Vector2(-tier1 + 0.01f, 0f), out _);
            _harness.Field.SpawnAt(1, new Vector2(tier1 - 0.01f, 0f), out _);
            yield return new WaitForSeconds(0.4f);

            Assert.That(scoredSession.Score, Is.EqualTo(15), "旧局不应再收到场地事件（避免重复计分）");
        }

        [UnityTest]
        public IEnumerator RetryButton_CompletesRoundAndStartsNewOne()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            DropOnce(_harness);
            yield return null;
            _harness.Context.EndRound();
            yield return null;

            Assert.That(_harness.Context.Session.Phase, Is.EqualTo(RoundPhase.GameOver));
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(MergeWater.Presentation.PanelId.Settlement));

            _harness.View.retryButton.onClick.Invoke();
            yield return null;

            Assert.That(_harness.Store.Exists, Is.True);
            Assert.That(_harness.Context.Save.Data.gamesPlayed, Is.EqualTo(1), "结算应累计局数");
            Assert.That(_harness.Context.Session.Phase, Is.EqualTo(RoundPhase.Ready));
            Assert.That(_harness.Context.Session.Score, Is.EqualTo(0));
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(MergeWater.Presentation.PanelId.None));
        }

        [UnityTest]
        public IEnumerator Revive_WhenAdCompletes_RemovesClusterAndResumes_ThenSecondAttemptRejected()
        {
            // 重力设为 0：投放的水果会静止悬停在警戒线上方，从而稳定触发越线判负。
            _harness = BootstrapTestHarness.Create(privacyAccepted: true, gravity: 0f);
            yield return _harness.Activate();
            _harness.GetMockAds().Configure(completes: true, rewardedDelaySeconds: 0f, interstitialDelaySeconds: 0f);

            DropOnce(_harness);
            yield return null;
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(1));

            yield return new WaitForSeconds(_harness.Context.Balance.DangerHoldSeconds
                                            + _harness.Context.Balance.DangerSettleGraceSeconds + 0.5f);

            var session = _harness.Context.Session;
            Assert.That(session.Phase, Is.EqualTo(RoundPhase.Reviving), "越线满 1.2s 进入失败页（R6）");
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(MergeWater.Presentation.PanelId.Settlement));
            Assert.That(_harness.View.reviveButton.interactable, Is.True);

            var before = _harness.Field.LiveFruitCount;
            _harness.View.reviveButton.onClick.Invoke();
            yield return null;

            Assert.That(session.Phase, Is.EqualTo(RoundPhase.Playing), "复活后继续这一局（R7）");
            Assert.That(session.RevivesUsed, Is.EqualTo(1));
            Assert.That(session.CanRevive, Is.False, "每局限 1 次（V2.10）");
            Assert.That(_harness.Field.LiveFruitCount, Is.LessThan(before), "应清除最高一簇（≤3 颗）");
            Assert.That(_harness.Panels.CurrentPanel, Is.EqualTo(MergeWater.Presentation.PanelId.None));

            Assert.That(_harness.Context.Save.Data.lastReviveAdUtcTicks, Is.GreaterThan(0), "应记录 90s 冷却起点");
            Assert.That(_harness.GetMockAds().RewardedShown, Is.EqualTo(1), "复活只播放一次广告");
        }

        [UnityTest]
        public IEnumerator Revive_WhenAdSkipped_KeepsRevivingState()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true, gravity: 0f);
            yield return _harness.Activate();
            _harness.GetMockAds().Configure(completes: false, rewardedDelaySeconds: 0f);

            DropOnce(_harness);
            yield return null;

            yield return new WaitForSeconds(_harness.Context.Balance.DangerHoldSeconds
                                            + _harness.Context.Balance.DangerSettleGraceSeconds + 0.5f);

            var session = _harness.Context.Session;
            Assert.That(session.Phase, Is.EqualTo(RoundPhase.Reviving));

            var before = _harness.Field.LiveFruitCount;
            _harness.View.reviveButton.onClick.Invoke();
            yield return null;

            Assert.That(session.Phase, Is.EqualTo(RoundPhase.Reviving), "广告未完成不改变局状态");
            Assert.That(session.RevivesUsed, Is.EqualTo(0));
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(before), "不应清除任何水果");
            Assert.That(_harness.Context.Save.Data.lastReviveAdUtcTicks, Is.EqualTo(0), "未完成不应写冷却");
            Assert.That(_harness.GetMockAds().RewardedShown, Is.EqualTo(1));
        }
    }
}
