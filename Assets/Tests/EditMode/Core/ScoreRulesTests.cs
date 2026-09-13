using MergeWater.Core;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M1 验收 A6：得分与可合成判定（V2.3/V2.7）。</summary>
    public sealed class ScoreRulesTests
    {
        private readonly GameBalance _balance = GameBalance.CreateDefault();

        [Test]
        public void ScoreFor_AppliesMultiplierAndRounds()
        {
            Assert.That(ScoreRules.ScoreFor(1, 1.0f, _balance), Is.EqualTo(10));
            Assert.That(ScoreRules.ScoreFor(2, 1.0f, _balance), Is.EqualTo(15));
            Assert.That(ScoreRules.ScoreFor(2, 1.1f, _balance), Is.EqualTo(17), "15 × 1.1 = 16.5，四舍五入为 17");
            Assert.That(ScoreRules.ScoreFor(10, 1.0f, _balance), Is.EqualTo(91), "西瓜顶点分");
            Assert.That(ScoreRules.ScoreFor(10, 2.0f, _balance), Is.EqualTo(182), "顶点分 × 倍率上限");
        }

        [Test]
        public void ScoreFor_UnknownLevel_ReturnsZero()
        {
            Assert.That(ScoreRules.ScoreFor(0, 1.0f, _balance), Is.EqualTo(0));
            Assert.That(ScoreRules.ScoreFor(11, 1.0f, _balance), Is.EqualTo(0), "顶点之上无等级");
            Assert.That(ScoreRules.ScoreFor(12, 1.0f, _balance), Is.EqualTo(0));
            Assert.That(ScoreRules.ScoreFor(1, 1.0f, null), Is.EqualTo(0));
        }

        [Test]
        public void ScoreFor_NonPositiveMultiplier_FallsBackToOne()
        {
            Assert.That(ScoreRules.ScoreFor(1, 0f, _balance), Is.EqualTo(10));
            Assert.That(ScoreRules.ScoreFor(1, -3f, _balance), Is.EqualTo(10));
        }

        [Test]
        public void CanMerge_TopTier_IsFalse()
        {
            Assert.That(ScoreRules.CanMerge(10, _balance), Is.False, "10 级（西瓜）为顶点，不再合成");
            Assert.That(ScoreRules.CanMerge(9, _balance), Is.True, "9 级可合成为 10 级");
            Assert.That(ScoreRules.CanMerge(1, _balance), Is.True);
        }

        [Test]
        public void CanMerge_OutOfRangeLevels_AreFalse()
        {
            Assert.That(ScoreRules.CanMerge(0, _balance), Is.False);
            Assert.That(ScoreRules.CanMerge(-1, _balance), Is.False);
            Assert.That(ScoreRules.CanMerge(11, _balance), Is.False);
            Assert.That(ScoreRules.CanMerge(12, _balance), Is.False);
            Assert.That(ScoreRules.CanMerge(1, null), Is.False);
        }

        [Test]
        public void MaxTier_IsTen()
        {
            Assert.That(ScoreRules.MaxTier(_balance), Is.EqualTo(10));
        }
    }
}
