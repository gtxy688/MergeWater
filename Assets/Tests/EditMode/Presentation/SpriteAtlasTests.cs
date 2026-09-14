using System.IO;
using System.Linq;
using MergeWater.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// Sprite Atlas 门禁（2026-09-14）：图集必须真的覆盖它该覆盖的图、不覆盖它不该覆盖的图。
    ///
    /// <para>为什么需要门禁：图集是「内容清单 + 打包设置」的资产，最容易出的两种错都是**静默的**——
    /// ① 以后往 `Assets/UI/Art/` 加了新图却忘了重跑生成菜单 → 那张图不合批（谁也看不出来）；
    /// ② 把满屏大图（`bg_star`/`Gamebg`/`bg_night`）塞进图集 → 图集被撑爆、并顶到 2048 尺寸上限
    /// （ES2 只保证 2048²，超了在老设备上是不可用贴图）。</para>
    ///
    /// <para>星球图集是**可选**的（有包体风险，见 <see cref="SpriteAtlasGenerator"/>），
    /// 因此只在它存在时校验其内容与尺寸。</para>
    /// </summary>
    public sealed class SpriteAtlasTests
    {
        private const string DefaultPlatform = "DefaultTexturePlatform";

        [Test]
        public void UiAtlas_Exists_AndPacksExactlyTheUiArtExceptBackgrounds()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(SpriteAtlasGenerator.UiAtlasPath);
            Assert.That(atlas, Is.Not.Null,
                $"缺少 UI 图集 {SpriteAtlasGenerator.UiAtlasPath}；" +
                "跑菜单 MergeWater/Generate UI Sprite Atlas 生成。");

            var packed = PackedSpritePaths(atlas);
            var expected = SpriteAtlasGenerator.FindUiArtSprites()
                .Select(AssetDatabase.GetAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(packed, Is.EqualTo(expected),
                "UI 图集内容与 Assets/UI/Art 的实际内容不一致：新增了图没进图集，或删掉的图还留在图集里。" +
                "重跑菜单 MergeWater/Generate UI Sprite Atlas。");

            foreach (var excluded in SpriteAtlasGenerator.UiArtExcludedNames)
                Assert.That(packed.Any(path => Path.GetFileNameWithoutExtension(path) == excluded), Is.False,
                    $"{excluded} 是满屏大图，必须排除在 UI 图集之外（进图集只会占地方，" +
                    $"并可能把图集顶过 {SpriteAtlasGenerator.MaxAtlasSize} 上限）");
        }

        [Test]
        public void UiAtlas_KeepsTextureSizeWithinTheWeChatSafeLimit()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(SpriteAtlasGenerator.UiAtlasPath);
            Assert.That(atlas, Is.Not.Null, "缺少 UI 图集，先跑生成菜单");

            var settings = atlas.GetPlatformSettings(DefaultPlatform);
            Assert.That(settings, Is.Not.Null, "图集应带 DefaultTexturePlatform 平台设置");
            Assert.That(settings.maxTextureSize, Is.LessThanOrEqualTo(SpriteAtlasGenerator.MaxAtlasSize),
                $"图集尺寸上限必须 ≤ {SpriteAtlasGenerator.MaxAtlasSize}：" +
                "微信小游戏跑在 WebGL 派生运行时上，ES2 只保证 2048²。");
        }

        [Test]
        public void UiArtFolder_EveryImageIsEitherPackedOrExplicitlyExcluded()
        {
            // 漂移守卫：以后往 Assets/UI/Art 加图，要么重跑菜单进图集，要么进排除名单并写明理由。
            var onDisk = AssetDatabase.FindAssets("t:Sprite", new[] { SpriteAtlasGenerator.UiArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Path.GetFileNameWithoutExtension)
                .Distinct()
                .ToArray();

            var known = SpriteAtlasGenerator.FindUiArtSprites()
                .Select(AssetDatabase.GetAssetPath)
                .Select(Path.GetFileNameWithoutExtension)
                .Concat(SpriteAtlasGenerator.UiArtExcludedNames)
                .Distinct()
                .ToArray();

            var unknown = onDisk.Except(known).ToArray();
            Assert.That(unknown, Is.Empty,
                "以下美术既没有进 UI 图集、也不在排除名单里：\n" + string.Join("\n", unknown) +
                "\n→ 重跑菜单 MergeWater/Generate UI Sprite Atlas；若是满屏大图，" +
                "把它加进 SpriteAtlasGenerator.UiArtExcludedNames 并写明理由。");

            var missing = SpriteAtlasGenerator.UiArtExcludedNames.Except(onDisk).ToArray();
            Assert.That(missing, Is.Empty,
                "排除名单里的文件在磁盘上不存在（改名后忘了同步？）：\n" + string.Join("\n", missing));
        }

        [Test]
        public void FruitAtlas_IfPresent_PacksThePlanetFolderAndStaysWithinTheLimit()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(SpriteAtlasGenerator.FruitAtlasPath);
            if (atlas == null)
            {
                Assert.Pass("星球图集尚未生成——按计划先只打 UI 图集，" +
                            "星球图集待包体实测（Resources 下贴图可能与图集重复计包）后再决定。");
                return;
            }

            var packables = SpriteAtlasExtensions.GetPackables(atlas) ?? new Object[0];
            var folders = packables.OfType<DefaultAsset>().Select(AssetDatabase.GetAssetPath).ToArray();
            Assert.That(folders, Does.Contain(SpriteAtlasGenerator.PlanetFolder),
                "星球图集应按**目录**打包 Planets：以后新增 planetNN 会自动进图集");

            var planets = AssetDatabase.FindAssets("t:Sprite", new[] { SpriteAtlasGenerator.PlanetFolder });
            Assert.That(planets.Length, Is.EqualTo(10), "星球图集应覆盖 planet00..09 共 10 张");

            var settings = atlas.GetPlatformSettings(DefaultPlatform);
            Assert.That(settings, Is.Not.Null, "图集应带 DefaultTexturePlatform 平台设置");
            Assert.That(settings.maxTextureSize, Is.LessThanOrEqualTo(SpriteAtlasGenerator.MaxAtlasSize),
                $"图集尺寸上限必须 ≤ {SpriteAtlasGenerator.MaxAtlasSize}（ES2 只保证 2048²）");
        }

        private static string[] PackedSpritePaths(SpriteAtlas atlas) =>
            (SpriteAtlasExtensions.GetPackables(atlas) ?? new Object[0])
                .OfType<Sprite>()
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
    }
}
