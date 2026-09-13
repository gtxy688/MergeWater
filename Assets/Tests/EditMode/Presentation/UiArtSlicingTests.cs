using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// M5 验收：九宫格 UI 素材的导入与缩放不变量。
    ///
    /// <para><c>Image.Type.Sliced</c> 的边框会先换算成设计单位再决定九个切片的尺寸：
    /// <c>borderUI = border × Canvas.referencePixelsPerUnit ÷ sprite.pixelsPerUnit ÷ pixelsPerUnitMultiplier</c>。
    /// 本项目 Canvas 的 referencePixelsPerUnit 为 100，因此**带边框的 UI 素材必须以 PPU=100 导入**，
    /// 边框才是 1:1（51px 圆角 = 51 设计单位）。素材若按 PPU=1 / 2 导入，边框会被放大 100 / 50 倍。</para>
    ///
    /// <para>2026-09-12 需求方实测「设置面板这 UI 是个啥」：PPU=1 让 51px 边框变成 5100 设计单位，
    /// 远超 900×1240 的面板尺寸，Unity 只能把边框按比例压满整个 RectTransform——素材被整体拉伸，
    /// 表现为一个大白椭圆套一个品红圆环，按钮圆角也被拉成畸形。这两条断言是该缺陷的回归门禁。</para>
    /// </summary>
    public sealed class UiArtSlicingTests
    {
        private const string ArtFolder = "Assets/UI/Art";

        /// <summary>与场景中 Canvas 的 <c>m_ReferencePixelsPerUnit</c> 一致。</summary>
        private const float CanvasReferencePixelsPerUnit = 100f;

        [Test]
        public void ArtWithNineSliceBorder_IsImportedAtCanvasReferencePixelsPerUnit()
        {
            var checkedSprites = 0;

            foreach (var path in Directory.GetFiles(ArtFolder, "*.png", SearchOption.TopDirectoryOnly))
            {
                var assetPath = path.Replace('\\', '/');
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                Assert.That(sprite, Is.Not.Null, $"应导入为 Sprite：{assetPath}");

                var border = sprite.border;
                if (Mathf.Approximately(border.sqrMagnitude, 0f))
                    continue;

                checkedSprites++;

                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(CanvasReferencePixelsPerUnit).Within(1e-3f),
                    $"{Path.GetFileName(assetPath)} 带九宫格边框，必须以 PPU={CanvasReferencePixelsPerUnit} 导入" +
                    $"（当前 {sprite.pixelsPerUnit}，边框会被放大 {CanvasReferencePixelsPerUnit / sprite.pixelsPerUnit:0} 倍并撑满 Rect）");
            }

            Assert.That(checkedSprites, Is.GreaterThanOrEqualTo(5), "应检查到面板/按钮/进度条等九宫格素材");
        }

        [Test]
        public void SceneSlicedImages_DoNotClampTheirBordersIntoTheWholeRect()
        {
            var scene = EditorSceneManager.OpenScene(MergeWater.Editor.MainSceneAsset.Path, OpenSceneMode.Additive);

            try
            {
                var images = Object.FindObjectsOfType<Image>(true)
                    .Where(image => image.gameObject.scene == scene)
                    .Where(image => image.type == Image.Type.Sliced && image.sprite != null)
                    .ToArray();

                Assert.That(images.Length, Is.GreaterThan(0), "场景中应有九宫格图片（面板/按钮/进度条）");

                foreach (var image in images)
                {
                    var sprite = image.sprite;
                    var rect = image.rectTransform.rect;
                    var multiplier = image.pixelsPerUnitMultiplier <= 0f ? 1f : image.pixelsPerUnitMultiplier;

                    var scale = CanvasReferencePixelsPerUnit / sprite.pixelsPerUnit / multiplier;
                    var borderUI = Mathf.Max(sprite.border.x, sprite.border.z) * scale;
                    var minSide = Mathf.Min(Mathf.Abs(rect.width), Mathf.Abs(rect.height));

                    // 边框合计超过元素尺寸时 Unity 会按比例压小，素材被整体拉伸（详见类注释）。
                    // 允许 1.25 倍余量：胶囊按钮高度略小于两倍边框时只会被轻微压扁。
                    Assert.That(borderUI, Is.LessThanOrEqualTo(minSide * 0.5f * 1.25f + 1f),
                        $"「{image.name}」({rect.width:0}×{rect.height:0}) 的九宫格边框换算后有 {borderUI:0} 设计单位，" +
                        "超过元素最小边的一半——边框会被压满整个 Rect，素材被整体拉伸成椭圆/畸形圆角");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
