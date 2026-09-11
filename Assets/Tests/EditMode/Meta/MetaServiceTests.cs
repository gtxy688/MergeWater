using System.Collections.Generic;
using MergeWater.Core;
using MergeWater.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M6 验收 A10–A12 与 R20/R21/R25：排行、隐私门、埋点与设置。</summary>
    public sealed class MetaServiceTests
    {
        private MetaTestContext _context;

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            _context = null;
        }

        [Test]
        public void Leaderboard_Submit_OrdersByScore_AndGetTopRespectsPageSize()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var board = new LeaderboardService(_context.Save.Data);

            board.Submit("A", 300);
            board.Submit("B", 900);
            board.Submit("C", 600);
            board.Submit(LeaderboardService.SelfName, 700);

            var top = board.GetTop(3);
            Assert.That(top.Count, Is.EqualTo(3));
            Assert.That(top[0].Score, Is.EqualTo(900));
            Assert.That(top[1].Score, Is.EqualTo(700));
            Assert.That(top[1].Name, Is.EqualTo(LeaderboardService.SelfName));
            Assert.That(top[1].IsSelf, Is.True);
            Assert.That(top[2].Score, Is.EqualTo(600));

            var page2 = board.GetTop(3, 2);
            Assert.That(page2.Count, Is.EqualTo(1));
            Assert.That(page2[0].Score, Is.EqualTo(300), "每页 20 条语义下的第二页");
        }

        [Test]
        public void Leaderboard_Submit_DoesNotLowerExistingScore_AndIgnoresZero()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var board = new LeaderboardService(_context.Save.Data);

            board.Submit(LeaderboardService.SelfName, 500);
            board.Submit(LeaderboardService.SelfName, 200);
            Assert.That(board.SelfBestScore(), Is.EqualTo(500), "低分不覆盖高分");

            board.Submit(LeaderboardService.SelfName, 0);
            board.Submit("", 0);
            Assert.That(board.Count, Is.EqualTo(1), "0 分不写入排行，避免污染");

            board.Submit(LeaderboardService.SelfName, 800);
            Assert.That(board.SelfBestScore(), Is.EqualTo(800), "破纪录后第一名更新");
            Assert.That(board.GetTop(1)[0].Score, Is.EqualTo(800));
        }

        [Test]
        public void Leaderboard_IsSelfBeaten_OnlyWhenFriendScoreHigher()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var board = new LeaderboardService(_context.Save.Data);

            board.Submit(LeaderboardService.SelfName, 500);
            Assert.That(board.IsSelfBeaten, Is.False, "本地单机时未被超越（R21 在 V1 接入好友数据后生效）");

            board.Submit("好友", 600);
            Assert.That(board.IsSelfBeaten, Is.True);

            board.Submit("好友", 100);
            Assert.That(board.GetTop(1)[0].Score, Is.EqualTo(600), "好友记录同样只增不减");
        }

        [Test]
        public void Leaderboard_TrimsToMaxEntries()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var board = new LeaderboardService(_context.Save.Data, maxEntries: 5);

            for (var i = 0; i < 20; i++)
                board.Submit($"P{i}", 100 + i);

            Assert.That(board.Count, Is.EqualTo(5));
            Assert.That(board.GetTop(5)[0].Score, Is.EqualTo(119));
        }

        [Test]
        public void PrivacyGate_BeforeAccept_Blocks_AfterAccept_Persists()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var gate = new PrivacyGate(_context.Save);

            Assert.That(gate.IsAccepted, Is.False, "首启默认未同意");

            gate.Accept();
            Assert.That(gate.IsAccepted, Is.True);

            var reloaded = new SaveService(_context.Store, _context.Clock).Load();
            Assert.That(reloaded.privacyAccepted, Is.True, "同意状态应持久化");
        }

        [Test]
        public void Settings_Persist_AndClearCacheResetsEverything()
        {
            _context = new MetaTestContext();
            _context.Save.Load();
            var settings = new SettingsService(_context.Save);

            Assert.That(settings.SfxEnabled, Is.True);
            settings.SetSfx(false);
            settings.SetMusic(false);
            settings.SetVibrate(false);

            var reloaded = new SaveService(_context.Store, _context.Clock).Load();
            Assert.That(reloaded.settingsSfx, Is.False, "开关应持久化（R25）");
            Assert.That(reloaded.settingsMusic, Is.False);
            Assert.That(reloaded.settingsVibrate, Is.False);

            var privacy = new PrivacyGate(_context.Save);
            privacy.Accept();

            settings.ClearCache();

            Assert.That(settings.SfxEnabled, Is.True, "清除缓存回到默认值");
            Assert.That(new PrivacyGate(_context.Save).IsAccepted, Is.False, "清除缓存后需重新弹隐私政策（R20）");
        }

        [Test]
        public void Analytics_BeforeInitialize_IsIgnored()
        {
            var sink = new InMemoryAnalyticsSink();
            var analytics = new AnalyticsService(sink, new FakeClock());

            Assert.That(analytics.IsInitialized, Is.False);
            analytics.Track(AnalyticsEventNames.AppLaunch);

            Assert.That(sink.Records, Is.Empty, "同意隐私前不上报（R20）");
        }

        [Test]
        public void Analytics_AfterInitialize_RecordsNameAndParameters()
        {
            var sink = new InMemoryAnalyticsSink();
            var analytics = new AnalyticsService(sink, new FakeClock());

            analytics.Initialize();
            analytics.Track(AnalyticsEventNames.Merge, AnalyticsParam.Int("tier", 4), AnalyticsParam.Int("combo", 3));

            Assert.That(sink.CountOf(AnalyticsEventNames.Merge), Is.EqualTo(1));
            var record = sink.Records[0];
            Assert.That(record.Parameters.Count, Is.EqualTo(2));
            Assert.That(record.Parameters[0].Key, Is.EqualTo("tier"));
            Assert.That(record.Parameters[0].Value, Is.EqualTo("4"));
        }

        [Test]
        public void Analytics_WhenSinkThrows_DoesNotThrow()
        {
            var sink = new InMemoryAnalyticsSink { ThrowOnWrite = true };
            var analytics = new AnalyticsService(sink, new FakeClock());
            analytics.Initialize();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Sink 写入失败"));

            Assert.DoesNotThrow(() => analytics.Track(AnalyticsEventNames.RoundEnd));
            Assert.DoesNotThrow(() => analytics.Track(AnalyticsEventNames.RoundEnd), "错误只告警一次，不重复抛出");
        }

        [Test]
        public void NullAnalytics_NeverInitializesOrRecords()
        {
            IAnalyticsService analytics = new NullAnalyticsService();

            analytics.Initialize();
            analytics.Track(AnalyticsEventNames.AppLaunch);

            Assert.That(analytics.IsInitialized, Is.False);
        }

        [Test]
        public void AdsServiceProxy_BeforeSwap_BehavesAsUnavailable()
        {
            var proxy = new AdsServiceProxy();
            Assert.That(proxy.IsAvailable, Is.False, "未同意隐私时广告代理不可用");

            RewardedResult result = RewardedResult.Completed;
            proxy.ShowRewarded(AdPlacement.Revive, r => result = r);
            Assert.That(result, Is.EqualTo(RewardedResult.Unavailable));

            proxy.Inner = new FakeAdsService();
            Assert.That(proxy.IsAvailable, Is.True, "同意后替换为真实/测试适配器");

            proxy.ShowRewarded(AdPlacement.Revive, r => result = r);
            Assert.That(result, Is.EqualTo(RewardedResult.Completed));
        }
    }
}
