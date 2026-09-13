using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 「等级 → 水果外观」规则的单元守卫（2026-09-13：水果图换成 `kenney_planets`）。
    ///
    /// <para>这条规则同时被 M2（生成水果）与 M5（待投预览）使用，所以必须只有一份实现——
    /// 否则会出现「投下来的水果是星球、瞄准时预览的却还是圆片」这类只在真机/真场景里才发现的不一致。</para>
    /// </summary>
    public sealed class FruitArtTests
    {
        private static Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            sprite.name = name;
            return sprite;
        }

        [Test]
        public void ForLevel_UsesSpriteIndexedByLevel()
        {
            var first = MakeSprite("first");
            var second = MakeSprite("second");
            var fallback = MakeSprite("fallback");
            var art = new FruitArt(new[] { first, second }, fallback);

            Assert.That(art.ForLevel(1), Is.SameAs(first), "1 级取数组第 0 张");
            Assert.That(art.ForLevel(2), Is.SameAs(second), "2 级取数组第 1 张");
            Assert.That(art.HasArt(1), Is.True);
            Assert.That(art.Count, Is.EqualTo(2));
        }

        [Test]
        public void WithArt_DoesNotTint_SoEachFruitKeepsItsOwnColors()
        {
            var art = new FruitArt(new[] { MakeSprite("a") }, MakeSprite("fallback"));

            Assert.That(art.TintForLevel(1), Is.EqualTo(Color.white),
                "专属美术必须按原色显示：染成调色板颜色会毁掉每颗水果各自的可辨识度");
        }

        [Test]
        public void WithoutArt_FallsBackToPlaceholder_AndKeepsPaletteTint()
        {
            var fallback = MakeSprite("fallback");
            var art = new FruitArt(new Sprite[] { null }, fallback);

            Assert.That(art.ForLevel(1), Is.SameAs(fallback), "该级缺图时回退占位圆片");
            Assert.That(art.HasArt(1), Is.False);
            Assert.That(art.TintForLevel(1), Is.EqualTo(FruitPalette.ForLevel(1)),
                "回退到占位圆片时必须保留调色板染色，否则 D4「颜色 + 半径双重区分」失效");
        }

        [Test]
        public void OutOfRangeLevel_FallsBackWithoutThrowing()
        {
            var fallback = MakeSprite("fallback");
            var art = new FruitArt(new[] { MakeSprite("a") }, fallback);

            foreach (var level in new[] { 0, -1, 2, 99 })
            {
                Assert.That(art.HasArt(level), Is.False, $"等级 {level} 不应有专属美术");
                Assert.That(art.ForLevel(level), Is.SameAs(fallback), $"等级 {level} 应回退");
            }
        }

        [Test]
        public void EmptyArtSet_FallsBackForEveryTier()
        {
            var fallback = MakeSprite("fallback");
            var art = new FruitArt(null, fallback);

            for (var level = GameBalance.MinTier; level <= GameBalance.MaxTier; level++)
            {
                Assert.That(art.HasArt(level), Is.False);
                Assert.That(art.ForLevel(level), Is.SameAs(fallback));
            }
        }

        [Test]
        public void LoadPlanetSprites_ResolvesOneSpritePerTier_ByNamingConvention()
        {
            var sprites = FruitArt.LoadPlanetSprites();

            Assert.That(sprites.Length, Is.EqualTo(GameBalance.MaxTier),
                "正式美术应覆盖全部等级（1..10）");

            for (var i = 0; i < sprites.Length; i++)
            {
                Assert.That(sprites[i], Is.Not.Null,
                    $"Resources 下缺少 kenney_planets/Planets/planet{i:00}（或未导入为 Sprite）");
                Assert.That(sprites[i].name, Is.EqualTo($"planet{i:00}"),
                    "加载顺序必须与文件名一致：等级 1 → planet00，依次顺延");
            }
        }
    }
}
