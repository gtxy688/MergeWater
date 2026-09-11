using System.Collections;
using System.Collections.Generic;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M7 验收 A6/E3 与 R11–R15：道具领取、使用与取消的编排。</summary>
    public sealed class ItemUseTests
    {
        private BootstrapTestHarness _harness;
        private readonly List<KeyValuePair<ItemKind, ItemUseResult>> _itemEvents =
            new List<KeyValuePair<ItemKind, ItemUseResult>>();

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
            _harness = null;
            _itemEvents.Clear();
        }

        private void StartPlayable()
        {
            _harness.DropOnce();
            _harness.Context.Session.Events.ItemUsed += (kind, result) =>
                _itemEvents.Add(new KeyValuePair<ItemKind, ItemUseResult>(kind, result));
        }

        [UnityTest]
        public IEnumerator UseBomb_DeductsStockAndRemovesFruits()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();
            StartPlayable();

            _harness.Context.Economy.Inventory.Grant(ItemKind.Bomb, _harness.Context.Balance);
            _harness.Field.SpawnAt(1, new Vector2(-0.2f, 0f), out _);
            _harness.Field.SpawnAt(3, new Vector2(0.2f, 0f), out _);
            _harness.Field.SpawnAt(2, new Vector2(1.9f, 0f), out _);

            _harness.Context.Items.OnEntryClicked(ItemKind.Bomb);
            Assert.That(_harness.Context.Aim.State.IsItemAim, Is.True, "有库存时应进入瞄准模式");

            var before = _harness.Field.LiveFruitCount;
            _harness.Context.Items.ApplyTargetedItem(ItemKind.Bomb, Vector2.zero);
            yield return null;

            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Bomb), Is.EqualTo(0), "使用后扣除库存");
            Assert.That(_harness.Field.LiveFruitCount, Is.LessThan(before), "范围内水果被清除（R12）");
            Assert.That(_itemEvents.Exists(e => e.Key == ItemKind.Bomb && e.Value == ItemUseResult.Applied), Is.True);
        }

        [UnityTest]
        public IEnumerator CancelAim_DoesNotDeductStockOrRemoveFruit()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();
            StartPlayable();

            _harness.Context.Economy.Inventory.Grant(ItemKind.Hammer, _harness.Context.Balance);
            _harness.Field.SpawnAt(1, Vector2.zero, out _);
            var before = _harness.Field.LiveFruitCount;

            _harness.Context.Items.OnEntryClicked(ItemKind.Hammer);
            Assert.That(_harness.Context.Aim.State.IsItemAim, Is.True);

            _harness.Context.Aim.CancelItemAim();
            yield return null;

            Assert.That(_harness.Context.Aim.State.IsItemAim, Is.False);
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Hammer), Is.EqualTo(1), "取消不扣库存");
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(before), "取消不产生效果");
        }

        [UnityTest]
        public IEnumerator EmptyStock_PlaysRewardedAdThenContinuesToAim()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();
            _harness.GetMockAds().Configure(true, 0f, 0f);
            StartPlayable();

            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Bomb), Is.EqualTo(0));

            _harness.Context.Items.OnEntryClicked(ItemKind.Bomb);
            yield return null;

            Assert.That(_harness.GetMockAds().RewardedShown, Is.EqualTo(1), "库存为 0 时应先看激励视频（R15）");
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Bomb), Is.EqualTo(1), "看完广告获得 1 个");
            Assert.That(_harness.Context.Aim.State.IsItemAim, Is.True, "领取成功后应自动继续这次使用（D11）");
        }

        [UnityTest]
        public IEnumerator Undo_RemovesLastUnmergedDrop_AndDeductsStock()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();
            StartPlayable();

            _harness.Context.Economy.Inventory.Grant(ItemKind.Undo, _harness.Context.Balance);
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(1), "已投放一颗");

            _harness.Context.Items.OnEntryClicked(ItemKind.Undo);
            yield return null;

            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(0), "撤销移除最后一颗未合成水果（R11）");
            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Undo), Is.EqualTo(0));
            Assert.That(_itemEvents.Exists(e => e.Key == ItemKind.Undo && e.Value == ItemUseResult.Applied), Is.True);
        }

        [UnityTest]
        public IEnumerator Shake_PlaysAd_MovesFruitsAndConsumesDailyQuota()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();
            _harness.GetMockAds().Configure(true, 0f, 0f);
            StartPlayable();

            _harness.Field.SpawnAt(1, new Vector2(-0.8f, 0f), out _);
            _harness.Field.SpawnAt(2, new Vector2(0.8f, 0f), out _);
            var before = _harness.Field.LiveFruitCount;
            var remainingBefore = _harness.Context.Economy.ItemRemainingToday(ItemKind.Shake);

            _harness.Context.Items.OnEntryClicked(ItemKind.Shake);
            yield return null;

            Assert.That(_harness.GetMockAds().RewardedShown, Is.EqualTo(1), "摇一摇仅通过激励视频触发（R14）");
            Assert.That(_harness.Field.LiveFruitCount, Is.EqualTo(before), "摇一摇不直接消除水果");
            Assert.That(_harness.Context.Economy.ItemRemainingToday(ItemKind.Shake),
                Is.EqualTo(remainingBefore - 1), "消耗当日摇一摇次数（V2.14）");
        }

        [UnityTest]
        public IEnumerator UseBomb_WithNoFruitInRadius_DoesNotDeductStock()
        {
            _harness = BootstrapTestHarness.Create(privacyAccepted: true);
            yield return _harness.Activate();
            StartPlayable();

            _harness.Context.Economy.Inventory.Grant(ItemKind.Bomb, _harness.Context.Balance);
            _harness.Field.SpawnAt(1, new Vector2(2.0f, 0f), out _);

            _harness.Context.Items.ApplyTargetedItem(ItemKind.Bomb, new Vector2(-2f, 0f));
            yield return null;

            Assert.That(_harness.Context.Economy.ItemCount(ItemKind.Bomb), Is.EqualTo(1), "空放不扣库存");
            Assert.That(_itemEvents.Exists(e => e.Key == ItemKind.Bomb && e.Value == ItemUseResult.NoTarget), Is.True);
        }
    }
}
