using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 程序化生成占位美术（决策 D4）：全部为白色可染色贴图，替换正式资源时只需替换文件。
    /// 输出到 Resources 以便运行时兜底也能加载。
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        public const string Folder = "Assets/Resources/Placeholder";
        public const string FruitCirclePath = Folder + "/fruit_circle.png";
        public const string ArrowPath = Folder + "/ui_arrow.png";
        public const string DotPath = Folder + "/ui_dot.png";

        private const int CircleSize = 256;
        private const int ArrowSize = 128;
        private const int DotSize = 32;

        [MenuItem("MergeWater/Generate Placeholder Art", priority = 1)]
        public static void GenerateMenu() => GenerateAll();

        public static void GenerateAll()
        {
            EnsureFolder();

            WriteSprite("fruit_circle", CircleSize, CircleSize, DrawCircle, CircleSize);
            WriteSprite("ui_arrow", ArrowSize, ArrowSize, DrawArrow, 100f);
            WriteSprite("ui_dot", DotSize, DotSize, DrawDot, 100f);

            AssetDatabase.Refresh();
            Debug.Log($"[PlaceholderArtGenerator] 占位美术已生成：{Folder}");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AssetDatabase.CreateFolder("Assets/Resources", "Placeholder");
        }

        private static void WriteSprite(string name, int width, int height, Func<Vector2, int, Color> shader,
            float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = shader(new Vector2(x, y), width);
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            var path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        /// <summary>白色实心圆 + 略暗边缘，便于按等级染色后仍有轮廓可读。</summary>
        private static Color DrawCircle(Vector2 pixel, int size)
        {
            var half = size * 0.5f;
            var dx = pixel.x + 0.5f - half;
            var dy = pixel.y + 0.5f - half;
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            var edge = half - 1.5f;

            var alpha = Mathf.Clamp01((edge - distance) / 2f);
            var rim = Mathf.Clamp01((distance - (edge - size * 0.05f)) / (size * 0.05f));
            var value = Mathf.Lerp(1f, 0.82f, rim);

            return new Color(value, value, value, alpha);
        }

        /// <summary>朝下的实心三角，用于新手引导箭头。</summary>
        private static Color DrawArrow(Vector2 pixel, int size)
        {
            var half = size * 0.5f;
            var x = pixel.x + 0.5f;
            var y = pixel.y + 0.5f;

            var baseY = size * 0.78f;
            var apexY = size * 0.22f;

            if (y > baseY || y < apexY)
                return new Color(1f, 1f, 1f, 0f);

            var t = Mathf.InverseLerp(baseY, apexY, y);
            var halfWidth = Mathf.Lerp(size * 0.40f, 0f, t);
            var distance = Mathf.Abs(x - half) - halfWidth;
            var alpha = Mathf.Clamp01(0.5f - distance);

            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color DrawDot(Vector2 pixel, int size)
        {
            var half = size * 0.5f;
            var dx = pixel.x + 0.5f - half;
            var dy = pixel.y + 0.5f - half;
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            var alpha = Mathf.Clamp01((half - 1f - distance) / 1.5f);

            return new Color(1f, 1f, 1f, alpha);
        }
    }
}
