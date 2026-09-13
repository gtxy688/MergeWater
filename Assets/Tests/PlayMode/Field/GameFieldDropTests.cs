using System.Collections;
using System.Collections.Generic;
using MergeWater.Core;
using MergeWater.Field;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M2 验收 A1/A2/E4：水果生成与上限（V1/V3）。</summary>
    public sealed class GameFieldDropTests
    {
        private FieldTestRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [UnityTest]
        public IEnumerator Drop_ValidTier_SpawnsOneFruitWithConfiguredRadiusAndMass()
        {
            _rig = new FieldTestRig(buildArena: true);

            Assert.That(_rig.Field.Drop(3, 0.5f, out var fruitId), Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1));

            var fruit = _rig.Root.transform.Find($"Fruit_3_{fruitId}");
            Assert.That(fruit, Is.Not.Null, "生成的对象应按 等级_id 命名");

            var body = fruit.GetComponent<FruitBody>();
            Assert.That(body.Level, Is.EqualTo(3));
            Assert.That(body.Collider.radius, Is.EqualTo(0.45f).Within(1e-4f), "V1 等级 3 半径（2026-09-13 整体 x1.5）");
            Assert.That(body.Body.mass, Is.EqualTo(0.28f).Within(1e-4f), "V1 等级 3 质量");
            Assert.That(body.Body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous), "V3 需开启 CCD");
        }

        [UnityTest]
        public IEnumerator Drop_OutOfBoundsX_ClampsInsideField()
        {
            _rig = new FieldTestRig(buildArena: true);

            _rig.Field.Drop(1, 999f, out var fruitId);
            yield return new WaitForFixedUpdate();

            var body = _rig.Root.transform.Find($"Fruit_1_{fruitId}").GetComponent<FruitBody>();
            var limit = _rig.Balance.FieldHalfWidth - _rig.Balance.GetTier(1).Radius;

            Assert.That(body.transform.position.x, Is.LessThanOrEqualTo(limit + 1e-3f));
        }

        [UnityTest]
        public IEnumerator Drop_InvalidTierOrBeyondLimit_ReturnsFalseWithoutSpawning()
        {
            _rig = new FieldTestRig(gravity: 0f);

            Assert.That(_rig.Field.Drop(0, 0f, out _), Is.False, "等级 0 非法");
            Assert.That(_rig.Field.Drop(12, 0f, out _), Is.False, "等级 12 非法");
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(0));

            // 网格间距 0.5 > 两颗 1 级水果的接触距离 0.36，避免测试自身触发合成。
            var spawned = 0;
            for (var row = 0; row < 10 && spawned < _rig.Balance.MaxLiveFruits; row++)
            {
                for (var column = 0; column < 12 && spawned < _rig.Balance.MaxLiveFruits; column++)
                {
                    var position = new Vector2(-2.2f + column * 0.5f, row * 0.5f);
                    Assert.That(_rig.Field.SpawnAt(1, position, out _), Is.True, $"第 {spawned} 颗应生成成功");
                    spawned++;
                }
            }

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(_rig.Balance.MaxLiveFruits));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("数量上限"));
            Assert.That(_rig.Field.Drop(1, 0f, out _), Is.False, "达到上限后拒绝生成");
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(_rig.Balance.MaxLiveFruits));
            yield return null;
        }
    }
}
