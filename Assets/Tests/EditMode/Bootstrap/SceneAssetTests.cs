using System.IO;
using System.Linq;
using MergeWater.Aim;
using MergeWater.Bootstrap;
using MergeWater.Field;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// M7 验收 A1：主场景确实装配了必需的组件与引用（不是只检查文件存在）。
    /// 前置：先运行 `MergeWater/Build Main Scene`。
    /// </summary>
    public sealed class SceneAssetTests
    {
        private Scene _scene;
        private bool _opened;

        [SetUp]
        public void SetUp()
        {
            Assert.That(File.Exists(MergeWater.Editor.SceneBuilder.ScenePath), Is.True,
                "缺少主场景，请先运行 MergeWater/Build Main Scene");

            _scene = EditorSceneManager.OpenScene(MergeWater.Editor.SceneBuilder.ScenePath, OpenSceneMode.Additive);
            _opened = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (!_opened)
                return;

            EditorSceneManager.CloseScene(_scene, true);
            _opened = false;
        }

        private T[] InScene<T>() where T : Component =>
            Object.FindObjectsOfType<T>(true).Where(component => component.gameObject.scene == _scene).ToArray();

        [Test]
        public void MainScene_ContainsBootstrapperFieldAimCanvasAndPresentation()
        {
            Assert.That(InScene<GameBootstrapper>().Length, Is.EqualTo(1), "应有唯一 GameBootstrapper");
            Assert.That(InScene<GameField>().Length, Is.EqualTo(1), "应有 GameField");
            Assert.That(InScene<AimController>().Length, Is.EqualTo(1), "应有 AimController");
            Assert.That(InScene<ItemUseController>().Length, Is.EqualTo(1), "应有 ItemUseController");
            Assert.That(InScene<TutorialDirector>().Length, Is.EqualTo(1), "应有 TutorialDirector");

            Assert.That(InScene<Canvas>().Length, Is.GreaterThanOrEqualTo(1), "应有 HUD Canvas");
            Assert.That(InScene<HudView>().Length, Is.EqualTo(1), "应有 HudView");
            Assert.That(InScene<PanelController>().Length, Is.EqualTo(1), "应有 PanelController");
            Assert.That(InScene<HudBinder>().Length, Is.EqualTo(1), "应有 HudBinder");
            Assert.That(InScene<FeedbackDirector>().Length, Is.EqualTo(1), "应有 FeedbackDirector");
            Assert.That(InScene<AudioDirector>().Length, Is.EqualTo(1), "应有 AudioDirector");
            Assert.That(InScene<DangerLineView>().Length, Is.EqualTo(1), "应有警戒线");
            Assert.That(InScene<AimPreviewView>().Length, Is.EqualTo(1), "应有瞄准预览");
            Assert.That(InScene<EventSystem>().Length, Is.EqualTo(1), "按钮需要 EventSystem");

            Assert.That(InScene<Camera>().Any(camera => camera.orthographic), Is.True, "主相机应为正交 2D");
        }

        [Test]
        public void Bootstrapper_SerializedReferencesAreWired()
        {
            var bootstrapper = InScene<GameBootstrapper>().Single();
            var serialized = new SerializedObject(bootstrapper);

            foreach (var fieldName in new[]
                     { "field", "aim", "hud", "panels", "audio", "feedback", "tutorial", "items", "balanceAsset",
                       "metaSettings", "targetCamera" })
            {
                var property = serialized.FindProperty(fieldName);
                Assert.That(property, Is.Not.Null, $"GameBootstrapper 应有序列化字段 {fieldName}");
                Assert.That(property.objectReferenceValue, Is.Not.Null, $"GameBootstrapper.{fieldName} 应已装配");
            }
        }

        [Test]
        public void HudView_RequiredElementsAreWired()
        {
            var view = InScene<HudView>().Single();

            Assert.That(view.scoreText, Is.Not.Null, "顶栏当前分");
            Assert.That(view.bestScoreText, Is.Not.Null, "顶栏最高分");
            Assert.That(view.stageProgressFill, Is.Not.Null, "阶段目标进度条");
            Assert.That(view.settingsButton, Is.Not.Null, "设置齿轮");
            Assert.That(view.nextFruitIcon, Is.Not.Null, "next 预览");
            Assert.That(view.privacyPanel, Is.Not.Null, "隐私弹窗（R20）");
            Assert.That(view.privacyAcceptButton, Is.Not.Null);
            Assert.That(view.settlementPanel, Is.Not.Null);
            Assert.That(view.retryButton, Is.Not.Null);
            Assert.That(view.reviveButton, Is.Not.Null);
            Assert.That(view.shareButton, Is.Not.Null);
            Assert.That(view.settingsPanel, Is.Not.Null);
            Assert.That(view.sfxToggle, Is.Not.Null);
            Assert.That(view.musicToggle, Is.Not.Null);
            Assert.That(view.vibrateToggle, Is.Not.Null);
            Assert.That(view.leaderboardPanel, Is.Not.Null);
            Assert.That(view.toastText, Is.Not.Null);

            // 两侧入口齐备（GDD §6.2）
            Assert.That(view.shakeButton, Is.Not.Null, "左侧：摇一摇");
            Assert.That(view.hammerButton, Is.Not.Null, "左侧：锤子");
            Assert.That(view.giftButton, Is.Not.Null, "左侧：大礼包");
            Assert.That(view.undoButton, Is.Not.Null, "右侧：撤销");
            Assert.That(view.bombButton, Is.Not.Null, "右侧：炸弹");
        }

        [Test]
        public void MainScene_HasNoBottomBannerOrCrossPromotionSlots()
        {
            // R27：底部不放 Banner / 信息流 / 交叉推广位。
            var suspicious = InScene<Transform>()
                .Where(t => t.name.IndexOf("banner", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || t.name.Contains("信息流")
                            || t.name.Contains("交叉")
                            || t.name.Contains("推广"))
                .Select(t => t.name)
                .ToArray();

            Assert.That(suspicious, Is.Empty, $"不应存在广告或推广位节点：{string.Join(", ", suspicious)}");
        }

        [Test]
        public void MainScene_IsInBuildSettings()
        {
            var path = MergeWater.Editor.SceneBuilder.ScenePath;
            var scenes = EditorBuildSettings.scenes;

            Assert.That(scenes.Any(scene => scene.path == path && scene.enabled), Is.True,
                "主场景应写入构建场景列表并启用");
        }
    }
}
