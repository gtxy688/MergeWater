using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeWater.Editor
{
    /// <summary>
    /// TMP 字体引用工具：审计 + 重指。
    ///
    /// <para>解决两类问题：
    /// 1. TMP 组件引用了不存在 / 不该用的字体资产（例如 TMP 默认的 LiberationSans SDF、缺字的字体，
    ///    或**指向字体资产里的材质子资产、但那个 fileID 已经不存在**——重建字体资产后极易出现，
    ///    组件本身看不出异常，材质却是悬空的）。
    /// 2. 新增语言字体资产后，把场景 / Prefab / TMP Settings 的引用一次性指过去。</para>
    ///
    /// <para>**只改字体与材质引用**，不碰 RectTransform、字号、颜色、对齐、换行——那些属于美术与排版决策。</para>
    /// </summary>
    public static class TMPFontReferenceTool
    {
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("MergeWater/Font/审计 TMP 字体引用", priority = 22)]
        public static void AuditMenu() => AuditAndReport();

        [MenuItem("MergeWater/Font/3. 重指到随包中文字体（可选）", priority = 23)]
        public static void RepointMenu()
        {
            if (!EditorUtility.DisplayDialog("MergeWater",
                    $"把场景与 Prefab 里所有 TMP 文本的字体/材质重指到：\n{TMPFontBuilder.OutputPath}\n\n" +
                    "只改字体与材质引用，不动布局、字号、颜色、对齐与换行。",
                    "执行", "取消"))
                return;

            RepointAll();
        }

        // ── 审计 ─────────────────────────────────────────────────────

        /// <summary>只报告不修改：字体 / 材质 / 对齐 / 换行 / 超长文本。</summary>
        public static void AuditAndReport()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMPFontBuilder.OutputPath);
            var validMaterial = fontAsset != null ? fontAsset.material : null;

            var sb = new StringBuilder();
            sb.AppendLine("[TMPFontReferenceTool] ===== TMP 字体引用审计 =====");
            sb.AppendLine($"  目标字体资产：{TMPFontBuilder.OutputPath}" +
                          (fontAsset == null ? "（不存在）" : ""));
            sb.AppendLine($"  其材质 fileID：{(validMaterial == null ? "—" : GetLocalFileId(validMaterial).ToString())}");

            var settingsFont = FindTmpSettingsFont();
            sb.AppendLine($"  TMP Settings 默认字体：{Describe(settingsFont)}");
            sb.AppendLine();

            var total = 0;
            var dangling = 0;
            var wrongFont = 0;
            var justified = 0;
            var wrapOffLong = 0;
            var rendererDangling = 0;
            var alignment = new SortedDictionary<string, int>();

            foreach (var (owner, component) in EnumerateTmpTexts(includePrefabs: true))
            {
                total++;
                var so = new SerializedObject(component);
                var fontProp = so.FindProperty("m_fontAsset");
                var matProp = so.FindProperty("m_sharedMaterial");
                var textProp = so.FindProperty("m_text");
                var hProp = so.FindProperty("m_HorizontalAlignment");
                var vProp = so.FindProperty("m_VerticalAlignment");
                var wProp = so.FindProperty("m_enableWordWrapping");

                var font = fontProp?.objectReferenceValue as TMP_FontAsset;
                var mat = matProp?.objectReferenceValue as Material;
                var text = textProp?.stringValue ?? string.Empty;
                var h = hProp?.intValue ?? 0;
                var v = vProp?.intValue ?? 0;
                var wrap = (wProp?.intValue ?? 0) != 0;

                var alignKey = DescribeAlignment(h, v);
                alignment.TryGetValue(alignKey, out var n);
                alignment[alignKey] = n + 1;

                if (font != fontAsset)
                    wrongFont++;

                if (mat == null)
                    dangling++;

                // MeshRenderer 上的字形材质（世界空间 TMP）：重建字体资产后这里最容易留下悬空引用
                var meshRenderer = component.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    var rendererObject = new SerializedObject(meshRenderer);
                    var rendererMaterials = rendererObject.FindProperty("m_Materials");
                    if (rendererMaterials != null && rendererMaterials.isArray)
                    {
                        for (var i = 0; i < rendererMaterials.arraySize; i++)
                        {
                            if (rendererMaterials.GetArrayElementAtIndex(i).objectReferenceValue != fontAsset?.material)
                            {
                                rendererDangling++;
                                break;
                            }
                        }
                    }
                }

                if (h == 8 || h == 16)
                {
                    justified++;
                    sb.AppendLine($"  [Justified/Flush] {owner} 「{Trim(text)}」 对齐={alignKey}");
                }

                // 长文本却关掉了自动换行：中文不会自动折行，容易溢出（需求方明确关注点）
                if (!wrap && DisplayLength(text) > 12)
                {
                    wrapOffLong++;
                    sb.AppendLine($"  [换行关闭] {owner} 「{Trim(text)}」 对齐={alignKey}");
                }
            }

            sb.AppendLine($"  TMP 文本组件：{total} 个");
            sb.AppendLine($"  字体不是目标字体的：{wrongFont} 个");
            sb.AppendLine($"  材质引用为空（悬空）：{dangling} 个");
            sb.AppendLine($"  MeshRenderer 材质未指向目标材质（悬空/串材质）：{rendererDangling} 个");
            sb.AppendLine($"  使用 Justified/Flush（中文会出现异常大空隙）：{justified} 个");
            sb.AppendLine($"  长文本但关闭了自动换行：{wrapOffLong} 个");
            sb.Append("  对齐分布：");
            foreach (var pair in alignment)
                sb.Append($"{pair.Key}={pair.Value}  ");
            sb.AppendLine();
            sb.AppendLine("[TMPFontReferenceTool] ===== 审计结束 =====");

            Debug.Log(sb.ToString());
        }

        // ── 重指 ─────────────────────────────────────────────────────

        /// <summary>
        /// 把场景 / Prefab / TMP Settings 的字体引用重指到 <see cref="TMPFontBuilder.OutputPath"/>，
        /// 并顺带修掉悬空材质。返回改动的组件数。
        /// </summary>
        public static int RepointAll()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMPFontBuilder.OutputPath);
            if (fontAsset == null)
            {
                Debug.LogError($"[TMPFontReferenceTool] 找不到字体资产 {TMPFontBuilder.OutputPath}；" +
                               "先运行 MergeWater/Font/2. 烘焙中文 TMP 字体资产。");
                return 0;
            }

            var changed = 0;

            // 1) 已打开的场景
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var touched = RepointInRoots(scene.GetRootGameObjects(), fontAsset);
                if (touched > 0)
                {
                    changed += touched;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            if (changed > 0)
                EditorSceneManager.SaveOpenScenes();

            // 2) Prefab（排除第三方素材包）
            changed += RepointPrefabs(fontAsset);

            // 3) TMP Settings 的默认字体
            if (RepointTmpSettings(fontAsset))
                changed++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMPFontReferenceTool] 重指完成：共修改 {changed} 处引用 → {TMPFontBuilder.OutputPath}");
            return changed;
        }

        /// <summary>命令行入口：打开主场景 → 审计（改前）→ 重指 → 保存 → 再审计（改后）→ 退出。</summary>
        public static void RepointFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMPFontBuilder.OutputPath);
            if (fontAsset == null)
            {
                Debug.LogError($"[TMPFontReferenceTool] 找不到字体资产 {TMPFontBuilder.OutputPath}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[TMPFontReferenceTool] ======== 重指前 ========");
            AuditAndReport();

            var touched = RepointInRoots(scene.GetRootGameObjects(), fontAsset);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var prefabs = RepointPrefabs(fontAsset);
            var settings = RepointTmpSettings(fontAsset) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 重新加载场景，让改后的审计读的是**已落盘的序列化数据**，而不是内存里的临时状态。
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            Debug.Log("[TMPFontReferenceTool] ======== 重指后 ========");
            AuditAndReport();

            Debug.Log($"[TMPFontReferenceTool] 场景内重指 {touched} 个组件，Prefab {prefabs} 个，TMP Settings {settings} 处。");
            EditorApplication.Exit(0);
        }

        /// <summary>命令行入口：只审计，不修改。</summary>
        public static void AuditFromCommandLine()
        {
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            AuditAndReport();
            EditorApplication.Exit(0);
        }

        private static int RepointInRoots(GameObject[] roots, TMP_FontAsset fontAsset)
        {
            var changed = 0;
            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (RepointComponent(component, fontAsset))
                        changed++;
                }
            }

            return changed;
        }

        private static int RepointPrefabs(TMP_FontAsset fontAsset)
        {
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');

                // 第三方 UI 素材包不动：它的示例文本与本作无关，改它反而破坏素材包完整性。
                if (path.StartsWith("Assets/Art/", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null)
                    continue;

                try
                {
                    var touched = 0;
                    foreach (var component in prefabRoot.GetComponentsInChildren<TMP_Text>(true))
                        if (RepointComponent(component, fontAsset))
                            touched++;

                    if (touched > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        changed += touched;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return changed;
        }

        private static bool RepointTmpSettings(TMP_FontAsset fontAsset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
                return false;

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null || prop.objectReferenceValue == fontAsset)
                return false;

            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return true;
        }

        /// <summary>
        /// 把单个 TMP 组件的字体与材质指向目标字体资产，并清掉同 GameObject 上
        /// <see cref="MeshRenderer"/> 遗留的悬空材质（世界空间 TMP 才会用到 MeshRenderer）。
        /// 返回是否发生了改动。
        /// </summary>
        private static bool RepointComponent(TMP_Text component, TMP_FontAsset fontAsset)
        {
            var changed = false;

            var so = new SerializedObject(component);
            var fontProp = so.FindProperty("m_fontAsset");
            var matProp = so.FindProperty("m_sharedMaterial");
            if (fontProp != null && matProp != null)
            {
                var fontOk = fontProp.objectReferenceValue == fontAsset;
                var matOk = matProp.objectReferenceValue == fontAsset.material;
                if (!fontOk || !matOk)
                {
                    fontProp.objectReferenceValue = fontAsset;
                    matProp.objectReferenceValue = fontAsset.material;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(component);
                    changed = true;
                }
            }

            if (RepointRendererMaterials(component, fontAsset))
                changed = true;

            return changed;
        }

        /// <summary>
        /// 世界空间 TMP（<c>TextMeshPro</c>）由 <see cref="MeshRenderer"/> 绘制，字形材质记在它的
        /// <c>m_Materials</c> 数组里。**重建字体资产会换掉材质子资产的 fileID**，而这个数组不会自动
        /// 跟着更新，于是留下指向已删除材质的悬空引用——2026-09-13 的飘字池 12 条就是这个形态：
        /// 组件的 <c>m_sharedMaterial</c> 看着正常，MeshRenderer 上却挂着不存在的材质，而当时的审计
        /// 只看组件字段，所以完全没发现。TMP 运行时确实会重新赋材质，但序列化数据必须干净。
        /// </summary>
        private static bool RepointRendererMaterials(TMP_Text component, TMP_FontAsset fontAsset)
        {
            var meshRenderer = component.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return false;

            var serialized = new SerializedObject(meshRenderer);
            var materials = serialized.FindProperty("m_Materials");
            if (materials == null || !materials.isArray)
                return false;

            var alreadyCorrect = materials.arraySize == 1 &&
                                 materials.GetArrayElementAtIndex(0).objectReferenceValue == fontAsset.material;
            if (alreadyCorrect)
                return false;

            materials.arraySize = 1;
            materials.GetArrayElementAtIndex(0).objectReferenceValue = fontAsset.material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(meshRenderer);
            return true;
        }

        // ── 辅助 ─────────────────────────────────────────────────────

        private static IEnumerable<(string Owner, TMP_Text Component)> EnumerateTmpTexts(bool includePrefabs)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<TMP_Text>(true))
                        yield return ($"{scene.name}/{PathOf(c.transform)}", c);
            }

            if (!includePrefabs)
                yield break;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.StartsWith("Assets/Art/", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null)
                    continue;

                try
                {
                    foreach (var c in prefabRoot.GetComponentsInChildren<TMP_Text>(true))
                        yield return ($"{path}/{PathOf(c.transform)}", c);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }

            return sb.ToString();
        }

        private static TMP_FontAsset FindTmpSettingsFont()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
                return null;

            var so = new SerializedObject(settings);
            return so.FindProperty("m_defaultFontAsset")?.objectReferenceValue as TMP_FontAsset;
        }

        private static string Describe(Object asset)
        {
            if (asset == null)
                return "（空）";

            return $"{asset.name}（{AssetDatabase.GetAssetPath(asset)}）";
        }

        private static long GetLocalFileId(Object asset)
        {
            if (asset == null)
                return 0;

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long localId) ? localId : 0;
        }

        private static string DescribeAlignment(int h, int v)
        {
            var hs = h switch
            {
                1 => "Left",
                2 => "Center",
                4 => "Right",
                8 => "Justified",
                16 => "Flush",
                32 => "Geometry",
                _ => "h" + h
            };
            var vs = v switch
            {
                256 => "Top",
                512 => "Middle",
                1024 => "Bottom",
                2048 => "Baseline",
                4096 => "Geometry",
                _ => "v" + v
            };
            return hs + "/" + vs;
        }

        private static int DisplayLength(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var n = 0;
            foreach (var c in text)
                if (c != '\n' && c != '\r')
                    n++;

            return n;
        }

        private static string Trim(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var oneLine = text.Replace('\n', ' ').Replace('\r', ' ');
            return oneLine.Length <= 40 ? oneLine : oneLine.Substring(0, 40) + "…";
        }
    }
}
