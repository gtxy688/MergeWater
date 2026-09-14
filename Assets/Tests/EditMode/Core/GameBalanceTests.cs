using System.Collections.Generic;
using MergeWater.Core;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M1 验收 A1/A2：V1 等级表与权重（Docs/architecture/01-core-test.md）。</summary>
    public sealed class GameBalanceTests
    {
        // 2026-09-13 需求方「球形弄得太小了」：半径整体 x1.5（质量/得分/权重不变）
        private static readonly float[] ExpectedRadius =
        {
            0.27f, 0.36f, 0.45f, 0.57f, 0.69f, 0.825f, 0.975f, 1.14f, 1.32f, 1.50f
        };

        private static readonly float[] ExpectedMass =
        {
            0.10f, 0.18f, 0.28f, 0.45f, 0.66f, 0.95f, 1.32f, 1.80f, 2.41f, 3.14f
        };

        private static readonly int[] ExpectedScore = { 10, 15, 21, 28, 36, 45, 55, 66, 78, 91 };

        private static readonly float[] ExpectedWeight = { 0.5f, 0.3f, 0.2f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

        private static readonly string[] ExpectedName =
        {
            "葡萄", "樱桃", "橘子", "柠檬", "猕猴桃", "番茄", "桃子", "菠萝", "椰子", "西瓜"
        };

        [Test]
        public void Default_MatchesRequirementsV1Table()
        {
            var balance = GameBalance.CreateDefault();

            Assert.That(balance.TierCount, Is.EqualTo(10), "V1 表应为 10 级（2026-09-13 需求方：减一级适配美术资源）");

            for (var level = GameBalance.MinTier; level <= GameBalance.MaxTier; level++)
            {
                var tier = balance.GetTier(level);
                var index = level - 1;

                Assert.That(tier.Level, Is.EqualTo(level));
                Assert.That(tier.DisplayName, Is.EqualTo(ExpectedName[index]), $"等级 {level} 名称");
                Assert.That(tier.Radius, Is.EqualTo(ExpectedRadius[index]).Within(1e-5f), $"等级 {level} 半径");
                Assert.That(tier.Mass, Is.EqualTo(ExpectedMass[index]).Within(1e-5f), $"等级 {level} 质量");
                Assert.That(tier.Score, Is.EqualTo(ExpectedScore[index]), $"等级 {level} 得分");
                Assert.That(tier.DropWeight, Is.EqualTo(ExpectedWeight[index]).Within(1e-5f), $"等级 {level} 权重");
            }
        }

        [Test]
        public void Default_Tiers_AreMonotonicAndWeightsSumToOne()
        {
            var balance = GameBalance.CreateDefault();

            for (var level = GameBalance.MinTier; level < GameBalance.MaxTier; level++)
            {
                var lower = balance.GetTier(level);
                var upper = balance.GetTier(level + 1);

                Assert.That(upper.Radius, Is.GreaterThan(lower.Radius), $"半径应随等级严格递增（{level}→{level + 1}）");
                Assert.That(upper.Mass, Is.GreaterThan(lower.Mass), $"质量应随等级严格递增（{level}→{level + 1}）");
                Assert.That(upper.Score, Is.GreaterThan(lower.Score), $"得分应随等级严格递增（{level}→{level + 1}）");
            }

            var weightSum = 0f;
            for (var level = GameBalance.MinTier; level <= GameBalance.MaxTier; level++)
                weightSum += balance.GetTier(level).DropWeight;

            Assert.That(weightSum, Is.EqualTo(1f).Within(1e-5f), "1–3 级投放权重之和应为 1");

            for (var level = 4; level <= GameBalance.MaxTier; level++)
                Assert.That(balance.GetTier(level).DropWeight, Is.EqualTo(0f), $"等级 {level} 不应参与投放");
        }

        [Test]
        public void GetTier_OutOfRange_ReturnsInvalidDefaultWithoutThrowing()
        {
            var balance = GameBalance.CreateDefault();

            foreach (var level in new[] { -1, 0, 11, 12, 999 })
            {
                var tier = balance.GetTier(level);
                Assert.That(tier.IsValid, Is.False, $"等级 {level} 应返回无效默认值");
                Assert.That(balance.HasTier(level), Is.False);
            }
        }

        [Test]
        public void Default_StageMilestones_MatchV218()
        {
            var balance = GameBalance.CreateDefault();
            var milestones = new List<StageMilestone>(balance.StageMilestones);

            Assert.That(milestones.Count, Is.EqualTo(3));
            Assert.That(milestones[0].Score, Is.EqualTo(200));
            Assert.That(milestones[0].Reward, Is.EqualTo(ItemKind.Undo));
            Assert.That(milestones[1].Score, Is.EqualTo(500));
            Assert.That(milestones[1].Reward, Is.EqualTo(ItemKind.Bomb));
            Assert.That(milestones[2].Score, Is.EqualTo(1000));
            Assert.That(milestones[2].Reward, Is.EqualTo(ItemKind.Hammer));
        }

        [Test]
        public void Clone_IsDeepCopy_AndDoesNotShareArrays()
        {
            var original = GameBalance.CreateDefault();
            var clone = original.Clone();

            Assert.That(clone.Tiers, Is.Not.SameAs(original.Tiers));
            Assert.That(clone.StageMilestones, Is.Not.SameAs(original.StageMilestones));

            clone.Tiers[0].Score = -1;
            Assert.That(original.Tiers[0].Score, Is.EqualTo(10), "克隆体修改不应影响原对象");
        }

        [Test]
        public void Default_FeelAndRuleValues_MatchV2()
        {
            var balance = GameBalance.CreateDefault();

            Assert.That(balance.ComboWindowSeconds, Is.EqualTo(3.0f).Within(1e-5f), "V2.1");
            Assert.That(balance.ComboMultiplierStep, Is.EqualTo(0.1f).Within(1e-5f), "V2.2");
            Assert.That(balance.ComboMultiplierCap, Is.EqualTo(2.0f).Within(1e-5f), "V2.3");
            Assert.That(balance.DangerHoldSeconds, Is.EqualTo(1.2f).Within(1e-5f), "V2.8");
            Assert.That(balance.RevivePerRound, Is.EqualTo(1), "V2.10");
            Assert.That(balance.ReviveClusterMax, Is.EqualTo(3), "V2.11");
            Assert.That(balance.ReviveCooldownSeconds, Is.EqualTo(90f).Within(1e-5f), "V2.12");
            Assert.That(balance.ItemDailyCap, Is.EqualTo(3), "V2.13");
            Assert.That(balance.ShakeDailyCap, Is.EqualTo(2), "V2.14");
            Assert.That(balance.InterstitialEveryNGames, Is.EqualTo(3), "V2.16");
            Assert.That(balance.BombRadius, Is.EqualTo(0.9f).Within(1e-5f), "V2.17（2026-09-13 随半径 x1.5 同步）");
            Assert.That(balance.DropShakeSeconds, Is.EqualTo(0.05f).Within(1e-5f), "V2.20");
            Assert.That(balance.SlowMoScale, Is.EqualTo(0.2f).Within(1e-5f), "V2.22");
            Assert.That(balance.SlowMoSeconds, Is.EqualTo(0.15f).Within(1e-5f), "V2.22");
            Assert.That(balance.HitStopMinSeconds, Is.EqualTo(0.08f).Within(1e-5f), "V2.23");
            Assert.That(balance.HitStopMaxSeconds, Is.EqualTo(0.12f).Within(1e-5f), "V2.23");
            Assert.That(balance.ComboSemitoneCap, Is.EqualTo(12), "V2.24「封顶 8 度」= 12 半音");
            Assert.That(balance.AimPreviewMaxSeconds, Is.EqualTo(0.8f).Within(1e-5f), "V2.26");
            Assert.That(balance.ShareCooldownSeconds, Is.EqualTo(60f).Within(1e-5f), "V2.19");
            Assert.That(balance.AbsorbDurationSeconds, Is.EqualTo(0.06f).Within(1e-5f), "V2.21");
            Assert.That(balance.MergeResultUpwardImpulse, Is.EqualTo(0f).Within(1e-5f), "V2.31b：合成结果不向上蹦");
            // 2026-09-13 半径 x1.5 后同比放大（同比例的速度尺度）
            Assert.That(balance.MergeResultSideImpulse, Is.EqualTo(2.25f).Within(1e-5f),
                "V2.31b：合成结果水平初速（0.45→1.5→2.25，随半径 x1.5 同步）");
        }
    }
}
