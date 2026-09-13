using System.Collections;
using System.IO;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Field;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// V2.42 回归：方案 B「屏幕即边框」下，运行时地面必须从屏幕底边**抬起**一段
    /// （<c>GameBalance.FloorScreenInset</c>），否则水果落地后紧贴最后一行像素、观感上像被下边缘切掉。
    ///
    /// 走真场景（相机与注入都在 <c>GameBootstrapper.Awake</c> 里完成），断言的是
    /// 「贴地水果的可见下沿与屏幕底之间确实留出了间隙」这一玩家可观察结果，而不是内部字段。
    /// 同时把该帧渲染成 `Logs/Diagnostics/ui-05-floor.png` 供人工复核。
    /// </summary>
    public sealed class PlayAreaFloorTests
    {
        private const string SceneName = "Main";
        private const int CaptureWidth = 1080;
        private const int CaptureHeight = 1920;

        private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "mwater_save.json");

        private Scene _scene;
        private bool _loaded;
        private bool _hadSave;
        private string _saveBackup;

        [SetUp]
        public void SetUp()
        {
            _hadSave = File.Exists(SavePath);
            if (_hadSave)
            {
                _saveBackup = File.ReadAllText(SavePath);
                File.Delete(SavePath);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                _loaded = false;
                yield return null;
            }

            if (_hadSave)
                File.WriteAllText(SavePath, _saveBackup);
            else if (File.Exists(SavePath))
                File.Delete(SavePath);

            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayFloor_SitsAboveTheScreenBottom_SoGroundFruitIsNotClipped()
        {
            LogAssert.ignoreFailingMessages = true;

            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, $"场景 {SceneName} 必须已加入构建场景列表");

            while (!load.isDone)
                yield return null;

            _scene = SceneManager.GetSceneByName(SceneName);
            _loaded = true;

            yield return null; // Awake（此处注入相机与场地边界）
            yield return null; // Start

            var bootstrapper = Object.FindObjectOfType<GameBootstrapper>();
            var field = Object.FindObjectOfType<GameField>();
            var camera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();

            Assert.That(bootstrapper, Is.Not.Null);
            Assert.That(field, Is.Not.Null);
            Assert.That(camera, Is.Not.Null, "真场景应有主相机");
            Assert.That(camera.orthographic, Is.True, "场地边界依赖正交相机");

            var screenBottom = camera.transform.position.y - camera.orthographicSize;
            var inset = bootstrapper.Context.Balance.FloorScreenInset;

            Assert.That(inset, Is.GreaterThan(0f),
                "V2.42：地面必须从屏幕底边抬起一段，否则贴地水果会被下边缘切掉");

            Assert.That(field.PlayFloorY, Is.EqualTo(screenBottom + inset).Within(1e-3f),
                "运行时地面高度应等于「屏幕底边 + FloorScreenInset」");

            // 玩家可观察结果：把一颗水果放到地面上，它的可见下沿与屏幕底之间必须留出间隙。
            Assert.That(field.SpawnAt(1, new Vector2(0f, field.PlayFloorY + 0.05f), out _), Is.True);

            // 等它落稳（地面就在脚下，稍等即可）。
            var settleDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < settleDeadline)
                yield return null;

            var fruit = Object.FindObjectOfType<FruitBody>();
            Assert.That(fruit, Is.Not.Null, "应已生成水果");

            var fruitBottom = fruit.Collider.bounds.min.y;
            Assert.That(fruitBottom, Is.GreaterThan(screenBottom),
                $"贴地水果下沿 y={fruitBottom:0.###} 不得低于屏幕底边 y={screenBottom:0.###}（否则看起来被切）");

            var visibleGap = fruitBottom - screenBottom;

            // 这条下限只用来拦住「回到贴死屏幕底」（inset = 0 → 间隙 ≈ 0，正是 V2.42 修的缺陷），
            // 不是审美门槛。审美上「该留多少」由需求方目视定档的 `floorScreenInset` 决定：
            // 2026-09-13 需求方用 `MergeWater/Floor Tuning (Play Mode)` 目视定档 0.06
            // （1080×1920 下约 11 px，相当于等级 1 水果直径的 ~18%），此前这里写死的 0.2
            // 是本测试自己估的保守值、没有需求依据，会把定档值误判为回归。
            Assert.That(visibleGap, Is.GreaterThanOrEqualTo(0.03f),
                $"贴地水果与屏幕底之间应留出可见间隙（≥0.03 世界单位，1080×1920 下约 5 px），" +
                $"实际仅 {visibleGap:0.###} 世界单位——为 0 说明地面又贴死了屏幕底（V2.42 回归）");

            CaptureWorld(camera, "ui-05-floor.png");
        }

        private static bool CanRender =>
            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>
        /// 把世界渲染成 PNG 供人工复核。批处理 `-nographics` 下 GfxDevice 为空，
        /// `Camera.Render()` 会 SIGSEGV（不可捕获），因此前置跳过并写标记文件
        /// （与 `PhysicsAndPreviewDiagnostics` 相同约定）。
        /// 这里只拍世界：Overlay 画布不经过相机，需要 UI 时用 `UiScreenshotDiagnostics`。
        /// </summary>
        private static void CaptureWorld(Camera camera, string fileName)
        {
            var directory = Path.Combine("Logs", "Diagnostics");
            Directory.CreateDirectory(directory);

            if (!CanRender)
            {
                File.WriteAllText(Path.Combine(directory, fileName + ".skipped.txt"),
                    "no graphics device (batch -nographics)");
                return;
            }

            var previous = camera.targetTexture;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24);

            try
            {
                camera.targetTexture = target;
                camera.Render();

                var active = RenderTexture.active;
                RenderTexture.active = target;

                var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                texture.Apply();

                RenderTexture.active = active;

                File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
                Object.Destroy(texture);
            }
            finally
            {
                camera.targetTexture = previous;
                Object.Destroy(target);
            }
        }
    }
}
