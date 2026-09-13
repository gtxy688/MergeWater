using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MergeWater.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeWater.Editor
{
    /// <summary>
    /// 就地把主场景里的 legacy uGUI `Text` 迁移成 TextMeshPro，并把面板换成 <see cref="UiPanel"/>。
    ///
    /// <para>为什么不直接用 Build Main Scene 重建：那会丢掉场景里所有手工调整（位置/颜色/字号等，
    /// 需求方已在编辑器里做过）。本工具只改**组件类型**与**引用**，RectTransform 上的布局数值一律不动。</para>
    ///
    /// <para>迁移步骤：① 每个 legacy Text 就地换成 TextMeshProUGUI，保留文字/字号/颜色/对齐/换行；
    /// ② 面板根节点挂 UiPanel（CanvasGroup 保留，alpha 设为 1 且 active=false，方便在编辑器里勾选预览）；
    /// ③ 按 `UiPanel`/TMP 的新类型重新把 HudView 的 54 个引用接好。</para>
    /// </summary>
    public static class UiTmpMigrator
    {
        [MenuItem("MergeWater/Migrate UI To TMP (In Open Scene)", priority = 5)]
        public static void MigrateMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != SceneBuilder.ScenePath)
            {
                EditorUtility.DisplayDialog("MergeWater",
                    $"请先打开主场景：{SceneBuilder.ScenePath}", "好的");
                return;
            }

            if (!EditorUtility.DisplayDialog("MergeWater",
                    "将把场景里的 legacy Text 换成 TextMeshPro，并给面板挂 UiPanel。\n" +
                    "布局数值不会被改动。\n\n确定继续？", "迁移", "取消"))
                return;

            var report = Migrate(scene);
            EditorUtility.DisplayDialog("MergeWater", report, "好的");
        }

        public static void MigrateFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            var report = Migrate(scene);
            Debug.Log("[UiTmpMigrator] " + report);
            EditorApplication.Exit(0);
        }

        private static string Migrate(Scene scene)
        {
            var font = HudBuilder.LoadFontAsset();
            if (font == null)
                return $"找不到字体资产 {HudBuilder.FontAssetPath}；先运行 MergeWater/Font/2. 烘焙中文 TMP 字体资产。";

            var report = new StringBuilder();

            // ── 1. legacy Text -> TextMeshProUGUI（保留全部视觉参数） ──
            var legacyTexts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Text>(true))
                .ToArray();

            foreach (var old in legacyTexts)
            {
                var go = old.gameObject;
                var text = old.text;
                var fontSize = old.fontSize;
                var color = old.color;
                var alignment = ToTmp(old.alignment);
                var wrap = old.horizontalOverflow == HorizontalWrapMode.Wrap;
                var rect = old.rectTransform;

                Object.DestroyImmediate(old, true);

                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = text;
                tmp.font = font;
                tmp.fontSize = fontSize;
                tmp.color = color;
                tmp.alignment = alignment;
                tmp.enableWordWrapping = wrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
                // 保留原有 RectTransform 数值（迁移不改布局）
                if (rect != null)
                    tmp.rectTransform.sizeDelta = rect.sizeDelta;
            }

            report.Append($"legacy Text -> TMP：{legacyTexts.Length} 个\n");

            // ── 2. 面板根节点挂 UiPanel ──
            var hudView = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HudView>(true))
                .FirstOrDefault();

            if (hudView == null)
                return report.Append("找不到 HudView。").ToString();

            var panelNames = new[] { "PrivacyPanel", "SettlementPanel", "SettingsPanel",
                "LeaderboardPanel", "AdOverlay", "Toast", "LoadingPanel" };
            var panelCount = 0;

            foreach (var name in panelNames)
            {
                var tr = FindDeep(hudView.transform, name);
                if (tr == null)
                    continue;

                var group = tr.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    // alpha 归 1：否则在编辑器里勾上 active 也看不见（需求方实际踩到的坑）。
                    group.alpha = 1f;
                }

                if (tr.GetComponent<UiPanel>() == null)
                    tr.gameObject.AddComponent<UiPanel>();

                panelCount++;
            }

            report.Append($"面板挂 UiPanel：{panelCount} 个（alpha 已设为 1）\n");

            // ── 2b. 飘字池：运行时不生成 UI，飘字条目必须是场景里预置的对象 ──
            var poolCount = EnsureFloatingTextPool(hudView.transform, font);
            report.Append($"飘字池预置对象：{poolCount} 条\n");

            // ── 3. 重接 HudView 引用 ──
            var reconnected = ReconnectFields(hudView, font);
            report.Append($"重接 HudView 引用：{reconnected} 个");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SceneBuilder.ScenePath);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>
        /// 预置飘字池：`Presentation/FloatingText/Pool` 下建 <see cref="HudBuilder.FloatingTextPoolSize"/> 条
        /// 带 TextMeshPro 的对象（初始 inactive）。运行时的 <c>FloatingTextSpawner</c> 只在这些对象上轮转播放，
        /// 不再 `new GameObject`。缺这一步的表现是「合成有反馈但看不到飘字」。
        /// </summary>
        private static int EnsureFloatingTextPool(Transform hudRoot, TMP_FontAsset font)
        {
            var spawner = hudRoot.GetComponentInChildren<FloatingTextSpawner>(true);
            if (spawner == null)
                return 0;

            var poolRoot = spawner.transform.Find("Pool");
            if (poolRoot == null)
            {
                var go = new GameObject("Pool");
                go.transform.SetParent(spawner.transform, false);
                poolRoot = go.transform;
            }

            var items = new List<FloatingTextItem>();
            for (var i = 0; i < HudBuilder.FloatingTextPoolSize; i++)
            {
                var name = $"FloatingText_{i:00}";
                var existing = poolRoot.Find(name);
                GameObject itemGo;
                if (existing != null)
                {
                    itemGo = existing.gameObject;
                }
                else
                {
                    itemGo = new GameObject(name);
                    itemGo.transform.SetParent(poolRoot, false);
                }

                var mesh = itemGo.GetComponent<TextMeshPro>();
                if (mesh == null)
                    mesh = itemGo.AddComponent<TextMeshPro>();
                if (font != null)
                    mesh.font = font;
                mesh.alignment = TextAlignmentOptions.Center;
                mesh.fontSize = 4;
                mesh.text = string.Empty;

                var renderer = itemGo.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    if (font != null && font.material != null)
                        renderer.sharedMaterial = font.material;
                    renderer.sortingOrder = 600;
                }

                var item = itemGo.GetComponent<FloatingTextItem>();
                if (item == null)
                    item = itemGo.AddComponent<FloatingTextItem>();
                items.Add(item);

                itemGo.SetActive(false);
            }

            // 池子数组是序列化字段，必须显式写回（否则运行时 pool 为空、飘字静默消失）。
            var serialized = new SerializedObject(spawner);
            var property = serialized.FindProperty("pool");
            if (property != null)
            {
                property.arraySize = items.Count;
                for (var i = 0; i < items.Count; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return items.Count;
        }

        /// <summary>按字段名 → 场景对象路径重接 HudView 的引用（类型已变，序列化引用会断）。</summary>
        private static int ReconnectFields(HudView view, TMP_FontAsset font)
        {
            var connected = 0;
            var serialized = new SerializedObject(view);

            // 字段名 → 层级路径（与 HudBuilder 生成的结构一致）
            var paths = new Dictionary<string, string>
            {
                ["bestScoreText"] = "Canvas/BestScore",
                ["scoreText"] = "Canvas/Score",
                ["settingsButton"] = "Canvas/SettingsButton",
                ["shakeButton"] = "Canvas/ShakeButton",
                ["shakeLabel"] = "Canvas/ShakeButton/Label",
                ["hammerButton"] = "Canvas/HammerButton",
                ["hammerLabel"] = "Canvas/HammerButton/Label",
                ["giftButton"] = "Canvas/GiftButton",
                ["giftLabel"] = "Canvas/GiftButton/Label",
                ["undoButton"] = "Canvas/UndoButton",
                ["undoLabel"] = "Canvas/UndoButton/Label",
                ["bombButton"] = "Canvas/BombButton",
                ["bombLabel"] = "Canvas/BombButton/Label",
                ["leaderboardButton"] = "Canvas/LeaderboardButton",
                ["privacyPanel"] = "Canvas/PrivacyPanel",
                ["privacyText"] = "Canvas/PrivacyPanel/Panel/Body",
                ["privacyAcceptButton"] = "Canvas/PrivacyPanel/Panel/AcceptButton",
                ["privacyDeclineButton"] = "Canvas/PrivacyPanel/Panel/DeclineButton",
                ["settlementPanel"] = "Canvas/SettlementPanel",
                ["settlementTitleText"] = "Canvas/SettlementPanel/Panel/Title",
                ["settlementScoreText"] = "Canvas/SettlementPanel/Panel/Score",
                ["settlementBestText"] = "Canvas/SettlementPanel/Panel/Best",
                ["settlementHintText"] = "Canvas/SettlementPanel/Panel/Hint",
                ["reviveButton"] = "Canvas/SettlementPanel/Panel/ReviveButton",
                ["reviveLabel"] = "Canvas/SettlementPanel/Panel/ReviveButton/Label",
                ["retryButton"] = "Canvas/SettlementPanel/Panel/RetryButton",
                ["shareButton"] = "Canvas/SettlementPanel/Panel/ShareButton",
                ["settingsPanel"] = "Canvas/SettingsPanel",
                ["sfxToggle"] = "Canvas/SettingsPanel/Panel/SfxToggle",
                ["sfxSlider"] = "Canvas/SettingsPanel/Panel/SfxToggle/VolumeBar",
                ["sfxVolumeFill"] = "Canvas/SettingsPanel/Panel/SfxToggle/VolumeBar/Fill",
                ["musicToggle"] = "Canvas/SettingsPanel/Panel/MusicToggle",
                ["musicSlider"] = "Canvas/SettingsPanel/Panel/MusicToggle/VolumeBar",
                ["musicVolumeFill"] = "Canvas/SettingsPanel/Panel/MusicToggle/VolumeBar/Fill",
                ["vibrateToggle"] = "Canvas/SettingsPanel/Panel/VibrateToggle",
                ["settingsCloseButton"] = "Canvas/SettingsPanel/Panel/CloseButton",
                ["privacyPolicyButton"] = "Canvas/SettingsPanel/Panel/PrivacyPolicyButton",
                ["userAgreementButton"] = "Canvas/SettingsPanel/Panel/UserAgreementButton",
                ["antiAddictionButton"] = "Canvas/SettingsPanel/Panel/AntiAddictionButton",
                ["clearCacheButton"] = "Canvas/SettingsPanel/Panel/ClearCacheButton",
                ["versionText"] = "Canvas/SettingsPanel/Panel/Version",
                ["leaderboardPanel"] = "Canvas/LeaderboardPanel",
                ["leaderboardText"] = "Canvas/LeaderboardPanel/Panel/Body",
                ["leaderboardCloseButton"] = "Canvas/LeaderboardPanel/Panel/CloseButton",
                ["leaderboardNextPageButton"] = "Canvas/LeaderboardPanel/Panel/NextPageButton",
                ["adOverlay"] = "Canvas/AdOverlay",
                ["adOverlayText"] = "Canvas/AdOverlay/AdText",
                ["toastPanel"] = "Canvas/Toast",
                ["toastText"] = "Canvas/Toast/Text",
                ["loadingPanel"] = "Canvas/LoadingPanel",
                ["loadingProgressFill"] = "Canvas/LoadingPanel/ProgressTrack/ProgressFill",
                ["loadingPercentText"] = "Canvas/LoadingPanel/Percent",
                ["loadingHintText"] = "Canvas/LoadingPanel/Hint",
                ["tutorialArrow"] = "Canvas/TutorialArrow",
            };

            foreach (var pair in paths)
            {
                var property = serialized.FindProperty(pair.Key);
                if (property == null)
                    continue;

                var target = FindPath(view.transform, pair.Value);
                if (target == null)
                    continue;

                AssignComponent(property, target.gameObject);
                connected++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return connected;
        }

        /// <summary>按字段类型取对应组件（TMP 文本 / UiPanel / Slider / Toggle / Button / Image）。</summary>
        private static void AssignComponent(SerializedProperty property, GameObject go)
        {
            var type = property.propertyType == SerializedPropertyType.ObjectReference
                ? property.objectReferenceValue?.GetType()
                : null;

            // 类型已变：按字段名推断目标组件
            var name = property.name;
            Object value = null;

            if (name.EndsWith("Text") || name.EndsWith("Label"))
                value = go.GetComponent<TextMeshProUGUI>();
            else if (name.EndsWith("Panel") || name == "adOverlay")
                value = go.GetComponent<UiPanel>();
            else if (name.EndsWith("Slider"))
                value = go.GetComponent<Slider>();
            else if (name.EndsWith("Toggle"))
                value = go.GetComponent<Toggle>();
            else if (name.EndsWith("Button"))
                value = go.GetComponent<Button>();
            else if (name.EndsWith("Fill") || name == "tutorialArrow")
                value = go.GetComponent<Image>();
            else
            {
                // 兜底：按已知类型逐个取第一个非空的（C# 的 ?? 对不同类型的 Unity 对象不可用）
                Object found = go.GetComponent<UiPanel>();
                if (found == null) found = go.GetComponent<TextMeshProUGUI>();
                if (found == null) found = go.GetComponent<Slider>();
                if (found == null) found = go.GetComponent<Toggle>();
                if (found == null) found = go.GetComponent<Button>();
                if (found == null) found = go.GetComponent<Image>();
                value = found;
            }

            if (value != null)
                property.objectReferenceValue = value;
            else
                Debug.LogWarning($"[UiTmpMigrator] 「{name}」在 {go.name} 上找不到匹配组件");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>按 "A/B/C" 从 root 往下找（root 自身算起）。</summary>
        private static Transform FindPath(Transform root, string path)
        {
            var current = root;
            foreach (var part in path.Split('/'))
            {
                current = current.Find(part);
                if (current == null)
                    return null;
            }
            return current;
        }

        private static TextAlignmentOptions ToTmp(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }
    }
}
