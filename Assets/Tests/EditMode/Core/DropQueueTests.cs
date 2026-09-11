using MergeWater.Core;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M1 验收 A5：投放等级池与权重（V2.4–V2.6）。</summary>
    public sealed class DropQueueTests
    {
        private static readonly int[] Seeds = { 1, 7, 12345, 99991 };

        [Test]
        public void Next_FirstTwentyDrops_OnlyTierOneOrTwo()
        {
            var balance = GameBalance.CreateDefault();

            foreach (var seed in Seeds)
            {
                var queue = new DropQueue(balance, seed);
                for (var dropIndex = 0; dropIndex < balance.EarlyPhaseDropCount; dropIndex++)
                {
                    var level = queue.LevelForDropIndex(dropIndex);
                    Assert.That(level, Is.EqualTo(1).Or.EqualTo(2), $"seed {seed} 第 {dropIndex} 投应只有 1–2 级");
                }
            }
        }

        [Test]
        public void Next_AfterTwentyDrops_CanProduceTierThree()
        {
            var balance = GameBalance.CreateDefault();
            var queue = new DropQueue(balance, 20260911);

            var sawTierThree = false;
            for (var i = 0; i < 500 && !sawTierThree; i++)
                sawTierThree = queue.LevelForDropIndex(balance.EarlyPhaseDropCount + i) == 3;

            Assert.That(sawTierThree, Is.True, "20 投后应可能出现 3 级");
        }

        [Test]
        public void Next_AtSixtyDropsAndBeyond_KeepsSamePool()
        {
            var balance = GameBalance.CreateDefault();
            var queue = new DropQueue(balance, 4242);

            Assert.That(queue.MaxLevelForDropIndex(balance.MidPhaseDropCount), Is.EqualTo(3), "V2.6：60 投后等级池不变");
            Assert.That(queue.MaxLevelForDropIndex(balance.MidPhaseDropCount + 1000), Is.EqualTo(3));

            for (var i = 0; i < 2000; i++)
            {
                var level = queue.LevelForDropIndex(balance.MidPhaseDropCount + i);
                Assert.That(level, Is.InRange(1, 3), "60 投后仍只出 1–3 级");
            }
        }

        [Test]
        public void Next_WeightDistribution_MatchesV1WithinTolerance()
        {
            const int samples = 40000;
            var balance = GameBalance.CreateDefault();
            var queue = new DropQueue(balance, 777);
            var counts = new int[4];

            for (var i = 0; i < samples; i++)
                counts[queue.LevelForDropIndex(balance.MidPhaseDropCount)]++;

            var ratio1 = counts[1] / (float)samples;
            var ratio2 = counts[2] / (float)samples;
            var ratio3 = counts[3] / (float)samples;

            Assert.That(ratio1, Is.EqualTo(0.5f).Within(0.02f), "等级 1 权重 0.5");
            Assert.That(ratio2, Is.EqualTo(0.3f).Within(0.02f), "等级 2 权重 0.3");
            Assert.That(ratio3, Is.EqualTo(0.2f).Within(0.02f), "等级 3 权重 0.2");
            Assert.That(ratio1 + ratio2 + ratio3, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void SameSeed_ProducesReproducibleSequence()
        {
            var balance = GameBalance.CreateDefault();
            var a = new DropQueue(balance, 31337);
            var b = new DropQueue(balance, 31337);

            for (var i = 0; i < 200; i++)
                Assert.That(b.LevelForDropIndex(i), Is.EqualTo(a.LevelForDropIndex(i)), "同 seed 序列应一致");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var balance = GameBalance.CreateDefault();
            var a = new DropQueue(balance, 1);
            var b = new DropQueue(balance, 2);

            var differences = 0;
            for (var i = 0; i < 200; i++)
            {
                if (a.LevelForDropIndex(i) != b.LevelForDropIndex(i))
                    differences++;
            }

            Assert.That(differences, Is.GreaterThan(0), "不同 seed 不应完全一致");
        }
    }
}
