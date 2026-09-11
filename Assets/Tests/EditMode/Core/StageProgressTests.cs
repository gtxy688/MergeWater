using System.Collections.Generic;
using MergeWater.Core;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M1 验收 A7：阶段目标每局每节点只发放一次（V2.18）。</summary>
    public sealed class StageProgressTests
    {
        private readonly GameBalance _balance = GameBalance.CreateDefault();
        private readonly List<StageMilestone> _reached = new List<StageMilestone>();

        [Test]
        public void Advance_CrossingMultipleMilestones_ReturnsEachOnce()
        {
            var progress = new StageProgress(_balance);

            var count = progress.Advance(1200, _reached);
            Assert.That(count, Is.EqualTo(3), "一次跨越 200/500/1000 应返回全部三个");
            Assert.That(_reached.Count, Is.EqualTo(3));
            Assert.That(_reached[0].Score, Is.EqualTo(200));
            Assert.That(_reached[1].Score, Is.EqualTo(500));
            Assert.That(_reached[2].Score, Is.EqualTo(1000));

            count = progress.Advance(2000, _reached);
            Assert.That(count, Is.EqualTo(0), "同一局内不应重复发放");
            Assert.That(_reached, Is.Empty);
        }

        [Test]
        public void Advance_BelowFirstMilestone_ReturnsNothing()
        {
            var progress = new StageProgress(_balance);

            Assert.That(progress.Advance(199, _reached), Is.EqualTo(0));
            Assert.That(_reached, Is.Empty);

            Assert.That(progress.Advance(200, _reached), Is.EqualTo(1));
            Assert.That(_reached[0].Reward, Is.EqualTo(ItemKind.Undo));
        }

        [Test]
        public void Reset_AllowsMilestonesToBeGrantedAgainInNextRound()
        {
            var progress = new StageProgress(_balance);
            progress.Advance(1000, _reached);

            progress.Reset();

            Assert.That(progress.Advance(1000, _reached), Is.EqualTo(3), "重开一局后里程碑应可再次发放");
        }

        [Test]
        public void TryGetNext_TracksRemainingMilestones()
        {
            var progress = new StageProgress(_balance);

            Assert.That(progress.TryGetNext(out var first), Is.True);
            Assert.That(first.Score, Is.EqualTo(200));

            progress.Advance(500, _reached);

            Assert.That(progress.TryGetNext(out var next), Is.True);
            Assert.That(next.Score, Is.EqualTo(1000));

            progress.Advance(5000, _reached);
            Assert.That(progress.TryGetNext(out _), Is.False, "全部达成后无下一个节点");
            Assert.That(progress.ReachedCount, Is.EqualTo(3));
        }

        [Test]
        public void GetNormalizedProgress_IsZeroAtStart_OneWhenAllReached()
        {
            var progress = new StageProgress(_balance);

            Assert.That(progress.GetNormalizedProgress(0), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(progress.GetNormalizedProgress(100), Is.EqualTo(0.5f).Within(1e-4f), "0→200 的半程");
            Assert.That(progress.GetNormalizedProgress(200), Is.EqualTo(0f).Within(1e-4f), "刚到 200 时进入下一段起点");
            Assert.That(progress.GetNormalizedProgress(350), Is.EqualTo(0.5f).Within(1e-4f), "200→500 的半程");
            Assert.That(progress.GetNormalizedProgress(1000), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(progress.GetNormalizedProgress(99999), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Constructor_WithNullBalance_YieldsEmptyMilestones()
        {
            var progress = new StageProgress(null);

            Assert.That(progress.MilestoneCount, Is.EqualTo(0));
            Assert.That(progress.Advance(99999, _reached), Is.EqualTo(0));
            Assert.That(progress.TryGetNext(out _), Is.False);
        }
    }
}
