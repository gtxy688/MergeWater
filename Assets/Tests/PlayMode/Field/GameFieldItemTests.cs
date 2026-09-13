using System.Collections;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M2 验收 A6/E3：道具对场地的作用（R11–R14、V2.17、E3）。</summary>
    public sealed class GameFieldItemTests
    {
        private FieldTestRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [UnityTest]
        public IEnumerator Bomb_RemovesFruitsInRadius()
        {
            _rig = new FieldTestRig(gravity: 0f);

            _rig.Field.SpawnAt(1, new Vector2(-0.2f, 0f), out _);
            _rig.Field.SpawnAt(1, new Vector2(0.2f, 0f), out _);
            _rig.Field.SpawnAt(2, new Vector2(0.5f, 0f), out _);
            _rig.Field.SpawnAt(3, new Vector2(1.9f, 0f), out _);

            var removed = _rig.Field.RemoveFruitInRadius(Vector2.zero, _rig.Balance.BombRadius);

            Assert.That(removed, Is.EqualTo(3), "0.6m 内三颗应被清除");
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1), "半径外的水果保留");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bomb_WithNonPositiveRadius_RemovesNothing()
        {
            _rig = new FieldTestRig(gravity: 0f);
            _rig.Field.SpawnAt(1, Vector2.zero, out _);

            Assert.That(_rig.Field.RemoveFruitInRadius(Vector2.zero, 0f), Is.EqualTo(0));
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hammer_RemovesSingleNearest()
        {
            _rig = new FieldTestRig(gravity: 0f);

            _rig.Field.SpawnAt(1, new Vector2(0.1f, 0f), out var nearId);
            _rig.Field.SpawnAt(3, new Vector2(1.2f, 0f), out var farId);

            var removed = _rig.Field.RemoveSingleNearest(Vector2.zero, _rig.Balance.HammerMaxRadius, out var removedId);

            Assert.That(removed, Is.True);
            Assert.That(removedId, Is.EqualTo(nearId), "只应移除最近的一颗");
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1));
            Assert.That(_rig.Field.RemoveFruit(farId), Is.True, "较远的水果仍存在");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hammer_WhenNothingInRange_ReturnsFalse()
        {
            _rig = new FieldTestRig(gravity: 0f);
            _rig.Field.SpawnAt(1, new Vector2(2.0f, 0f), out _);

            Assert.That(_rig.Field.RemoveSingleNearest(new Vector2(-2f, 0f), 0.5f, out _), Is.False);
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Shake_MovesFruitsWithoutRemoving()
        {
            _rig = new FieldTestRig(gravity: 0f);

            _rig.Field.SpawnAt(1, new Vector2(-0.8f, 0f), out var firstId);
            _rig.Field.SpawnAt(2, new Vector2(0.8f, 0f), out _);

            var before = _rig.Root.transform.Find($"Fruit_1_{firstId}").position;

            _rig.Field.ApplyShakeShuffle(_rig.Balance.ShakeImpulse, 12345);
            yield return new WaitForSeconds(0.15f);

            var after = _rig.Root.transform.Find($"Fruit_1_{firstId}").position;

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(2), "摇一摇不直接消除水果（R14）");
            Assert.That(Vector3.Distance(before, after), Is.GreaterThan(0.01f), "水果应发生可见位移");
        }
    }
}
