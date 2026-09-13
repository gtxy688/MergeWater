using System.Collections.Generic;
using System.IO;
using System.Linq;
using MergeWater.Aim;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Field;
using MergeWater.Meta;
using MergeWater.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeWater.Editor
{
    /// <summary>
    /// 生成可运行的主场景 <c>Assets/Scenes/Main.unity</c>：相机、场地、GameRoot 与表现层，
    /// 并写入构建场景列表。UI 由 <see cref="HudBuilder"/> 在**编辑期**一次性生成成场景里的实际对象，
    /// 运行时不再生成任何界面（因此生成后可直接在编辑器里选中调整）。
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        private const float CameraOrthographicSize = 5.6f;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0.4f, -10f);

        [MenuItem("MergeWater/Build Main Scene", priority = 3)]
        public static void BuildMenu()
        {
            // 整场重建会丢弃场景里的一切手工调整（UI 位置/颜色/文字等都已序列化进 Main.unity）。
            // 因此先确认；只想重建 UI 请用 `MergeWater/Rebuild UI In Open Scene`。
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("MergeWater",
                    $"将重建整个场景，场景里对 UI 的手工调整会全部丢失：\n{ScenePath}\n\n" +
                    "只想重建界面请改用菜单「MergeWater/Rebuild UI In Open Scene」。\n\n确定继续？",
                    "重建整个场景", "取消"))
                return;

            Build(setAsBuildScene: true);
            EditorUtility.DisplayDialog("MergeWater", $"已生成场景：{ScenePath}", "好的");
        }

        /// <summary>
        /// 只重建当前已打开主场景里的 UI（`GameRoot/Presentation` 子树），场景其余部分与
        /// 其它手工调整全部保留。用于「改了 <see cref="HudBuilder"/> 的布局后局部刷新」。
        /// </summary>
        [MenuItem("MergeWater/Rebuild UI In Open Scene", priority = 4)]
        public static void RebuildUiMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("MergeWater",
                    $"请先打开主场景：{ScenePath}\n（当前：{(string.IsNullOrEmpty(scene.path) ? "未保存的场景" : scene.path)}）",
                    "好的");
                return;
            }

            if (!EditorUtility.DisplayDialog("MergeWater",
                    "将删除并重建该场景里的 UI（GameRoot/Presentation），场景其它内容保留。\n\n确定继续？",
                    "重建 UI", "取消"))
                return;

            RebuildUiInOpenScene(scene);
            EditorUtility.DisplayDialog("MergeWater", "已重建 UI。", "好的");
        }

        /// <summary>
        /// 只重建 <c>GameRoot/Presentation</c>：删掉旧子树、按 <see cref="HudBuilder"/> 重新生成，
        /// 并把 <see cref="GameBootstrapper"/> 上的表现层引用重新接好。相机、场地、GameRoot 等一律不动。
        /// </summary>
        public static void RebuildUiInOpenScene(Scene scene)
        {
            var gameRoot = FindRoot(scene, "GameRoot");
            if (gameRoot == null)
            {
                Debug.LogError($"[SceneBuilder] 场景里找不到 GameRoot，无法只重建 UI。请用 MergeWater/Build Main Scene。");
                return;
            }

            var bootstrapper = gameRoot.GetComponentInChildren<GameBootstrapper>(true);
            if (bootstrapper == null)
            {
                Debug.LogError("[SceneBuilder] GameRoot 下找不到 GameBootstrapper，无法只重建 UI。");
                return;
            }

            // 旧 UI 连同其上的手工调整一起删除——这是本菜单的语义（重建界面）。
            var existing = gameRoot.transform.Find("Presentation");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.FruitCirclePath);
            var arrow = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.ArrowPath);
            var view = HudBuilder.Build(gameRoot.transform, circle, arrow, out var presentationRoot);
            var tutorial = gameRoot.GetComponentInChildren<TutorialDirector>(true);
            if (tutorial != null)
                tutorial.Configure(presentationRoot.GetComponent<PanelController>(), view.tutorialArrow);

            // 表现层引用是序列化字段，重建后必须重新指向新对象（否则运行时表现为「UI 不响应」）。
            var serialized = new SerializedObject(bootstrapper);
            serialized.FindProperty("hud").objectReferenceValue = presentationRoot.GetComponent<HudBinder>();
            serialized.FindProperty("panels").objectReferenceValue = presentationRoot.GetComponent<PanelController>();
            serialized.FindProperty("audio").objectReferenceValue = presentationRoot.GetComponent<AudioDirector>();
            serialized.FindProperty("feedback").objectReferenceValue = presentationRoot.GetComponent<FeedbackDirector>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneBuilder] 已只重建 UI：{ScenePath}");
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);

        /// <summary>批量模式入口（不弹窗）：生成占位美术、配置资产与主场景，并写入构建列表。</summary>
        public static void BuildFromCommandLine() => Build(setAsBuildScene: true);

        public static void Build(bool setAsBuildScene)
        {
            PlaceholderArtGenerator.GenerateAll();
            ConfigAssetGenerator.GenerateAll(force: false);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera();
            var root = new GameObject("GameRoot");

            var field = BuildField(root.transform);
            var aim = root.AddComponent<AimController>();
            aim.SetCamera(camera);

            var items = root.AddComponent<ItemUseController>();
            var tutorial = root.AddComponent<TutorialDirector>();
            var bootstrapper = root.AddComponent<GameBootstrapper>();

            var view = BuildPresentation(root.transform, out var panels, out var hud, out var feedback, out var audio);
            tutorial.Configure(panels, view != null ? view.tutorialArrow : null);

            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(ConfigAssetGenerator.BalancePath);
            var settings = AssetDatabase.LoadAssetAtPath<MetaSettings>(ConfigAssetGenerator.MetaSettingsPath);

            bootstrapper.ConfigureReferences(field, aim, hud, panels, audio, feedback, tutorial, items, balance,
                settings, camera);

            field.Configure(balance != null ? balance.ToBalance() : GameBalance.CreateDefault(),
                AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.FruitCirclePath), null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            if (setAsBuildScene)
                SetAsBuildScene(ScenePath);

            Debug.Log($"[SceneBuilder] 已生成 {ScenePath}");
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HudBuilder.BackgroundColor;
            camera.transform.position = CameraPosition;
            go.AddComponent<AudioListener>();

            return camera;
        }

        private static GameField BuildField(Transform root)
        {
            var fieldGo = new GameObject("Field");
            fieldGo.transform.SetParent(root, false);
            fieldGo.transform.position = Vector3.zero;

            return fieldGo.AddComponent<GameField>();
        }

        private static HudView BuildPresentation(Transform root, out PanelController panels, out HudBinder hud,
            out FeedbackDirector feedback, out AudioDirector audio)
        {
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.FruitCirclePath);
            var arrow = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.ArrowPath);

            var view = HudBuilder.Build(root, circle, arrow, out var presentationRoot);

            panels = presentationRoot.GetComponent<PanelController>();
            hud = presentationRoot.GetComponent<HudBinder>();
            feedback = presentationRoot.GetComponent<FeedbackDirector>();
            audio = presentationRoot.GetComponent<AudioDirector>();

            return view;
        }

        private static void SetAsBuildScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.All(scene => scene.path != path))
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
