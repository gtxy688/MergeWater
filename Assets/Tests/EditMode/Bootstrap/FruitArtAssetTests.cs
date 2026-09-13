using System.Linq;
using MergeWater.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 水果正式美术的资产门禁（2026-09-13：换成 `kenney_planets`）。
    ///
    /// <para>守两件事：</para>
    /// <para>① **等级 → 贴图** 一一对应且导入设置正确。贴图世界尺寸 = 像素 / PPU，
    /// 而水果视觉是按半径缩放这张贴图的，所以 PPU 与像素尺寸必须自洽，否则视觉直径会漂移。</para>
    /// <para>② `Assets/Resources/` 下不得混入**用不到**的素材——`Resources` 里的资源会被
    /// **无条件**打进包，而微信小游戏首包只有 4MB 预算。本项目已经因此付过一次代价
    /// （`Resources/Art` 4.9MB，2026-09-13 移出）；本次 `kenney_planets/Parts/`
    /// （42 张 4.24MB）同理，已移到 `Assets/Art/kenney_planets/`。</para>
    /// </summary>
    public sealed class FruitArtAssetTests
    {
        private const string PlanetsFolder = "Assets/Resources/kenney_planets/Planets";
        private const string PlanetsRoot = "Assets/Resources/kenney_planets";

        [Test]
        public void PlanetSprite_ExistsForEveryTier_WithMatchingImportSettings()
        {
            for (var level = GameBalance.MinTier; level <= GameBalance.MaxTier; level++)
            {
                var path = $"{PlanetsFolder}/planet{level - 1:00}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                Assert.That(sprite, Is.Not.Null, $"等级 {level} 缺少贴图：{path}（须导入为 Sprite）");
                Assert.That(sprite.texture, Is.Not.Null, $"{path} 的贴图缺失");

                Assert.That(sprite.texture.width, Is.EqualTo(sprite.texture.height),
                    $"{path} 应为正方形：视觉是等比缩放的，非正方形会与圆形碰撞体对不上");

                Assert.That(sprite.pixelsPerUnit, Is.EqualTo((float)sprite.texture.width).Within(0.01f),
                    $"{path} 的 PPU 应等于贴图边长——这样贴图世界尺寸恰好 1×1，" +
                    "再乘以「半径×2」才等于碰撞直径");
            }
        }

        [Test]
        public void ResourcesFolder_HoldsOnlyArtThatIsLoadedAtRuntime()
        {
            var extra = AssetDatabase.FindAssets(string.Empty, new[] { PlanetsRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => !path.StartsWith(PlanetsFolder + "/"))
                .ToArray();

            Assert.That(extra, Is.Empty,
                $"{PlanetsRoot} 下只应保留运行时通过 Resources.Load 取用的 Planets/；" +
                "其余内容会被无条件打进包（本次实测 Parts/ 42 张 4.24MB）。多余文件：\n" +
                string.Join("\n", extra));
        }
    }
}
