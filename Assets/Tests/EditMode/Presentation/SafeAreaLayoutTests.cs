using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 安全区适配的纯计算（2026-09-13 需求方真机截图：中间分数被刘海挡住、右上齿轮与微信胶囊重叠）。
    ///
    /// <para>这段数学决定了「顶栏要往下让多少」，算错就会在真机上要么继续被挡、要么在无刘海设备上
    /// 白白下沉。<see cref="Screen"/> 与 RectTransform 都在真实设备上才准确，所以这里只测与设备无关的
    /// 换算：屏幕高度、安全区、胶囊矩形全部作为参数传入。</para>
    /// </summary>
    public sealed class SafeAreaLayoutTests
    {
        private const float ScreenHeight = 1920f;
        private const float CanvasHeight = 1920f;

        private static Rect SafeAreaWithTopOcclusion(float topOcclusion) =>
            new Rect(0f, 0f, 1080f, ScreenHeight - topOcclusion);

        private static Rect Capsule(float topFromScreenTop, float bottomFromScreenTop, float width = 260f) =>
            new Rect(1080f - width, ScreenHeight - bottomFromScreenTop, width, bottomFromScreenTop - topFromScreenTop);

        [Test]
        public void RequiredTop_WithoutNotchOrCapsule_IsJustTheMargin()
        {
            var required = SafeAreaLayout.RequiredTopPixels(
                ScreenHeight, new Rect(0f, 0f, 1080f, ScreenHeight), false, default, marginPx: 16f);

            Assert.That(required, Is.EqualTo(16f).Within(1e-3f), "没有遮挡时就只剩间距");
        }

        [Test]
        public void RequiredTop_UsesSafeAreaTop_ForNotchedDevices()
        {
            // 刘海遮住顶部 120px
            var required = SafeAreaLayout.RequiredTopPixels(
                ScreenHeight, SafeAreaWithTopOcclusion(120f), false, default, marginPx: 16f);

            Assert.That(required, Is.EqualTo(136f).Within(1e-3f), "120 安全区 + 16 间距");
        }

        [Test]
        public void RequiredTop_TakesCapsule_WhenCapsuleIsLowerThanTheNotch()
        {
            // 胶囊下沿 200px，比刘海的 120px 更低 → 以胶囊为准（这是右上角齿轮必须避开的量）
            var required = SafeAreaLayout.RequiredTopPixels(
                ScreenHeight, SafeAreaWithTopOcclusion(120f), true, Capsule(100f, 200f), marginPx: 16f);

            Assert.That(required, Is.EqualTo(216f).Within(1e-3f), "取 200 与 120 的较大者，再加 16");
        }

        [Test]
        public void RequiredTop_KeepsNotch_WhenCapsuleIsHigher()
        {
            // 胶囊比刘海高：仍以刘海为准，不能因为胶囊浅就让元素钻进刘海
            var required = SafeAreaLayout.RequiredTopPixels(
                ScreenHeight, SafeAreaWithTopOcclusion(120f), true, Capsule(20f, 60f), marginPx: 16f);

            Assert.That(required, Is.EqualTo(136f).Within(1e-3f));
        }

        [Test]
        public void ExtraTop_WhenDesignAlreadyReservesEnough_IsZero()
        {
            // 设计稿已预留 216 设计单位，设备只要求 136 → 不上移也不下移
            var extra = SafeAreaLayout.ExtraTopPixels(requiredTopPx: 136f, currentTopPx: 216f);

            Assert.That(extra, Is.EqualTo(0f), "设备要求更低时不能把顶栏往上顶");
        }

        [Test]
        public void ExtraTop_WhenDeviceNeedsMore_IsTheDifference()
        {
            var extra = SafeAreaLayout.ExtraTopPixels(requiredTopPx: 300f, currentTopPx: 216f);

            Assert.That(extra, Is.EqualTo(84f).Within(1e-3f), "补差 = 设备要求 − 已预留");
        }

        [Test]
        public void PixelAndDesignUnitConversion_RoundTrips()
        {
            var design = SafeAreaLayout.PixelsToDesignUnits(96f, ScreenHeight, CanvasHeight);
            Assert.That(design, Is.EqualTo(96f).Within(1e-3f), "参考高度等于屏幕高度时 1:1");

            var back = SafeAreaLayout.DesignUnitsToPixels(design, ScreenHeight, CanvasHeight);
            Assert.That(back, Is.EqualTo(96f).Within(1e-3f), "逆变换应回到原值");

            // 屏幕比参考画布高（高分辨率真机）：同样的像素对应的设计单位更少
            var scaled = SafeAreaLayout.PixelsToDesignUnits(96f, screenHeightPx: 3840f, canvasHeightDesign: 1920f);
            Assert.That(scaled, Is.EqualTo(48f).Within(1e-3f));

            Assert.That(SafeAreaLayout.PixelsToDesignUnits(96f, 0f, CanvasHeight), Is.EqualTo(0f),
                "屏幕高度非法时返回 0，不产生 NaN");
        }
    }
}
