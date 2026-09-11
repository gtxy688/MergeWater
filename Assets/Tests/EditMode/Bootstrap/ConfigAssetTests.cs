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

            Assert.That(disk.ComboWindowSeconds, Is.EqualTo(code.ComboWindowSeconds).Within(1e-5f));
            Assert.That(disk.ComboMultiplierCap, Is.EqualTo(code.ComboMultiplierCap).Within(1e-5f));
            Assert.That(disk.DangerHoldSeconds, Is.EqualTo(code.DangerHoldSeconds).Within(1e-5f));
            Assert.That(disk.ReviveClusterMax, Is.EqualTo(code.ReviveClusterMax));
            Assert.That(disk.ReviveCooldownSeconds, Is.EqualTo(code.ReviveCooldownSeconds).Within(1e-5f));
            Assert.That(disk.BombRadius, Is.EqualTo(code.BombRadius).Within(1e-5f));
            Assert.That(disk.StageMilestones.Length, Is.EqualTo(code.StageMilestones.Length));

            for (var i = 0; i < code.StageMilestones.Length; i++)
            {
                Assert.That(disk.StageMilestones[i].Score, Is.EqualTo(code.StageMilestones[i].Score));
                Assert.That(disk.StageMilestones[i].Reward, Is.EqualTo(code.StageMilestones[i].Reward));
            }
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
