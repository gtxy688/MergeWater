using System.Collections;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M7 验收 A7 与 R10：阶段目标奖励发放一次并落库。</summary>
    public sealed class MilestoneRewardTests
    {
        private BootstrapTestHarness _harness;

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
            _harness = null;
        }

        /// <summary>
        /// 两颗 10 级合成 11 级（105 分）；相邻放置，生成当帧即接触合成。
        /// 两次合成后总分 105 + 116（1.1 倍率）= 221，越过 200 分里程碑但未到 500。
        /// </summary>
        private IEnumerator SpawnTwoTierTenMerges(float y)
        {
            _harness.Field.SpawnAt(10, new Vector2(-0.99f, y), out _);
            _harness.Field.SpawnAt(10, new Vector2(0.99f, y), out _);
            yield return new WaitForSeconds(0.4f);
        }

        [UnityTest]
        public IEnumerator MilestoneReached_GrantsItemOnceAndPersists()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            _harness.DropOnce();
            yield return null;
            Assert.That(_harness.Context.Session.Phase, Is.EqualTo(RoundPhase.Playing));

            yield return SpawnTwoTierTenMerges(0f);
            yield return SpawnTwoTierTenMerges(2.0f);

            var session = _harness.Context.Session;
            Assert.That(session.Score, Is.EqualTo(221), "105 + 116（连击倍率 1.1）= 221");

            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(1),
                "越 200 分里程碑应发放 1 个撤销（V2.18）");

            Assert.That(_harness.Store.TryRead(out var json), Is.True);
            Assert.That(json, Does.Contain("\"undoCount\":1"), "奖励必须落盘（R10 + 持久化）");
        }

        [UnityTest]
        public IEnumerator MilestoneReached_IsNotGrantedTwiceInSameRound()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            _harness.DropOnce();
            yield return null;

            yield return SpawnTwoTierTenMerges(0f);
            yield return SpawnTwoTierTenMerges(2.0f);
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(1));

            // 清场后继续合成，总分再次越过 200 分但仍低于 500 分
            _harness.Field.ClearAll();
            yield return null;

            yield return SpawnTwoTierTenMerges(0f);
            yield return SpawnTwoTierTenMerges(2.0f);

            var score = _harness.Context.Session.Score;
            Assert.That(score, Is.GreaterThan(400), "已再次越过 200 分");
            Assert.That(score, Is.LessThan(500), "但未到 500 分节点");
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(1),
                "同一节点每局只发放一次（R10）");
        }
    }
}
