using System.Collections;
using MergeWater.Field;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M2 验收 A7/E2：重开清理、仿真开关与逃逸回收（E2）。</summary>
    public sealed class GameFieldLifecycleTests
    {
        private FieldTestRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [UnityTest]
        public IEnumerator ClearAll_RemovesEveryFruit()
        {
            _rig = new FieldTestRig(gravity: 0f);

            _rig.Field.SpawnAt(1, new Vector2(-0.8f, 0f), out _);
            _rig.Field.SpawnAt(2, new Vector2(0f, 0f), out _);
            _rig.Field.Drop(1, 0.8f, out _);
            yield return new WaitForFixedUpdate();

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(3));

            _rig.Field.ClearAll();
            yield return null;

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(0));
            Assert.That(_rig.Root.transform.Find("Fruit_1_0"), Is.Null);
        }

        [UnityTest]
        public IEnumerator SimulationToggle_FreezesAndRestoresBodies()
        {
            _rig = new FieldTestRig(gravity: -20f);

            _rig.Field.Drop(1, 0f, out var fruitId);
            yield return new WaitForFixedUpdate();

            var fruit = _rig.Root.transform.Find($"Fruit_1_{fruitId}").GetComponent<FruitBody>();

            _rig.Field.SetSimulationEnabled(false);
            Assert.That(fruit.Body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic), "冻结时切到 Kinematic");
            Assert.That(_rig.Field.SimulationEnabled, Is.False);

            yield return new WaitForFixedUpdate();

            _rig.Field.SetSimulationEnabled(true);
            Assert.That(fruit.Body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic), "恢复时切回 Dynamic");
            Assert.That(_rig.Field.SimulationEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator EscapedFruit_IsRecycledInsteadOfLeaking()
        {
            _rig = new FieldTestRig(gravity: -80f, buildArena: false);

            _rig.Field.SpawnAt(1, new Vector2(0f, 0f), out _);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("离开场地"));

            yield return new WaitForSeconds(0.6f);

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(0), "掉出场地的水果应被回收");
        }
    }
}
