using System.Collections;
using System.IO;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// UI 观感诊断：在真实场景里把各个面板渲染成 PNG（`Logs/Diagnostics/ui-*.png`），
    /// 供人工/多模态复核。UI 只能靠看图判断，这条测试就是「看得见」的那一环。
    ///
    /// 存在理由（2026-09-12）：需求方截图反馈「设置面板这 UI 是个啥」——九宫格素材按 PPU=1 导入，
    /// 边框被放大 100 倍后撑满整个 Rect，面板被拉伸成一个大白椭圆。修完必须能再看一眼确认。
    /// 注意：`-nographics` 下没有图形设备，`Camera.Render()` 会让进程 SIGSEGV，
    /// 因此这里前置检查并写出 `.skipped.txt`（与 <see cref="PhysicsAndPreviewDiagnostics"/> 相同约定）。
    /// </summary>
    public sealed class UiScreenshotDiagnostics
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
        public IEnumerator CapturePanels_ForVisualReview()
        {
            LogAssert.ignoreFailingMessages = true;

            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, $"场景 {SceneName} 必须已加入构建场景列表");

            while (!load.isDone)
                yield return null;

            _scene = SceneManager.GetSceneByName(SceneName);
            _loaded = true;

            yield return null; // Awake
            yield return null; // Start

            var bootstrapper = Object.FindObjectOfType<GameBootstrapper>();
            var panels = Object.FindObjectOfType<PanelController>();
            Assert.That(bootstrapper, Is.Not.Null);
            Assert.That(panels, Is.Not.Null, "真场景应有 PanelController");

            // 1) 加载页（V2.35）
            bootstrapper.BeginEntryFlow();
            yield return null;
            yield return null;
            CaptureUi("ui-01-loading.png");

            // 2) 设置面板（需求方反馈的那一个）
            panels.Show(PanelId.Settings);
            yield return null;
            CaptureUi("ui-02-settings.png");

            // 3) 隐私弹窗（同一套 CreatePanelRoot + 按钮）
            panels.Show(PanelId.Privacy);
            yield return null;
            CaptureUi("ui-03-privacy.png");

            // 4) 结算页（按钮最多、含禁用态）
            panels.SetSettlement("本局结束", 1234, 5090, "再来一局？", true, true);
            panels.Show(PanelId.Settlement);
            yield return null;
            CaptureUi("ui-04-settlement.png");
        }

        /// <summary>是否存在真实图形设备。批处理 -nographics 下 GfxDevice 为空，Camera.Render() 会崩溃。</summary>
        private static bool CanRender =>
            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>
        /// 把 Canvas 临时切到 ScreenSpaceCamera 并用独立相机渲染，从而把 UI 拍进 RenderTexture
        /// （Overlay 模式的 Canvas 不经过相机，无法直接 Render）。
        /// </summary>
        private static void CaptureUi(string fileName)
        {
            var directory = Path.Combine("Logs", "Diagnostics");
            Directory.CreateDirectory(directory);

            if (!CanRender)
            {
                File.WriteAllText(Path.Combine(directory, fileName + ".skipped.txt"),
                    "no graphics device (batch -nographics)");
                return;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
                return;

            var mainCamera = Camera.main;
            var cameraObject = new GameObject("UiCaptureCamera");
            cameraObject.transform.SetParent(canvas.transform, false);
            var camera = cameraObject.AddComponent<Camera>();

            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousPlane = canvas.planeDistance;
            var previousSortingOrder = canvas.sortingOrder;

            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24);

            try
            {
                camera.orthographic = true;
                camera.orthographicSize = CaptureHeight * 0.5f;
                camera.nearClipPlane = 1f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = mainCamera != null ? mainCamera.backgroundColor : new Color(0.9f, 0.86f, 0.78f);
                camera.cullingMask = 1 << canvas.gameObject.layer;
                camera.transform.localPosition = new Vector3(0f, 0f, -100f);
                camera.targetTexture = target;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                canvas.sortingOrder = 0;

                Canvas.ForceUpdateCanvases();
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
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlane;
                canvas.sortingOrder = previousSortingOrder;
                camera.targetTexture = null;
                Object.Destroy(target);
                Object.Destroy(cameraObject);
            }
        }
    }
}
