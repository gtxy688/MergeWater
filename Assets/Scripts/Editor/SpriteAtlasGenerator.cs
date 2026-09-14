using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace MergeWater.Editor
{
    /// <summary>
    /// 生成/更新 **Sprite Atlas**（提升合批、减少 draw call）。
    ///
    /// <para>两个图集，分开生成、分开取舍：</para>
    /// <list type="bullet">
    /// <item><b>UI 图集</b>（`Assets/Atlases/UIAtlas.spriteatlas`）：`Assets/UI/Art/` 下的按钮/面板/图标/
    /// 进度条/开关等小图。它们本来就在 `Assets/UI/Art/`（不在 `Resources`），**进图集不增加包体**，
    /// 只是把 27 张独立贴图并成 1 张 → 减少 SetPass / Batches。三张满屏背景（`bg_star`/`Gamebg`/`bg_night`）
    /// **刻意排除**：它们各自就是单次绘制，塞进图集只会占地方并顶到 2048 上限。</item>
    /// <item><b>星球图集</b>（`Assets/Atlases/FruitAtlas.spriteatlas`）：`Assets/Resources/kenney_planets/Planets`
    /// 的 `planet00..09`。这是合批收益最大的一处（场上最多 120 颗水果，同贴图才能合批），
    /// **但有包体风险**：这些贴图在 `Resources/` 下会被无条件打进包，图集可能让同样的像素各带一份
    /// （约 +2.6MB）——所以单独一个菜单，先量包体再决定要不要用。</item>
    /// </list>
    ///
    /// <para>本类只产出**图集资产**，不改场景、不生成界面（V2.51 之后 UI 只手工维护）。
    /// 门禁：<c>SpriteAtlasTests</c>（存在性、内容、排除项、2048 尺寸上限、以及「美术目录新增了图却忘了进图集」的漂移）。</para>
    /// </summary>
    public static class SpriteAtlasGenerator
    {
        /// <summary>图集资产目录（刻意不放 `Resources/`：图集资产本身不需要 `Resources.Load`）。</summary>
        public const string AtlasFolder = "Assets/Atlases";

        public const string UiAtlasPath = AtlasFolder + "/UIAtlas.spriteatlas";
        public const string FruitAtlasPath = AtlasFolder + "/FruitAtlas.spriteatlas";

        /// <summary>UI 美术目录（小图，全部进 UI 图集）。</summary>
        public const string UiArtRoot = "Assets/UI/Art";

        /// <summary>
        /// **刻意排除**在 UI 图集之外的贴图（按文件名，不含扩展名）：
        /// 它们都是铺满屏幕的单张大图，各自只画一次，进图集只会浪费图集空间并可能超出 2048 上限。
        /// </summary>
        public static readonly string[] UiArtExcludedNames = { "bg_star", "Gamebg", "bg_night" };

        /// <summary>星球（水果）贴图目录。</summary>
        public const string PlanetFolder = "Assets/Resources/kenney_planets/Planets";

        /// <summary>
        /// 图集上限：**2048**。微信小游戏跑在 WebGL 派生运行时上，ES2 只保证 2048²；
        /// 放大到 4096 在老设备上会直接变成不可用贴图（D16/V2.48 之后的既有约束）。
        /// </summary>
        public const int MaxAtlasSize = 2048;

        // ── 菜单 ─────────────────────────────────────────────────────

        [MenuItem("MergeWater/Generate UI Sprite Atlas", priority = 8)]
        public static void GenerateUiAtlasMenu()
        {
            var count = GenerateUiAtlas();
            EditorUtility.DisplayDialog("MergeWater",
                $"已生成/更新 UI 图集：\n{UiAtlasPath}\n\n共 {count} 个 sprite（已排除 {string.Join("、", UiArtExcludedNames)}）。", "好的");
        }

        [MenuItem("MergeWater/Generate Fruit Sprite Atlas", priority = 8)]
        public static void GenerateFruitAtlasMenu()
        {
            var count = GenerateFruitAtlas();
            EditorUtility.DisplayDialog("MergeWater",
                $"已生成/更新星球图集：\n{FruitAtlasPath}\n\n包含 {count} 个 sprite。\n\n" +
                "注意：这些贴图在 Resources/ 下会被无条件打进包，图集**可能**让包体增加约 2.6MB——" +
                "决定启用前请先量一次包体。", "好的");
        }

        // ── 生成 ─────────────────────────────────────────────────────

        /// <summary>生成/更新 UI 图集，返回打包进去的 sprite 数量。</summary>
        public static int GenerateUiAtlas()
        {
            var sprites = FindUiArtSprites();
            WriteAtlas(UiAtlasPath, sprites.Cast<Object>().ToArray());
            Debug.Log($"[SpriteAtlasGenerator] UI 图集已更新：{UiAtlasPath}，{sprites.Count} 个 sprite" +
                      $"（排除 {string.Join("、", UiArtExcludedNames)}）。");
            return sprites.Count;
        }

        /// <summary>生成/更新星球图集（按**目录**打包，以后新增 planetNN 会自动进图集），返回包内 sprite 数。</summary>
        public static int GenerateFruitAtlas()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(PlanetFolder);
            if (folder == null)
            {
                Debug.LogError($"[SpriteAtlasGenerator] 找不到星球贴图目录：{PlanetFolder}");
                return 0;
            }

            WriteAtlas(FruitAtlasPath, new Object[] { folder });
            var count = AssetDatabase.FindAssets("t:Sprite", new[] { PlanetFolder }).Length;
            Debug.Log($"[SpriteAtlasGenerator] 星球图集已更新：{FruitAtlasPath}，目录内 {count} 个 sprite。");
            return count;
        }

        /// <summary>`Assets/UI/Art` 下应当进 UI 图集的 sprite（按路径排序，排除满屏大图）。</summary>
        public static List<Sprite> FindUiArtSprites()
        {
            // 用绝对路径扫盘（不依赖进程当前目录），再换回 "Assets/..." 资产路径。
            var dataPath = Application.dataPath.Replace('\\', '/');
            var absoluteRoot = dataPath + "/UI/Art";
            if (!Directory.Exists(absoluteRoot))
                return new List<Sprite>();

            return Directory.GetFiles(absoluteRoot, "*.png", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => !UiArtExcludedNames.Contains(Path.GetFileNameWithoutExtension(path)))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace(dataPath, "Assets")))
                .Where(sprite => sprite != null)
                .ToList();
        }

        private static void WriteAtlas(string path, Object[] packables)
        {
            if (!AssetDatabase.IsValidFolder(AtlasFolder))
                AssetDatabase.CreateFolder("Assets", "Atlases");

            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, path);
            }

            // 打包设置：不旋转（UI 与星球都不该被转 90°）、Tight 打包、留 4px 边距防溢色。
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                padding = 4,
                enableRotation = false,
                enableTightPacking = true,
                blockOffset = 1
            });

            // 贴图设置：不可读、无 mipmap（UI/2D 精灵用不到），双线性过滤。
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            });

            // 平台设置：把尺寸上限钉在 2048（微信/ES2 安全线），压缩沿用 Unity 的自动选择。
            atlas.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                overridden = true,
                maxTextureSize = MaxAtlasSize,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 50,
                crunchedCompression = false,
                allowsAlphaSplitting = false
            });

            // 内容：先清空再写入，避免「删掉的图还留在图集里」。目录型 packable 会被保留为目录。
            var existing = SpriteAtlasExtensions.GetPackables(atlas);
            if (existing != null && existing.Length > 0)
                SpriteAtlasExtensions.Remove(atlas, existing);
            if (packables.Length > 0)
                SpriteAtlasExtensions.Add(atlas, packables);

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // 立刻打一次包，让 spriteCount 有值（编辑器里也能马上看到合批效果）。
            // V2 模式下打包通常由导入/构建自动完成，这里只是「顺手催一下」——失败不影响图集资产本身。
            try
            {
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SpriteAtlasGenerator] 手动打包未执行（不影响图集设置，构建时仍会打包）：{e.Message}");
            }
        }
    }
}
