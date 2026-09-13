using System.Collections;
using MergeWater.Field;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// 水果视觉尺寸的不变量（2026-09-13：水果图换成 `kenney_planets` 后新增）。
    ///
    /// <para>存在理由：正式美术是 1280×1280 的贴图，若沿用「固定按 Radius×2 缩放」的老写法，
    /// 视觉尺寸会随素材导入设置漂移——实测 1280px @ PPU 100 是 **12.8 世界单位**，
    /// 而 1 级水果直径只有 0.36，接进去会直接糊满整个屏幕。
    /// 因此把「视觉直径 == 碰撞直径」钉成一条**与贴图 PPU/像素尺寸无关**的不变量。</para>
    /// </summary>
    public sealed class FruitArtVisualTests
    {
        private FieldTestRig _rig;
        private Texture2D _texture;
        private Sprite _sprite;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;

            if (_sprite != null)
                Object.Destroy(_sprite);
            if (_texture != null)
                Object.Destroy(_texture);

            _sprite = null;
            _texture = null;
        }

        [UnityTest]
        public IEnumerator VisualSize_MatchesColliderDiameter_EvenWhenSpriteImportSettingsDiffer()
        {
            _rig = new FieldTestRig(gravity: 0f);

            // 故意造一张「世界尺寸 2.56 单位」的贴图（256px @ 100 PPU）：
            // 老写法固定按 Radius×2 缩放，视觉会比碰撞体大 2.56 倍。
            _texture = new Texture2D(256, 256);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 256f, 256f), new Vector2(0.5f, 0.5f), 100f);

            Assert.That(_sprite.bounds.size.x, Is.EqualTo(2.56f).Within(1e-3f),
                "前置条件：这张贴图的世界尺寸应为 2.56 单位");

            _rig.Field.Configure(_rig.Balance, _sprite, null, buildArena: false);
            Assert.That(_rig.Field.SpawnAt(1, Vector2.zero, out var fruitId), Is.True);
            yield return null;

            var body = FindFruitBodyById(fruitId);
            Assert.That(body, Is.Not.Null);

            var worldSize = body.Visual.GetComponent<SpriteRenderer>().bounds.size;

            Assert.That(worldSize.x, Is.EqualTo(body.Radius * 2f).Within(1e-3f),
                "视觉宽度必须等于碰撞直径（与贴图 PPU / 像素尺寸无关）");
            Assert.That(worldSize.y, Is.EqualTo(body.Radius * 2f).Within(1e-3f),
                "视觉高度必须等于碰撞直径");
        }

        private static FruitBody FindFruitBodyById(int fruitId)
        {
            foreach (var body in Object.FindObjectsOfType<FruitBody>())
            {
                if (body.FruitId == fruitId)
                    return body;
            }

            return null;
        }
    }
}
