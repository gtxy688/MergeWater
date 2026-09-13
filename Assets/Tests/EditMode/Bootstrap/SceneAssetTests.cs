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
    /// 前置：`Assets/Scenes/Main.unity` 必须在场（UI 全在该场景里手工维护，没有生成器）。
    /// </summary>
    public sealed class SceneAssetTests
    {
        private Scene _scene;
        private bool _opened;

        [SetUp]
        public void SetUp()
        {
            Assert.That(File.Exists(MergeWater.Editor.MainSceneAsset.Path), Is.True,
                "缺少主场景资产 Assets/Scenes/Main.unity（UI 全部在该场景里手工维护，不要删除或改名）");

            _scene = EditorSceneManager.OpenScene(MergeWater.Editor.MainSceneAsset.Path, OpenSceneMode.Additive);
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
            // 需求方（2026-09-12）：阶段目标/进度条、NEXT 预览、顶栏连击提示都已移除。
            // 这些字段已从 HudView 中删除（不再是「赋值为 null」，而是根本没有这个 API），
            // 因此这里改为断言字段确实不存在，防止有人把它们加回来。
            Assert.That(typeof(HudView).GetField("stageProgressFill"), Is.Null, "阶段目标进度条已移除");
            Assert.That(typeof(HudView).GetField("stageProgressLabel"), Is.Null, "阶段目标文字已移除");
            Assert.That(typeof(HudView).GetField("nextFruitIcon"), Is.Null, "NEXT 预览已移除");
            Assert.That(typeof(HudView).GetField("nextFruitLabel"), Is.Null, "NEXT 文字已移除");
            Assert.That(typeof(HudView).GetField("comboText"), Is.Null, "顶栏连击提示已移到合成位置");
            Assert.That(view.settingsButton, Is.Not.Null, "设置齿轮");
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
            // 2026-09-12 需求方反馈「音量条丢失」：设置页必须带 音效/音乐 两条音量条。
            Assert.That(view.sfxSlider, Is.Not.Null, "设置页应有音效音量条");
            Assert.That(view.musicSlider, Is.Not.Null, "设置页应有音乐音量条");
            Assert.That(view.sfxVolumeFill, Is.Not.Null, "音效音量条应有分段填充");
            Assert.That(view.musicVolumeFill, Is.Not.Null, "音乐音量条应有分段填充");
            Assert.That(view.sfxSlider.minValue, Is.EqualTo(0f).Within(0.001f));
            Assert.That(view.sfxSlider.maxValue, Is.EqualTo(1f).Within(0.001f));
            Assert.That(view.musicSlider.maxValue, Is.EqualTo(1f).Within(0.001f));
            Assert.That(view.leaderboardPanel, Is.Not.Null);
            Assert.That(view.toastText, Is.Not.Null);

            // 两侧入口齐备（GDD §6.2）
            Assert.That(view.shakeButton, Is.Not.Null, "左侧：摇一摇");
            Assert.That(view.hammerButton, Is.Not.Null, "左侧：锤子");
            Assert.That(view.giftButton, Is.Not.Null, "左侧：大礼包");
            Assert.That(view.undoButton, Is.Not.Null, "右侧：撤销");
            Assert.That(view.bombButton, Is.Not.Null, "右侧：炸弹");
        }

        /// <summary>
        /// 对局背景：`Backdrop` 节点必须在场（世界空间 SpriteRenderer），排序值必须低于**所有**对局元素。
        ///
        /// 背景一旦排到水果/预览线之前，玩家就看不到玩法画面了——这条断言是该缺陷的门禁。
        /// 2026-09-12 需求方先要求换成美术包背景、随后要求换回原来的纯色底：因此**当前不指派素材**
        /// （渲染器关闭、由相机清屏色兜底），这里改为断言「节点在 + 排序安全」，不再要求 sprite 非空。
        /// 想重新启用背景图时，在场景里选中 `GameRoot/Presentation/Backdrop`、给它的 SpriteRenderer
        /// 指派 Sprite 并勾上渲染器即可，本条断言依然成立。
        /// </summary>
        [Test]
        public void Backdrop_IsBehindEveryWorldElement()
        {
            var backdrop = InScene<BackdropView>().SingleOrDefault();
            Assert.That(backdrop, Is.Not.Null, "主场景应有对局背景节点（世界空间排序安全位）");
            Assert.That(backdrop.Renderer, Is.Not.Null, "背景应有 SpriteRenderer");
            Assert.That(backdrop.Renderer.sortingOrder, Is.LessThan(0),
                "背景排序值必须为负，否则会排到水果（100+）之上");
            Assert.That(backdrop.Renderer.sprite == null || backdrop.Renderer.enabled,
                "指派了素材时渲染器必须启用（素材存在却被关掉 = 背景静默失效）");

            var backdropOrder = backdrop.Renderer.sortingOrder;

            var spriteOrders = InScene<SpriteRenderer>()
                .Where(renderer => renderer != backdrop.Renderer)
                .Select(renderer => renderer.sortingOrder);

            var lineOrders = InScene<LineRenderer>().Select(line => line.sortingOrder);

            var foreground = spriteOrders.Concat(lineOrders).ToArray();
            Assert.That(foreground, Is.Not.Empty, "场景里应能检查到对局元素（水果/线/粒子）");

            foreach (var order in foreground)
            {
                Assert.That(backdropOrder, Is.LessThan(order),
                    $"背景排序值 {backdropOrder} 必须低于所有对局元素（发现 {order}），否则会挡在玩法画面前");
            }
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
            var path = MergeWater.Editor.MainSceneAsset.Path;
            var scenes = EditorBuildSettings.scenes;

            Assert.That(scenes.Any(scene => scene.path == path && scene.enabled), Is.True,
                "主场景应写入构建场景列表并启用");
        }

        /// <summary>
        /// 需求方（2026-09-13）：合成音改用真实素材 `Assets/Audios/pop.ogg`。这里是它的门禁——
        /// 素材路径写错、或有人在场景里把槽位清空，音效会**静默**退回占位音（无声，且 Console 干干净净），
        /// 只有真机试玩才发现。断言两件事：① 指派的素材在工程 `Assets/Audios/` 内且是音频文件；
        /// ② `Merge` 与 `ComboUp` 都必须有真实素材（连击复用同一素材、靠 pitch 变调）。
        /// </summary>
        [Test]
        public void AudioDirector_MergeSfx_IsAssignedFromProjectAudioAssets()
        {
            var audio = InScene<AudioDirector>().Single();

            foreach (var id in new[] { SfxId.Merge, SfxId.ComboUp })
            {
                Assert.That(audio.HasClip(id), Is.True,
                    $"{id} 应有真实素材；为空 = 静默退回运行时占位音（玩家听到的是合成音，不是 pop.ogg）");

                var clip = audio.GetClip(id);
                var path = AssetDatabase.GetAssetPath(clip);

                Assert.That(path, Does.StartWith("Assets/Audios/").And.EndsWith(".ogg"),
                    $"{id} 应指向 Assets/Audios 下的 .ogg 素材，实际是「{path}」");
            }
        }

        [Test]
        public void MainScene_HasNoReferencesToMissingScripts()
        {
            // 指向「已不存在的脚本资产」的引用在编辑器里显示为 Missing Script：运行时该组件根本不会
            // 实例化，功能静默失效——而且序列化数据看起来完全正常，肉眼看场景也未必发现。
            // 2026-09-13 的飘字池正是这样坏的：`FloatingTextItem.cs` 从共享文件拆成独立文件后，
            // 场景里 12 个飘字条目仍指向拆分前的旧脚本 GUID，`FloatingTextSpawner.Spawn`
            // 遇到 null 条目直接 return，合成时再也不出现飘字（由 PlayMode 手感测试才暴露出来）。
            // 注意两种形态都要查：既没有 guid 的裸引用，以及**有 guid 但该 guid 不对应任何脚本**。
            var sceneText = File.ReadAllText(MergeWater.Editor.MainSceneAsset.Path);

            var referenced = System.Text.RegularExpressions.Regex
                .Matches(sceneText, @"m_Script: \{fileID: -?\d+(?:, guid: ([0-9a-f]+))?")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .ToArray();

            var missing = referenced
                .Where(guid => string.IsNullOrEmpty(guid) ||
                               !AssetDatabase.GUIDToAssetPath(guid).EndsWith(".cs"))
                .ToArray();

            Assert.That(missing, Is.Empty,
                "主场景里有引用不到脚本资产的组件（Missing Script，功能会静默失效）：" +
                string.Join(", ", missing.Select(g => string.IsNullOrEmpty(g) ? "(无 guid)" : g)));
        }
    }
}
