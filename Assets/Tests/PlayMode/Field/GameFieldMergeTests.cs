using System.Collections;
using System.Collections.Generic;
using MergeWater.Core;
using MergeWater.Field;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M2 验收 A3/A4/E1：同级合成与顶点保护（R3、V2.7）。</summary>
    public sealed class GameFieldMergeTests
    {
        private FieldTestRig _rig;
        private readonly List<MergeEvent> _merges = new List<MergeEvent>();

        [SetUp]
        public void SetUp()
        {
            _merges.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        private void TrackMerges() => _rig.Field.Merged += evt => _merges.Add(evt);

        [UnityTest]
        public IEnumerator SameTierCollision_MergesIntoNextTierAndRaisesMergedOnce()
        {
            _rig = new FieldTestRig(gravity: 0f);
            TrackMerges();

            var radius = _rig.Radius(1);
            _rig.Field.SpawnAt(1, new Vector2(-radius + 0.01f, 0f), out _);
            _rig.Field.SpawnAt(1, new Vector2(radius - 0.01f, 0f), out _);

            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(2));

            yield return new WaitForSeconds(0.4f);

            Assert.That(_merges.Count, Is.EqualTo(1), "每次成功合成恰好发布一次 Merged");
            Assert.That(_merges[0].SourceLevel, Is.EqualTo(1));
            Assert.That(_merges[0].ResultLevel, Is.EqualTo(2));
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1), "两颗合成一颗");

            var survivor = _rig.Root.transform.Find("Fruit_2_2");
            Assert.That(survivor, Is.Not.Null, "结果应为 2 级水果");
            Assert.That(survivor.GetComponent<FruitBody>().Level, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TopTierCollision_DoesNotMerge()
        {
            _rig = new FieldTestRig(gravity: 0f);
            TrackMerges();

            var radius = _rig.Radius(GameBalance.MaxTier);
            _rig.Field.SpawnAt(GameBalance.MaxTier, new Vector2(-radius + 0.01f, 0f), out _);
            _rig.Field.SpawnAt(GameBalance.MaxTier, new Vector2(radius - 0.01f, 0f), out _);

            yield return new WaitForSeconds(0.4f);

            Assert.That(_merges, Is.Empty, "11 级为顶点，不应产生合成");
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator MergeCancelled_WhenOneFruitRemovedDuringAbsorb()
        {
            _rig = new FieldTestRig(gravity: 0f);
            TrackMerges();

            var radius = _rig.Radius(1);
            _rig.Field.SpawnAt(1, new Vector2(-radius + 0.01f, 0f), out var firstId);
            _rig.Field.SpawnAt(1, new Vector2(radius - 0.01f, 0f), out _);

            yield return new WaitForFixedUpdate();

            Assert.That(_rig.Field.RemoveFruit(firstId), Is.True, "吸附期间应可被道具移除");

            yield return new WaitForSeconds(0.4f);

            Assert.That(_merges, Is.Empty, "吸附被中断时不应产生合成事件");
            Assert.That(_rig.Field.LiveFruitCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MergeResult_PopsUpward_SoItDoesNotImmediatelyReMerge()
        {
            _rig = new FieldTestRig(gravity: 0f);

            var radius = _rig.Radius(1);
            _rig.Field.SpawnAt(1, new Vector2(-radius + 0.01f, 0f), out _);
            _rig.Field.SpawnAt(1, new Vector2(radius - 0.01f, 0f), out _);

            yield return new WaitForSeconds(0.25f);

            var survivor = _rig.Root.transform.Find("Fruit_2_2");
            Assert.That(survivor, Is.Not.Null);
            Assert.That(survivor.GetComponent<FruitBody>().Body.velocity.y, Is.GreaterThan(0f),
                "结果水果应有向上初速（V2.21 后脱离堆叠）");
        }
    }
}
