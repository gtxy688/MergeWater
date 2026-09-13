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
        public IEnumerator MergeResult_PushedSidewaysAtMidpoint()
        {
            // V2.31b（需求方要求）：合成结果不再向上蹦，而是「左右推开」——
            // 位置仍是两颗的中点，但会获得水平初速（方向按 id 左右交替），
            // 把邻居朝两侧推、腾出继续合成的空间。
            _rig = new FieldTestRig(gravity: 0f);

            var radius = _rig.Radius(1);
            _rig.Field.SpawnAt(1, new Vector2(-radius + 0.01f, 0f), out _);
            _rig.Field.SpawnAt(1, new Vector2(radius - 0.01f, 0f), out _);

            yield return new WaitForSeconds(0.25f);

            var survivor = _rig.Root.transform.Find("Fruit_2_2");
            Assert.That(survivor, Is.Not.Null, "应生成 2 级结果水果");

            var body = survivor.GetComponent<FruitBody>();
            Assert.That(body.Body.velocity.y, Is.LessThanOrEqualTo(0.05f), "结果水果不应向上蹦（需求方要求）");
            Assert.That(Mathf.Abs(body.Body.velocity.x), Is.GreaterThan(0.05f), "结果水果应获得水平推力");
            Assert.That(Mathf.Abs(survivor.position.x), Is.GreaterThan(0.02f), "结果水果应已向侧面移动");
            Assert.That(survivor.position.y, Is.EqualTo(0f).Within(0.05f), "不应向上移动");
        }

        [UnityTest]
        public IEnumerator MergeResult_SidePushEqualsConfiguredImpulse()
        {
            // 需求方（2026-09-12）：「水果合成后，给的力度太吝啬」——合成结果必须真的拿到
            // V2.31b 配置的水平初速（最初 0.45 时几乎立刻被阻尼磨平，看不出被推开）。
            // 在 Merged 当帧读取速度，避免结论变成「测了多久」的函数。
            _rig = new FieldTestRig(gravity: 0f);

            var pushed = Vector2.zero;
            var captured = false;
            _rig.Field.Merged += evt =>
            {
                foreach (Transform child in _rig.Root.transform)
                {
                    if (!child.name.StartsWith($"Fruit_{evt.ResultLevel}_"))
                        continue;

                    pushed = child.GetComponent<Rigidbody2D>().velocity;
                    captured = true;
                    break;
                }
            };

            var radius = _rig.Radius(1);
            _rig.Field.SpawnAt(1, new Vector2(-radius + 0.01f, 0f), out _);
            _rig.Field.SpawnAt(1, new Vector2(radius - 0.01f, 0f), out _);

            yield return new WaitForSeconds(0.2f);

            Assert.That(captured, Is.True, "两颗同级水果接触后应发生合成");
            Assert.That(Mathf.Abs(pushed.x), Is.EqualTo(_rig.Balance.MergeResultSideImpulse).Within(1e-3f),
                "合成结果应获得 V2.31b 配置的水平初速（需求方：力度太吝啬）");
            Assert.That(Mathf.Abs(pushed.y), Is.LessThan(1e-3f), "合成结果不得向上蹦（V2.31b）");
        }
    }
}
