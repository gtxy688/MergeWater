using System.Collections;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// R10 变更后的回归（需求方 2026-09-12）：
    /// **连续合成不再发放奖励**——道具只能通过（激励视频）广告获取。
    /// 里程碑事件仍会发布（供埋点/阶段推进），但不再授予任何道具、也不落库。
    /// </summary>
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
        /// 两颗 9 级合成 10 级（91 分）；相邻放置，生成当帧即接触合成。
        /// 顶级由 11 级下调为 10 级后（2026-09-13 需求方：减一级适配美术资源），
        /// 跨越 200 分里程碑需要三次合成：91 + 100（1.1 倍率）+ 109（1.2 倍率）= 300。
        /// </summary>
        private IEnumerator SpawnTierNineMergePair(float y)
        {
            _harness.Field.SpawnAt(9, new Vector2(-0.87f, y), out _);
            _harness.Field.SpawnAt(9, new Vector2(0.87f, y), out _);
            yield return new WaitForSeconds(0.4f);
        }

        [UnityTest]
        public IEnumerator MilestoneReached_DoesNotGrantFreeItem()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            var undoBefore = _harness.Context.Economy.ItemCount(ItemKind.Undo);

            _harness.DropOnce();
            yield return null;
            Assert.That(_harness.Context.Session.Phase, Is.EqualTo(RoundPhase.Playing));

            yield return SpawnTierNineMergePair(0f);
            yield return SpawnTierNineMergePair(2.0f);
            yield return SpawnTierNineMergePair(4.0f);

            var session = _harness.Context.Session;
            Assert.That(session.Score, Is.EqualTo(300), "91 + 100（1.1 倍率）+ 109（1.2 倍率）= 300");

            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(undoBefore),
                "越过 200 分里程碑不应再免费发道具（需求方：奖励只通过广告获取）");

            Assert.That(_harness.Store.TryRead(out var json), Is.True);
            Assert.That(json, Does.Not.Contain("\"undoCount\":1"),
                "不应因里程碑把道具写入存档");
        }

        [UnityTest]
        public IEnumerator MilestoneReached_RepeatedMergesStillGrantNothing()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();

            var undoBefore = _harness.Context.Economy.ItemCount(ItemKind.Undo);

            _harness.DropOnce();
            yield return null;

            yield return SpawnTierNineMergePair(0f);
            yield return SpawnTierNineMergePair(2.0f);
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(undoBefore));

            // 清场后继续合成，总分再次越过 200 分但仍低于 500 分
            _harness.Field.ClearAll();
            yield return null;

            yield return SpawnTierNineMergePair(0f);
            yield return SpawnTierNineMergePair(2.0f);

            var score = _harness.Context.Session.Score;
            Assert.That(score, Is.GreaterThan(400), "已再次越过 200 分");
            Assert.That(score, Is.LessThan(500), "但未到 500 分节点");
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(undoBefore),
                "反复连击/越里程碑都不发道具（奖励只通过广告获取）");
        }
    }
}
