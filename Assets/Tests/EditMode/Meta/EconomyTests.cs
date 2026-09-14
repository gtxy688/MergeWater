using System;
using MergeWater.Core;
using MergeWater.Meta;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M6 验收 A4–A9：库存、每日上限、广告放行、分享冷却（V2.12–V2.19）。</summary>
    public sealed class EconomyTests
    {
        private MetaTestContext _context;

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            _context = null;
        }

        private (DailyLimitService daily, InventoryService inventory) CreateInventory()
        {
            var daily = new DailyLimitService(_context.Save.Data, _context.Clock);
            return (daily, new InventoryService(_context.Save.Data, daily));
        }

        [Test]
        public void Grant_BeyondDailyCap_IsRejected_AndSpendBelowZero_IsRejected()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var (_, inventory) = CreateInventory();

            for (var i = 0; i < _context.Balance.ItemDailyCap; i++)
                Assert.That(inventory.Grant(ItemKind.Undo, _context.Balance), Is.EqualTo(GrantResult.Granted), $"第 {i + 1} 次应成功");

            Assert.That(inventory.Grant(ItemKind.Undo, _context.Balance), Is.EqualTo(GrantResult.DailyCapReached),
                "V2.13：撤销每日上限 3");
            Assert.That(inventory.Count(ItemKind.Undo), Is.EqualTo(3));

            Assert.That(inventory.Spend(ItemKind.Undo), Is.EqualTo(SpendResult.Spent));
            Assert.That(inventory.Spend(ItemKind.Undo), Is.EqualTo(SpendResult.Spent));
            Assert.That(inventory.Spend(ItemKind.Undo), Is.EqualTo(SpendResult.Spent));
            Assert.That(inventory.Spend(ItemKind.Undo), Is.EqualTo(SpendResult.NoStock), "库存为 0 时不可使用");
            Assert.That(inventory.Count(ItemKind.Undo), Is.EqualTo(0));
        }

        [Test]
        public void ItemDailyCaps_MatchV213ToV215()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var (daily, inventory) = CreateInventory();

            Assert.That(inventory.RemainingToday(ItemKind.Undo, _context.Balance), Is.EqualTo(3), "V2.13");
            Assert.That(inventory.RemainingToday(ItemKind.Bomb, _context.Balance), Is.EqualTo(3), "V2.13");
            Assert.That(inventory.RemainingToday(ItemKind.Hammer, _context.Balance), Is.EqualTo(3), "V2.13");
            Assert.That(inventory.RemainingToday(ItemKind.Shake, _context.Balance), Is.EqualTo(2), "V2.14");
            Assert.That(inventory.GiftRemainingToday(_context.Balance), Is.EqualTo(1), "V2.15");

            for (var i = 0; i < 2; i++)
                Assert.That(inventory.Grant(ItemKind.Shake, _context.Balance), Is.EqualTo(GrantResult.Granted));

            Assert.That(inventory.Grant(ItemKind.Shake, _context.Balance), Is.EqualTo(GrantResult.DailyCapReached),
                "摇一摇每日上限 2");

            Assert.That(daily.GiftGrantedToday, Is.EqualTo(0));
        }

        [Test]
        public void GrantGiftBundle_GrantsThreeItems_OncePerDay()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var (_, inventory) = CreateInventory();

            Assert.That(inventory.GrantGiftBundle(_context.Balance), Is.EqualTo(GrantResult.Granted));
            Assert.That(inventory.Count(ItemKind.Undo), Is.EqualTo(1));
            Assert.That(inventory.Count(ItemKind.Bomb), Is.EqualTo(1));
            Assert.That(inventory.Count(ItemKind.Hammer), Is.EqualTo(1));

            Assert.That(inventory.GrantGiftBundle(_context.Balance), Is.EqualTo(GrantResult.DailyCapReached),
                "大礼包每日 1 次（V2.15）");
        }

        [Test]
        public void DailyLimits_ResetOnNextLocalDay()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var (daily, inventory) = CreateInventory();

            for (var i = 0; i < 3; i++)
                inventory.Grant(ItemKind.Bomb, _context.Balance);

            Assert.That(inventory.RemainingToday(ItemKind.Bomb, _context.Balance), Is.EqualTo(0));
            Assert.That(inventory.Grant(ItemKind.Bomb, _context.Balance), Is.EqualTo(GrantResult.DailyCapReached));

            _context.Clock.Advance(TimeSpan.FromDays(1));

            Assert.That(daily.EnsureCurrentDay(), Is.True, "跨天应重置");
            Assert.That(inventory.RemainingToday(ItemKind.Bomb, _context.Balance), Is.EqualTo(3));
            Assert.That(inventory.Grant(ItemKind.Bomb, _context.Balance), Is.EqualTo(GrantResult.Granted));
        }

        [Test]
        public void GrantUnlimited_IgnoresDailyCap()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var (_, inventory) = CreateInventory();

            for (var i = 0; i < 6; i++)
                Assert.That(inventory.GrantUnlimited(ItemKind.Undo), Is.EqualTo(GrantResult.Granted));

            Assert.That(inventory.Count(ItemKind.Undo), Is.EqualTo(6), "阶段目标奖励不受每日上限约束");
        }

        [Test]
        public void Revive_RespectsNinetySecondGlobalCooldown()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var economy = _context.CreateEconomy();

            Assert.That(economy.CanRevive().IsAllowed, Is.True, "首次可复活");

            economy.MarkReviveAdShown();

            var blocked = economy.CanRevive();
            Assert.That(blocked.IsAllowed, Is.False);
            Assert.That(blocked.Decision, Is.EqualTo(AdDecision.CooldownActive), "V2.12：全局冷却 90s");

            _context.Clock.Advance(TimeSpan.FromSeconds(89));
            Assert.That(economy.CanRevive().IsAllowed, Is.False);

            _context.Clock.Advance(TimeSpan.FromSeconds(2));
            Assert.That(economy.CanRevive().IsAllowed, Is.True, "超过 90s 后放行");
        }

        [Test]
        public void Revive_OfflinePolicy_FollowsOfflineGrantAll()
        {
            _context = new MetaTestContext();
            _context.Save.Load();

            var offlineAllowed = _context.CreateEconomy(new NullAdsService(), offlineGrantAll: true);
            Assert.That(offlineAllowed.CanRevive().IsAllowed, Is.True, "D6：离线时直接放行");

            _context.UseSettings(true, false);
            var offlineDenied = _context.CreateEconomyWithCurrentSettings(new NullAdsService());
            Assert.That(offlineDenied.CanRevive().IsAllowed, Is.False, "关闭离线领取时不可用");
            Assert.That(offlineDenied.CanRevive().Decision, Is.EqualTo(AdDecision.Unavailable));
        }

        [Test]
        public void ItemGrant_RespectsDailyCap_RegardlessOfAdsAvailability()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var economy = _context.CreateEconomy();

            for (var i = 0; i < 3; i++)
            {
                GrantResult granted = GrantResult.Failed;
                economy.RequestItemGrant(ItemKind.Bomb, (result, _, _) => granted = result);
                Assert.That(granted, Is.EqualTo(GrantResult.Granted), $"第 {i + 1} 次领取应成功");
            }

            var decision = economy.CanGrantItem(ItemKind.Bomb);
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Decision, Is.EqualTo(AdDecision.DailyCapReached));

            GrantResult fourth = GrantResult.Granted;
            economy.RequestItemGrant(ItemKind.Bomb, (result, _, _) => fourth = result);
            Assert.That(fourth, Is.EqualTo(GrantResult.DailyCapReached));
            Assert.That(economy.ItemCount(ItemKind.Bomb), Is.EqualTo(3), "上限后不再增加");
        }

        [Test]
        public void ItemGrant_WhenAdSkipped_DoesNotGrant()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var ads = new FakeAdsService { RewardedResult = RewardedResult.Skipped };
            var economy = _context.CreateEconomy(ads);

            GrantResult result = GrantResult.Granted;
            economy.RequestItemGrant(ItemKind.Hammer, (grant, _, _) => result = grant);

            Assert.That(result, Is.EqualTo(GrantResult.Failed), "广告未完成不发道具");
            Assert.That(economy.ItemCount(ItemKind.Hammer), Is.EqualTo(0));
        }

        [Test]
        public void Gift_OncePerDay_AndRequiresCompletedAd()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var economy = _context.CreateEconomy();

            GrantResult first = GrantResult.Failed;
            economy.RequestGiftGrant((grant, _, _) => first = grant);
            Assert.That(first, Is.EqualTo(GrantResult.Granted));
            Assert.That(economy.ItemCount(ItemKind.Undo), Is.EqualTo(1));

            GrantResult second = GrantResult.Granted;
            economy.RequestGiftGrant((grant, _, _) => second = grant);
            Assert.That(second, Is.EqualTo(GrantResult.DailyCapReached), "V2.15：大礼包每日 1 次");
            Assert.That(economy.ItemCount(ItemKind.Undo), Is.EqualTo(1));
        }

        [Test]
        public void Interstitial_ShowsAtMostOncePerThreeGames_AndRespectsToggle()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var economy = _context.CreateEconomy(interstitialEnabled: true);

            economy.OnRoundFinished(100, 2);
            Assert.That(economy.CanShowInterstitial().IsAllowed, Is.True, "满足间隔时首次可展示");

            InterstitialResult shown = InterstitialResult.Skipped;
            economy.ShowInterstitial(result => shown = result);
            Assert.That(shown, Is.EqualTo(InterstitialResult.Shown));

            economy.OnRoundFinished(120, 2);
            Assert.That(economy.CanShowInterstitial().IsAllowed, Is.False, "V2.16：每 3 局至多 1 次");

            economy.OnRoundFinished(140, 3);
            Assert.That(economy.CanShowInterstitial().IsAllowed, Is.False);

            economy.OnRoundFinished(160, 3);
            Assert.That(economy.CanShowInterstitial().IsAllowed, Is.True, "第 3 局后再次放行");

            _context.UseSettings(false, true);
            var toggledOff = _context.CreateEconomyWithCurrentSettings();
            var decision = toggledOff.CanShowInterstitial();
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Decision, Is.EqualTo(AdDecision.DeniedByToggle), "远程开关关闭时不展示");
        }

        [Test]
        public void ShareChallenge_FirstSucceeds_ThenCooldownForSixtySeconds()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var share = new ShareService(_context.Save.Data, _context.Save, _context.Clock, _context.Balance);

            var first = share.ShareChallenge(880, 5, out var text);
            Assert.That(first, Is.EqualTo(ShareResult.Shared));
            Assert.That(text, Does.Contain("880"), "文案包含当期分数");
            Assert.That(text, Does.Contain("MW880"), "文案包含口令");
            Assert.That(text, Does.Not.Contain("必得"), "文案不做必得承诺（V2.19）");

            var second = share.ShareChallenge(900, 6, out _);
            Assert.That(second, Is.EqualTo(ShareResult.Cooldown), "V2.19：60s 冷却");

            _context.Clock.Advance(TimeSpan.FromSeconds(61));
            Assert.That(share.ShareChallenge(900, 6, out _), Is.EqualTo(ShareResult.Shared));
        }

        [Test]
        public void RoundFinished_AccumulatesGamesAndBestScore()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var economy = _context.CreateEconomy();

            economy.OnRoundFinished(300, 4);
            economy.OnRoundFinished(120, 9);

            Assert.That(_context.Save.Data.gamesPlayed, Is.EqualTo(2));
            Assert.That(_context.Save.Data.bestScore, Is.EqualTo(300), "最高分只增不减");
            Assert.That(_context.Save.Data.bestCombo, Is.EqualTo(9));
        }
    }
}
