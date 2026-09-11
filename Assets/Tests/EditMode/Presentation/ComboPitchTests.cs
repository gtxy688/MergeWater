using MergeWater.Core;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M5 验收 A5：连击音阶与顿帧派生（V2.23/V2.24）。</summary>
    public sealed class ComboPitchTests
    {
        [Test]
        public void ComboPitch_AddsSemitonePerCombo_CappedAtOneOctave()
        {
            const int semitoneCap = 12; // V2.24「封顶 8 度」= 12 半音

            Assert.That(FeelRules.ComboPitch(0, semitoneCap), Is.EqualTo(1f).Within(1e-5f), "无连击为原调");
            Assert.That(FeelRules.ComboPitch(1, semitoneCap), Is.EqualTo(1f).Within(1e-5f));

            var expectedTwo = 1.0594631f; // 2^(1/12)
            Assert.That(FeelRules.ComboPitch(2, semitoneCap), Is.EqualTo(expectedTwo).Within(1e-4f), "2 连击 +1 半音");

            Assert.That(FeelRules.ComboPitch(13, semitoneCap), Is.EqualTo(2f).Within(1e-4f), "13 连击达到上限 8 度（×2）");
            Assert.That(FeelRules.ComboPitch(50, semitoneCap), Is.EqualTo(2f).Within(1e-4f), "封顶 8 度");
        }

        [Test]
        public void HitStopSeconds_GrowsWithCombo_AndStaysWithinV223Range()
        {
            var balance = GameBalance.CreateDefault();

            var low = FeelRules.HitStopSeconds(1, balance);
            var mid = FeelRules.HitStopSeconds(3, balance);
            var high = FeelRules.HitStopSeconds(balance.HitStopMaxCombo, balance);

            Assert.That(low, Is.EqualTo(0.08f).Within(1e-4f), "1 连击取下限 80ms");
            Assert.That(high, Is.EqualTo(0.12f).Within(1e-4f), "达到阈值连击取上限 120ms");
            Assert.That(mid, Is.GreaterThan(low).And.LessThan(high));

            for (var combo = 1; combo <= 40; combo++)
            {
                var value = FeelRules.HitStopSeconds(combo, balance);
                Assert.That(value, Is.InRange(0.08f, 0.12f), $"{combo} 连击顿帧应在 80–120ms");
            }
        }

        [Test]
        public void ShakeTier_IsThreeLevels()
        {
            Assert.That(FeelRules.ShakeTier(0), Is.EqualTo(0));
            Assert.That(FeelRules.ShakeTier(1), Is.EqualTo(0));
            Assert.That(FeelRules.ShakeTier(2), Is.EqualTo(1));
            Assert.That(FeelRules.ShakeTier(3), Is.EqualTo(1));
            Assert.That(FeelRules.ShakeTier(4), Is.EqualTo(2));
            Assert.That(FeelRules.ShakeTier(20), Is.EqualTo(2));
        }
    }
}
