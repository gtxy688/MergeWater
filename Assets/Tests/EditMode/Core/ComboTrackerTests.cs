using MergeWater.Core;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M1 验收 A3/A4：连击窗口与倍率（V2.1–V2.3）。</summary>
    public sealed class ComboTrackerTests
    {
        private static ComboTracker CreateTracker() => new ComboTracker(GameBalance.CreateDefault());

        [Test]
        public void RegisterMerge_WithinWindow_IncrementsAndCapsMultiplier()
        {
            var tracker = CreateTracker();

            Assert.That(tracker.Combo, Is.EqualTo(0));
            Assert.That(tracker.Multiplier, Is.EqualTo(1f).Within(1e-5f));

            tracker.RegisterMerge();
            Assert.That(tracker.Combo, Is.EqualTo(1));
            Assert.That(tracker.Multiplier, Is.EqualTo(1.0f).Within(1e-5f), "1 连击倍率为 1.0");

            tracker.RegisterMerge();
            Assert.That(tracker.Combo, Is.EqualTo(2));
            Assert.That(tracker.Multiplier, Is.EqualTo(1.1f).Within(1e-5f), "2 连击倍率为 1.1");

            for (var i = 0; i < 30; i++)
                tracker.RegisterMerge();

            Assert.That(tracker.Multiplier, Is.EqualTo(2.0f).Within(1e-5f), "倍率封顶 2.0");
        }

        [Test]
        public void Advance_BeyondWindow_ResetsCombo()
        {
            var tracker = CreateTracker();
            tracker.RegisterMerge();
            tracker.RegisterMerge();
            Assert.That(tracker.Combo, Is.EqualTo(2));

            tracker.Advance(1.0f);
            tracker.Advance(1.0f);
            Assert.That(tracker.Combo, Is.EqualTo(2), "窗口内不应归零");

            tracker.Advance(1.01f);
            Assert.That(tracker.Combo, Is.EqualTo(0), "超过 3.0s 应归零");
            Assert.That(tracker.Multiplier, Is.EqualTo(1.0f).Within(1e-5f));
        }

        [Test]
        public void Advance_ExactlyAtWindowBoundary_DoesNotReset()
        {
            var tracker = CreateTracker();
            tracker.RegisterMerge();
            tracker.RegisterMerge();

            tracker.Advance(3.0f);

            Assert.That(tracker.Combo, Is.EqualTo(2), "恰好 3.0s 不归零（严格大于判断）");
        }

        [Test]
        public void RegisterMerge_AfterReset_RestartsFromOne()
        {
            var tracker = CreateTracker();
            tracker.RegisterMerge();
            tracker.RegisterMerge();
            tracker.Advance(4f);
            Assert.That(tracker.Combo, Is.EqualTo(0));

            tracker.RegisterMerge();
            Assert.That(tracker.Combo, Is.EqualTo(1));
            Assert.That(tracker.Multiplier, Is.EqualTo(1.0f).Within(1e-5f));
        }

        [Test]
        public void Advance_NonPositiveDelta_IsIgnored()
        {
            var tracker = CreateTracker();
            tracker.RegisterMerge();

            tracker.Advance(0f);
            tracker.Advance(-5f);

            Assert.That(tracker.Combo, Is.EqualTo(1), "非正 dt 不应推进窗口");
        }

        [Test]
        public void Reset_ClearsComboAndMultiplier()
        {
            var tracker = CreateTracker();
            tracker.RegisterMerge();
            tracker.RegisterMerge();

            tracker.Reset();

            Assert.That(tracker.Combo, Is.EqualTo(0));
            Assert.That(tracker.Multiplier, Is.EqualTo(1.0f).Within(1e-5f));
        }
    }
}
