using System.Collections.Generic;
using MergeWater.Core;
using MergeWater.Session;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M3 验收 A1–A10 与边界 E1–E4：局内流程（R4–R7、R9、R10）。</summary>
    public sealed class RoundSessionTests
    {
        private GameBalance _balance;
        private FakeField _field;
        private RoundSession _session;
        private readonly List<StageMilestone> _grantedItems = new List<StageMilestone>();
        private readonly List<int> _bestScoreReports = new List<int>();
        private readonly List<DangerViolation> _dangerStarts = new List<DangerViolation>();
        private int _dangerEnds;

        [SetUp]
        public void SetUp()
        {
            _balance = GameBalance.CreateDefault();
            _field = new FakeField();
            _grantedItems.Clear();
            _bestScoreReports.Clear();
            _dangerStarts.Clear();
            _dangerEnds = 0;

            _session = new RoundSession(_balance, _field, null, 0, 20260911,
                best => _bestScoreReports.Add(best),
                milestone => _grantedItems.Add(milestone));

            _session.Events.DangerStarted += violation => _dangerStarts.Add(violation);
            _session.Events.DangerEnded += () => _dangerEnds++;
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            _session = null;
        }

        private void EnterPlaying()
        {
            _session.StartRound();
            Assert.That(_session.ReleaseDrop(0f), Is.True);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing), "首次投放应进入 Playing");
        }

        private void ForceDangerAndExpire()
        {
            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);
            _session.Tick(_balance.DangerHoldSeconds + 0.01f);
        }

        // A1
        [Test]
        public void StartRound_ResetsStateAndEntersReady_ThenFirstDropEntersPlaying()
        {
            _session.StartRound();

            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Ready));
            Assert.That(_session.Score, Is.EqualTo(0));
            Assert.That(_session.DropCount, Is.EqualTo(0));
            Assert.That(_field.ClearAllCalls, Is.EqualTo(1), "开局应清理场地");
            Assert.That(_session.CanAcceptDrop, Is.True, "Ready 阶段必须允许首次投放，否则游戏无法开始");
            Assert.That(_session.CanAct, Is.False, "Ready 阶段不允许使用道具");

            Assert.That(_session.ReleaseDrop(0f), Is.True);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing));
            Assert.That(_session.DropCount, Is.EqualTo(1));
        }

        // A2
        [Test]
        public void ReleaseDrop_WhilePlaying_UsesQueueTierAndAdvancesQueue()
        {
            _session.StartRound();
            var firstLevel = _session.CurrentLevel;
            var nextLevel = _session.NextLevel;

            Assert.That(firstLevel, Is.InRange(1, 2), "V2.4：前 20 投只出 1–2 级");

            _session.ReleaseDrop(1.5f);

            Assert.That(_field.Drops[0].Key, Is.EqualTo(firstLevel));
            Assert.That(_field.Drops[0].Value, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(_session.CurrentLevel, Is.EqualTo(nextLevel), "投放后当前等级应前移");
        }

        // A3
        [Test]
        public void ReleaseDrop_WhenFieldRejects_DoesNotConsumeQueue()
        {
            _session.StartRound();
            _field.AcceptDrops = false;
            var level = _session.CurrentLevel;

            Assert.That(_session.ReleaseDrop(0f), Is.False);
            Assert.That(_session.DropCount, Is.EqualTo(0));
            Assert.That(_session.CurrentLevel, Is.EqualTo(level), "生成失败不应消耗队列");
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing), "阶段已推进但不计数");
        }

        // A4
        [Test]
        public void Merged_AddsScoreWithMultiplier_AndTracksBestScore()
        {
            EnterPlaying();

            _field.SimulateMerge(2);
            Assert.That(_session.Score, Is.EqualTo(15), "2 级基础分 15 × 1.0");
            Assert.That(_session.Snapshot.Multiplier, Is.EqualTo(1.0f).Within(1e-4f));

            _field.SimulateMerge(3);
            Assert.That(_session.Score, Is.EqualTo(38), "21 × 1.1 = 23.1 → 23，累计 38");
            Assert.That(_session.BestScore, Is.EqualTo(38));
            Assert.That(_bestScoreReports, Is.Not.Empty, "破纪录应回调持久化");
        }

        // A5
        [Test]
        public void Combo_WithinThreeSeconds_IncreasesThenResetsAfterWindow()
        {
            EnterPlaying();

            _field.SimulateMerge(2);
            _field.SimulateMerge(2);

            Assert.That(_session.Snapshot.Combo, Is.EqualTo(2));
            Assert.That(_session.Snapshot.Multiplier, Is.EqualTo(1.1f).Within(1e-4f));

            _session.Tick(3.0f);
            Assert.That(_session.Snapshot.Combo, Is.EqualTo(2), "恰好 3.0s 不归零");

            _session.Tick(0.05f);
            Assert.That(_session.Snapshot.Combo, Is.EqualTo(0), "超过窗口后连击归零");
            Assert.That(_session.Snapshot.Multiplier, Is.EqualTo(1.0f).Within(1e-4f));

            _field.SimulateMerge(2);
            Assert.That(_session.Snapshot.Combo, Is.EqualTo(1));
        }

        // A6
        [Test]
        public void Danger_RequiresContinuousOnePointTwoSeconds_ThenEntersRevivingPage()
        {
            EnterPlaying();

            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);

            _session.Tick(0.6f);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing), "不足 1.2s 不判负");

            _session.Tick(0.6f + 0.01f);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Reviving), "满 1.2s 进入失败页");
            Assert.That(_field.SimulationEnabled, Is.False, "失败页应冻结仿真");
            Assert.That(_session.CanRevive, Is.True);
        }

        [Test]
        public void Danger_AfterReviveUsed_SecondFailureGoesToGameOver()
        {
            EnterPlaying();
            ForceDangerAndExpire();
            Assert.That(_session.ApplyRevive(), Is.EqualTo(ReviveOutcome.Applied));

            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);
            _session.Tick(_balance.DangerHoldSeconds + 0.01f);

            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.GameOver), "每局只有 1 次复活（V2.10）");
            Assert.That(_session.CanRevive, Is.False);
        }

        // E3
        [Test]
        public void Danger_TimerResets_WhenViolationClears()
        {
            EnterPlaying();

            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);
            _session.Tick(1.0f);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing));

            _field.Danger = null;
            _session.Tick(0.1f);
            Assert.That(_dangerEnds, Is.EqualTo(1), "越线结束应发布一次");

            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);
            _session.Tick(1.0f);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing), "中断后重新计时，不应残留进度");
        }

        // A7
        [Test]
        public void DangerEvents_ArePairedOncePerViolationEpisode()
        {
            EnterPlaying();

            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);
            _session.Tick(0.3f);
            _session.Tick(0.3f);
            Assert.That(_dangerStarts.Count, Is.EqualTo(1), "同一段越线只发布一次 DangerStarted");

            _field.Danger = null;
            _session.Tick(0.1f);
            Assert.That(_dangerEnds, Is.EqualTo(1));

            _field.Danger = new DangerViolation(1, 3, 3.8f, 0.4f);
            _session.Tick(0.3f);
            Assert.That(_dangerStarts.Count, Is.EqualTo(2));

            _session.Tick(1.2f);
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Reviving));
            Assert.That(_dangerStarts.Count, Is.EqualTo(_dangerEnds), "判负也应补齐配对的 DangerEnded");
        }

        // A8
        [Test]
        public void ApplyRevive_OnFirstCall_RemovesClusterAndResumes_SecondCallIsRejected()
        {
            EnterPlaying();
            _field.LiveFruitCount = 10;
            ForceDangerAndExpire();

            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Reviving));
            Assert.That(_session.CanRevive, Is.True);

            var outcome = _session.ApplyRevive();

            Assert.That(outcome, Is.EqualTo(ReviveOutcome.Applied));
            Assert.That(_field.RemoveHighestClusterCalls, Is.EqualTo(1));
            Assert.That(_field.LiveFruitCount, Is.EqualTo(7), "默认清除上限 3 颗（V2.11）");
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing));
            Assert.That(_session.RevivesUsed, Is.EqualTo(1));
            Assert.That(_session.CanRevive, Is.False, "每局仅 1 次（V2.10）");

            Assert.That(_session.ApplyRevive(), Is.EqualTo(ReviveOutcome.NotInGameOver),
                "复活后已回到 Playing，重复调用应被拒绝");
        }

        [Test]
        public void ApplyRevive_WhenNotInReviving_ReturnsNotInGameOver_AndChangesNothing()
        {
            EnterPlaying();
            var score = _session.Score;

            Assert.That(_session.ApplyRevive(), Is.EqualTo(ReviveOutcome.NotInGameOver));
            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Playing));
            Assert.That(_session.RevivesUsed, Is.EqualTo(0));
            Assert.That(_session.Score, Is.EqualTo(score));
        }

        // E1
        [Test]
        public void Reviving_RejectsDropAndItemUse()
        {
            EnterPlaying();
            ForceDangerAndExpire();

            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.Reviving));
            Assert.That(_session.CanAct, Is.False);
            Assert.That(_session.CanUseItem(), Is.False);
            Assert.That(_session.ReleaseDrop(0f), Is.False);
        }

        // A9
        [Test]
        public void MilestoneReached_IsGrantedOncePerRound()
        {
            EnterPlaying();

            // 顶级 10 级（西瓜）单次 91 分，取 12 次使总分 ≥ 1000 且不依赖连击倍率是否叠加。
            for (var i = 0; i < 12; i++)
                _field.SimulateMerge(10);

            Assert.That(_session.Score, Is.GreaterThan(1000), "应已越过全部里程碑");

            Assert.That(_grantedItems.Count, Is.EqualTo(3), "200/500/1000 各发放一次");
            Assert.That(_grantedItems[0].Score, Is.EqualTo(200));
            Assert.That(_grantedItems[1].Score, Is.EqualTo(500));
            Assert.That(_grantedItems[2].Score, Is.EqualTo(1000));

            var rewards = new List<ItemKind>();
            foreach (var milestone in _grantedItems)
                rewards.Add(milestone.Reward);

            Assert.That(rewards, Is.Unique, "每个里程碑奖励只发放一次");
        }

        // A10 + E4
        [Test]
        public void ReleaseDrop_AfterGameOver_IsRejected_AndLateMergesDoNotScore()
        {
            EnterPlaying();
            _session.EndRound();

            Assert.That(_session.Phase, Is.EqualTo(RoundPhase.GameOver));

            var scoreAtEnd = _session.Score;
            Assert.That(_session.ReleaseDrop(0f), Is.False, "结算后拒绝投放");
            Assert.That(_session.CanAct, Is.False);

            _field.SimulateMerge(3);
            Assert.That(_session.Score, Is.EqualTo(scoreAtEnd), "结算后的残留合成事件不计分");
        }

        [Test]
        public void DropCooldown_BlocksImmediateSecondDrop_ThenAllowsAfterTick()
        {
            EnterPlaying();

            Assert.That(_session.ReleaseDrop(0f), Is.False, "冷却内第二次投放应被拒绝");
            Assert.That(_session.DropCount, Is.EqualTo(1));

            _session.Tick(_balance.DropCooldownSeconds + 0.01f);
            Assert.That(_session.ReleaseDrop(0f), Is.True);
            Assert.That(_session.DropCount, Is.EqualTo(2));
        }

        [Test]
        public void MergeDuringReviving_IsIgnored()
        {
            EnterPlaying();
            ForceDangerAndExpire();

            var score = _session.Score;
            _field.SimulateMerge(3);

            Assert.That(_session.Score, Is.EqualTo(score), "复活清场期间的残留合成不计分（E4）");
        }

        [Test]
        public void Dispose_UnsubscribesFromField()
        {
            EnterPlaying();
            _session.Dispose();

            _field.SimulateMerge(3);

            Assert.That(_session.Score, Is.EqualTo(0), "Dispose 后不再响应场地事件");
        }
    }
}
