using System.Collections.Generic;
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

namespace MergeWater.Editor
{
    /// <summary>
    /// 生成可运行的主场景 <c>Assets/Scenes/Main.unity</c>：相机、场地、GameRoot 与表现层，
    /// 并写入构建场景列表。所有布局与控件都调用运行时共用的 <see cref="HudBuilder"/>，
    /// 避免编辑器与运行时两套实现漂移。
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        private const float CameraOrthographicSize = 5.6f;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0.4f, -10f);

        [MenuItem("MergeWater/Build Main Scene", priority = 3)]
        public static void BuildMenu()
        {
            Build(setAsBuildScene: true);
            EditorUtility.DisplayDialog("MergeWater", $"已生成场景：{ScenePath}", "好的");
        }

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
            camera.backgroundColor = new Color(0.99f, 0.93f, 0.84f);
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
