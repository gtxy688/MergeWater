using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 「对局背景必须铺满视口」的回归门禁（2026-09-12 需求方把米色底换成美术包背景图后新增）。
    ///
    /// 背景用世界空间 <see cref="SpriteRenderer"/> + cover 缩放实现：Canvas 是 ScreenSpaceOverlay，
    /// 任何满屏不透明 UI 底板都会盖住整个世界（见
    /// <c>HudLayoutTests.NoOpaqueFullScreenGraphic_HidesTheGameWorld</c>），所以背景只能放世界里。
    /// 若 cover 逻辑写错（例如按 fit 缩放、漏乘相机宽高比、只在一个方向上放大），竖屏/横屏下就会露出
    /// 相机清屏色的边带——这类缺陷普通逻辑测试抓不到，这里用相机参数直接算出应有的覆盖范围。
    /// </summary>
    public sealed class BackdropViewTests
    {
        private const float OrthographicSize = 5.6f;

        private GameObject _backdropObject;
        private GameObject _cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (_backdropObject != null)
                Object.DestroyImmediate(_backdropObject);

            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);

            _backdropObject = null;
            _cameraObject = null;
        }

        /// <summary>
        /// 背景图现在放在 <c>Assets/UI/Art</c>，不再走 <c>Resources</c>——`Resources` 下的素材会被
        /// 无条件打进包，而这里 28 张图里有 4 张没有任何引用（其中 `bg_night.png` 一张 5.0MB）。
        /// </summary>
        private static Sprite LoadBackdropSprite() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Art/bg_night.png");

        private (SpriteRenderer renderer, Camera camera) CreateBackdrop(float aspect, Sprite sprite)
        {
            _cameraObject = new GameObject("TestCamera");
            var camera = _cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.aspect = aspect;
            _cameraObject.transform.position = new Vector3(0f, 0.4f, -10f);

            _backdropObject = new GameObject("TestBackdrop");
            var renderer = _backdropObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            _backdropObject.AddComponent<BackdropView>().Configure(renderer, camera);

            return (renderer, camera);
        }

        [TestCase(1080f / 1920f, TestName = "Backdrop_CoversWholeCameraView_竖屏")]
        [TestCase(1920f / 1080f, TestName = "Backdrop_CoversWholeCameraView_横屏")]
        [TestCase(1f, TestName = "Backdrop_CoversWholeCameraView_正方形")]
        public void Backdrop_CoversWholeCameraView(float aspect)
        {
            var sprite = LoadBackdropSprite();
            Assert.That(sprite, Is.Not.Null, "缺少背景图 Assets/UI/Art/bg_night.png（美术包背景图是否已导入？）");

            var (renderer, camera) = CreateBackdrop(aspect, sprite);

            var viewHeight = OrthographicSize * 2f;
            var viewWidth = viewHeight * aspect;
            var bounds = renderer.bounds;

            Assert.That(bounds.size.y, Is.GreaterThanOrEqualTo(viewHeight),
                "背景高度不足：竖屏下会露出相机清屏色边带");
            Assert.That(bounds.size.x, Is.GreaterThanOrEqualTo(viewWidth),
                "背景宽度不足：横屏下会露出相机清屏色边带");

            Assert.That(bounds.center.x, Is.EqualTo(camera.transform.position.x).Within(0.001f), "背景应水平居中于相机");
            Assert.That(bounds.center.y, Is.EqualTo(camera.transform.position.y).Within(0.001f), "背景应垂直居中于相机");

            // cover 而不是「随便放大」：缩放值必须贴近理论 cover 比例（允许 overscan 级别的余量），
            // 否则要么浪费分辨率、要么在极端比例下盖不满。
            var spriteSize = sprite.bounds.size;
            var coverScale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
            var actualScale = renderer.transform.localScale.x;

            Assert.That(actualScale, Is.EqualTo(coverScale).Within(coverScale * 0.05f + 0.001f),
                "背景缩放应贴近 cover 比例（恰好盖满）");
        }

        [Test]
        public void Backdrop_RefitsWhenCameraAspectChanges()
        {
            var sprite = LoadBackdropSprite();
            Assert.That(sprite, Is.Not.Null, "缺少背景图 Assets/UI/Art/bg_night.png");

            var (renderer, camera) = CreateBackdrop(1080f / 1920f, sprite);
            var view = renderer.GetComponent<BackdropView>();
            var portraitScale = renderer.transform.localScale.x;

            // 超宽视口（例如平板横屏 2.2:1）需要比竖屏更大的横向覆盖比例。
            // 注意 16:9 视口与 16:9 素材的 cover 比例恰好等于竖屏值，不能用作「变大了」的判据。
            camera.aspect = 2.2f;
            view.Configure(renderer, camera);

            var viewHeight = OrthographicSize * 2f;
            var viewWidth = viewHeight * camera.aspect;

            Assert.That(renderer.transform.localScale.x, Is.GreaterThan(portraitScale),
                "超宽屏需要更大的横向覆盖比例，背景没有跟随宽高比变化重新贴合");
            Assert.That(renderer.bounds.size.x, Is.GreaterThanOrEqualTo(viewWidth), "重新贴合后仍不足以覆盖超宽视口");
            Assert.That(renderer.bounds.size.y, Is.GreaterThanOrEqualTo(viewHeight), "重新贴合后高度不足");
        }

        [Test]
        public void Backdrop_WithoutSprite_DoesNotThrow()
        {
            // 美术缺失时的降级路径：不缩放、不抛异常，由相机清屏色兜底（无素材时场景里
            // Backdrop 的 SpriteRenderer 是关闭状态）。
            var (renderer, camera) = CreateBackdrop(1080f / 1920f, null);
            var view = renderer.GetComponent<BackdropView>();

            Assert.DoesNotThrow(() => view.Configure(renderer, camera));
            Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one), "无素材时不应改动缩放");
        }
    }
}
