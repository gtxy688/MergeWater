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
