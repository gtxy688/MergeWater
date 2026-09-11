using System;
using MergeWater.Core;
using MergeWater.Meta;

namespace MergeWater.Tests.EditMode
{
    /// <summary>可推进的假时钟，用于每日上限与冷却测试。</summary>
    internal sealed class FakeClock : IClock
    {
        public FakeClock(DateTime? start = null)
        {
            Now = start ?? new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Local);
        }

        public DateTime Now { get; set; }

        public string Today => Now.ToString("yyyy-MM-dd");

        public void Advance(TimeSpan delta) => Now += delta;
    }

    /// <summary>Meta 测试的公共装配：内存存档 + 假时钟 + 默认数值。</summary>
    internal sealed class MetaTestContext
    {
        public readonly InMemorySaveStore Store;
        public readonly FakeClock Clock;
        public readonly GameBalance Balance;
        public readonly SaveService Save;

        public MetaSettings Settings { get; private set; }

        public MetaTestContext(string initialJson = null, DateTime? now = null)
        {
            Store = new InMemorySaveStore(initialJson);
            Clock = new FakeClock(now);
            Balance = GameBalance.CreateDefault();
            Save = new SaveService(Store, Clock);
        }

        public EconomyService CreateEconomy(IAdsService ads = null, bool interstitialEnabled = true,
            bool offlineGrantAll = true)
        {
            UseSettings(interstitialEnabled, offlineGrantAll);
            return CreateEconomyWithCurrentSettings(ads);
        }

        /// <summary>替换开关组合（会释放上一个 ScriptableObject）。</summary>
        public void UseSettings(bool interstitialEnabled, bool offlineGrantAll)
        {
            if (Settings != null)
                UnityEngine.Object.DestroyImmediate(Settings);

            Settings = MetaSettings.CreateDefault();
            Settings.Configure(interstitialEnabled, offlineGrantAll);
        }

        public EconomyService CreateEconomyWithCurrentSettings(IAdsService ads = null) =>
            new EconomyService(Save, Clock, Balance, ads ?? new FakeAdsService(), Settings);

        public void Dispose()
        {
            if (Settings != null)
            {
                UnityEngine.Object.DestroyImmediate(Settings);
                Settings = null;
            }
        }
    }
}
