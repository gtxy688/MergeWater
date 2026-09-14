using System;
using MergeWater.Core;
using MergeWater.Meta;
using NUnit.Framework;
using UnityEditor;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// M7 验收 A2：磁盘配置资产存在且与代码默认值（即需求 V1/V2 表）一致。
    /// 前置：先运行 `MergeWater/Generate Config Assets`。
    /// </summary>
    public sealed class ConfigAssetTests
    {
        [Test]
        public void GameBalanceAsset_Exists_AndMatchesCodeDefault()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                MergeWater.Editor.ConfigAssetGenerator.BalancePath);

            Assert.That(asset, Is.Not.Null,
                "缺少 GameBalance.asset，请先运行 MergeWater/Generate Config Assets");

            var disk = asset.RawBalance;
            var code = GameBalance.CreateDefault();

            Assert.That(disk, Is.Not.Null);
            Assert.That(disk.TierCount, Is.EqualTo(code.TierCount), "等级数与代码默认值一致");

            for (var level = GameBalance.MinTier; level <= GameBalance.MaxTier; level++)
            {
                var a = disk.GetTier(level);
                var b = code.GetTier(level);

                Assert.That(a.DisplayName, Is.EqualTo(b.DisplayName), $"等级 {level} 名称");
                Assert.That(a.Radius, Is.EqualTo(b.Radius).Within(1e-5f), $"等级 {level} 半径");
                Assert.That(a.Mass, Is.EqualTo(b.Mass).Within(1e-5f), $"等级 {level} 质量");
                Assert.That(a.Score, Is.EqualTo(b.Score), $"等级 {level} 得分");
                Assert.That(a.DropWeight, Is.EqualTo(b.DropWeight).Within(1e-5f), $"等级 {level} 权重");
            }

            Assert.That(disk.StageMilestones.Length, Is.EqualTo(code.StageMilestones.Length));

            // 全部标量逐项比对（含此前漏掉的 V2.36–V2.42 场地/手感项）。
            // 补这一段的直接原因（2026-09-13）：`FloorTuningWindow` 的「写回代码默认值」在写盘 .cs 后
            // 立刻用**尚未重编译**的旧程序集生成资产，把 `floorScreenInset` 原样写回旧值
            //（实测 .cs=0.06、资产=0.55；时间戳 .cs 10:01:34.931 → 资产 10:01:35.208 → Core.dll 10:01:38.268）。
            // 当时断言清单里没有 `FloorScreenInset`，于是测试全绿、需求方的调参却静默失效——
            // 说明「漏项」和「数值漂移」一样危险，因此这里改成整表覆盖而不是逐个补。
            AssertScalars(disk, code);

            for (var i = 0; i < code.StageMilestones.Length; i++)
            {
                Assert.That(disk.StageMilestones[i].Score, Is.EqualTo(code.StageMilestones[i].Score));
                Assert.That(disk.StageMilestones[i].Reward, Is.EqualTo(code.StageMilestones[i].Reward));
            }
        }

        /// <summary>
        /// 运行时的数值全部来自磁盘资产，因此资产里**任何一个**标量与代码默认值不一致，
        /// 都意味着「改了 <c>GameBalance</c> 却没同步资产」，玩家侧表现为改数值不生效。
        /// 用一张整表覆盖，避免再出现「新加的字段忘了加断言」。
        /// </summary>
        private static void AssertScalars(GameBalance disk, GameBalance code)
        {
            var scalars = new (string Label, Func<GameBalance, float> Read)[]
            {
                ("V2.4 早期阶段投放数", b => b.EarlyPhaseDropCount),
                ("V2.5 中期阶段投放数", b => b.MidPhaseDropCount),
                ("V2.1 连击窗口", b => b.ComboWindowSeconds),
                ("V2.2 连击倍率步长", b => b.ComboMultiplierStep),
                ("V2.3 连击倍率上限", b => b.ComboMultiplierCap),
                ("V2.8 越线保持时长", b => b.DangerHoldSeconds),
                ("V2.9 静止线速度阈值", b => b.DangerSettleLinearSpeed),
                ("V2.9 静止角速度阈值", b => b.DangerSettleAngularSpeed),
                ("V2.9 静止判定缓冲", b => b.DangerSettleGraceSeconds),
                ("V2.10 每局复活次数", b => b.RevivePerRound),
                ("V2.10 复活簇上限", b => b.ReviveClusterMax),
                ("V2.11 复活广告冷却", b => b.ReviveCooldownSeconds),
                ("V2.12 分享冷却", b => b.ShareCooldownSeconds),
                ("V2.16 插屏间隔局数", b => b.InterstitialEveryNGames),
                ("V2.13 道具每日上限", b => b.ItemDailyCap),
                ("V2.14 摇一摇每日上限", b => b.ShakeDailyCap),
                ("V2.17 炸弹半径", b => b.BombRadius),
                ("V2.17 锤子最大半径", b => b.HammerMaxRadius),
                ("V2.20 投放震屏时长", b => b.DropShakeSeconds),
                ("V2.21 慢动作倍率", b => b.SlowMoScale),
                ("V2.21 慢动作时长", b => b.SlowMoSeconds),
                ("V2.22 顿帧下限", b => b.HitStopMinSeconds),
                ("V2.22 顿帧上限", b => b.HitStopMaxSeconds),
                ("V2.23 顿帧连击上限", b => b.HitStopMaxCombo),
                ("V2.24 连击音阶封顶", b => b.ComboSemitoneCap),
                ("V2.25 预览最长时长", b => b.AimPreviewMaxSeconds),
                ("V2.26 吸收动画时长", b => b.AbsorbDurationSeconds),
                ("V3 场地半宽", b => b.FieldHalfWidth),
                ("V3 墙体厚度", b => b.WallThickness),
                ("V3 固定地面高度", b => b.FieldFloorY),
                ("V2.42 地面相对屏幕底边的抬升量", b => b.FloorScreenInset),
                ("V2.26a 生成高度", b => b.DropSpawnY),
                ("V2.8 警戒线", b => b.DangerLineY),
                ("V3 出局线", b => b.KillY),
                ("V3 最大存活数", b => b.MaxLiveFruits),
                ("V3 最大线速度", b => b.MaxLinearVelocity),
                ("V2.4 投放冷却", b => b.DropCooldownSeconds),
                ("V3 生成抖动", b => b.SpawnJitter),
                ("V2.31b 向上初速", b => b.MergeResultUpwardImpulse),
                ("V2.31b 水平初速", b => b.MergeResultSideImpulse),
                ("V2.32 摇一摇冲量", b => b.ShakeImpulse),
                ("V2.29 摩擦", b => b.FruitFriction),
                ("V2.30 弹性", b => b.FruitBounciness),
                ("V2.27 线性阻尼", b => b.FruitLinearDrag),
                ("V2.27 角阻尼", b => b.FruitAngularDrag),
                ("V2.28 重力倍率下限", b => b.GetGravityScale(GameBalance.MinTier)),
                ("V2.28 重力倍率上限", b => b.GetGravityScale(GameBalance.MaxTier)),
                ("V2.33 下一颗延迟", b => b.NextFruitRevealDelaySeconds),
                ("V2.34 渐显时长", b => b.NextFruitRevealDurationSeconds),
                ("V2.35 加载页最短时长", b => b.LoadingMinSeconds)
            };

            foreach (var (label, read) in scalars)
                Assert.That(read(disk), Is.EqualTo(read(code)).Within(1e-5f),
                    $"{label}：资产与代码默认值不一致，请运行 MergeWater/Generate Config Assets 重新生成");
        }

        [Test]
        public void MetaSettingsAsset_Exists_WithExpectedDefaults()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MetaSettings>(
                MergeWater.Editor.ConfigAssetGenerator.MetaSettingsPath);

            Assert.That(settings, Is.Not.Null,
                "缺少 MetaSettings.asset，请先运行 MergeWater/Generate Config Assets");

            Assert.That(settings.InterstitialEnabled, Is.True, "V2.16：插屏默认可远程开关为开");
            Assert.That(settings.OfflineGrantAll, Is.True, "D6：离线演示时允许直接领取");
        }

        [Test]
        public void PlaceholderArt_ExistsAndImportsAsSprite()
        {
            foreach (var path in new[]
                     {
                         MergeWater.Editor.PlaceholderArtGenerator.FruitCirclePath,
                         MergeWater.Editor.PlaceholderArtGenerator.ArrowPath
                     })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
                Assert.That(sprite, Is.Not.Null, $"占位图应导入为 Sprite：{path}");
                Assert.That(sprite.texture, Is.Not.Null);
            }
        }
    }
}
